using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Lightweight first-run reminder. It never opens UI or enables capabilities automatically.</summary>
internal static class McpFirstRunExperience
{
    private static readonly object Sync = new();
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(4);
    private static System.Threading.Timer? _timer;
    private static bool _reminderEmitted;

    internal static void Start()
    {
        lock (Sync)
        {
            if (_timer is not null || McpEcosystemSettings.FirstRunCompleted) return;
            _timer = new System.Threading.Timer(_ => EmitReminderOnce(), null, InitialDelay, Timeout.InfiniteTimeSpan);
        }
    }

    internal static void Stop()
    {
        System.Threading.Timer? timer;
        lock (Sync) { timer = _timer; _timer = null; }
        try { timer?.Dispose(); } catch { }
    }
    internal static void MarkCompletedFromLocalUser()
    {
        McpEcosystemSettings.MarkFirstRunCompletedFromLocalUser();
        Stop();
        McpDiagnosticHub.Log("mcp-first-run", "completed by explicit local user action");
    }

    internal static string SnapshotJson()
    {
        lock (Sync)
        {
            return McpJson.Serialize(new Dictionary<string, object?>
            {
                ["completed"] = McpEcosystemSettings.FirstRunCompleted,
                ["reminderScheduled"] = _timer is not null,
                ["reminderEmitted"] = _reminderEmitted,
                ["opensUiAutomatically"] = false,
                ["publicTransportAutoEnabled"] = false,
                ["foregroundControlAutoEnabled"] = false
            });
        }
    }

    private static void EmitReminderOnce()
    {
        lock (Sync)
        {
            if (_timer is null || McpEcosystemSettings.FirstRunCompleted) return;
            _reminderEmitted = true;
        }
        try
        {
            McpDiagnosticHub.InvokeInCadContext(() =>
            {
                AcApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                    "\nQS3D AutoCAD MCP is ready locally. Run QS3DMCPAGENTCENTER to review connection, consent, recovery and diagnostics.\n");
                return true;
            });
            McpDiagnosticHub.Log("mcp-first-run", "local reminder emitted; no capability activated");
        }
        catch (Exception ex)
        {
            McpDiagnosticHub.Log("mcp-first-run", "reminder failed: " + ex.Message);
        }
        finally
        {
            Stop();
        }
    }
}
