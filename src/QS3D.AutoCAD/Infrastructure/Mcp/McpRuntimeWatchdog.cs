using Timer = System.Threading.Timer;
namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Bounded local-listener watchdog. It never repairs while CAD mutation or emergency stop is active.</summary>
internal static class McpRuntimeWatchdog
{
    private static readonly object Sync = new();
    private static Timer? _timer;
    private static DateTime _lastCheckUtc;
    private static string _lastAction = "not-started";

    internal static void Start()
    {
        lock (Sync)
        {
            if (_timer is not null) return;
            _timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            _lastAction = "watching-local-listener";
        }
    }

    internal static void Stop()
    {
        Timer? timer;
        lock (Sync) { timer = _timer; _timer = null; _lastAction = "stopped"; }
        try { timer?.Dispose(); } catch { }
    }

    internal static string SnapshotJson()
    {
        lock (Sync)
        {
            return McpJson.Serialize(new Dictionary<string, object?>
            {
                ["started"] = _timer is not null,
                ["lastCheckUtc"] = _lastCheckUtc == default ? null : _lastCheckUtc.ToString("o"),
                ["lastAction"] = _lastAction,
                ["emergencyStopped"] = McpCadAgentRuntime.AutomationStopped,
                ["mutationActive"] = McpCadMutationCoordinator.IsMutationActive
            });
        }
    }

    private static void Tick()
    {
        try
        {
            lock (Sync) _lastCheckUtc = DateTime.UtcNow;
            if (McpCadAgentRuntime.AutomationStopped || McpCadMutationCoordinator.IsMutationActive) return;
            if (McpEmbeddedServer.IsRunning) return;
            if (McpTransportSettings.ExternalTransportEnabled) return;
            McpEmbeddedServerV2.EnsureStarted();
            lock (Sync) _lastAction = "restarted-safe-loopback-listener";
            McpDiagnosticHub.Log("mcp-watchdog", "safe local listener restart completed");
        }
        catch (Exception ex)
        {
            lock (Sync) _lastAction = "restart-failed";
            McpDiagnosticHub.Log("mcp-watchdog", "restart failed: " + ex.Message);
        }
    }
}
