namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Bounded watchdog for the local listener only; never touches CAD state.</summary>
internal static class McpRuntimeWatchdog
{
    private static readonly object Sync = new();
    private static Thread? _thread;
    private static volatile bool _stopping;
    private static long _restartCount;
    private const int PollMilliseconds = 3000;

    internal static void Start()
    {
        lock (Sync)
        {
            if (_thread is not null) return;
            _stopping = false;
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "QS3D AutoCAD MCP watchdog"
            };
            _thread.Start();
        }
    }

    internal static void Stop()
    {
        Thread? thread;
        lock (Sync)
        {
            _stopping = true;
            thread = _thread;
            _thread = null;
        }
        if (thread is not null && thread != Thread.CurrentThread)
            try { thread.Join(PollMilliseconds + 500); } catch { }
    }

    internal static string SnapshotJson() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["running"] = _thread is not null && !_stopping,
        ["restartCount"] = Interlocked.Read(ref _restartCount),
        ["pollMilliseconds"] = PollMilliseconds,
        ["blockedByEmergencyStop"] = McpCadAgentRuntime.AutomationStopped,
        ["blockedByMutation"] = McpCadMutationCoordinator.IsMutationActive
    });

    private static void Run()
    {
        while (!_stopping)
        {
            Thread.Sleep(PollMilliseconds);
            if (_stopping) return;
            if (McpEmbeddedServer.IsRunning) continue;
            if (McpCadAgentRuntime.AutomationStopped || McpCadMutationCoordinator.IsMutationActive) continue;
            try
            {
                if (McpEmbeddedServerV2.EnsureStarted())
                {
                    Interlocked.Increment(ref _restartCount);
                    McpDiagnosticHub.Log("mcp-watchdog", "restarted safe local embedded listener");
                }
            }
            catch (Exception ex)
            {
                McpDiagnosticHub.Log("mcp-watchdog", "restart failed: " + ex.Message);
            }
        }
    }
}
