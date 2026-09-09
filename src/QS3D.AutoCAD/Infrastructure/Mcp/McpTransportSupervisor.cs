namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>
/// Supervises only transport intent. External providers are never spawned from the CAD host;
/// a user must explicitly enable and configure a provider outside this process.
/// </summary>
internal static class McpTransportSupervisor
{
    private static readonly object Sync = new();
    private static bool _started;
    private static string _status = "stopped";

    internal static void Start()
    {
        lock (Sync)
        {
            if (_started) return;
            _started = true;
            if (!McpTransportSettings.ExternalTransportEnabled)
            {
                _status = "local-only";
                McpDiagnosticHub.Log("mcp-transport", "external transport disabled; loopback endpoint only");
                return;
            }

            _status = "external-requested-provider-required";
            McpDiagnosticHub.Log(
                "mcp-transport",
                "external transport was explicitly requested, but the AutoCAD plugin never launches shell/process tunnel providers; configure the provider outside AutoCAD");
        }
    }

    internal static void Stop()
    {
        lock (Sync)
        {
            if (!_started) return;
            _started = false;
            _status = "stopped";
        }
        McpDiagnosticHub.Log("mcp-transport", "transport supervisor stopped");
    }

    internal static string StatusJson()
    {
        lock (Sync)
        {
            return McpJson.Serialize(new Dictionary<string, object?>
            {
                ["started"] = _started,
                ["mode"] = _status,
                ["localEndpoint"] = McpTransportSettings.LocalEndpoint,
                ["externalRequested"] = McpTransportSettings.ExternalTransportEnabled,
                ["providerLaunchOwnedByPlugin"] = false
            });
        }
    }
}
