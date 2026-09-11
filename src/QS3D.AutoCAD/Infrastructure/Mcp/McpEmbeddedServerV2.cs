namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>
/// Versioned lifecycle facade over the canonical embedded MCP server.
/// It never owns a second tool registry or listener implementation.
/// </summary>
internal static class McpEmbeddedServerV2
{
    internal const string ContractVersion = "2";

    internal static bool EnsureStarted()
    {
        if (McpEmbeddedServer.IsRunning) return false;
        McpEmbeddedServer.Start();
        return true;
    }

    internal static void Stop() => McpEmbeddedServer.Stop();

    internal static string StatusJson() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["contractVersion"] = ContractVersion,
        ["running"] = McpEmbeddedServer.IsRunning,
        ["server"] = McpEmbeddedServer.ServerName,
        ["protocol"] = McpEmbeddedServer.ProtocolVersion,
        ["endpoint"] = McpTransportSettings.LocalEndpoint,
        ["lastError"] = McpEmbeddedServer.LastError,
        ["build"] = McpJson.Parse(McpRuntimeBuildProvenance.SnapshotJson()),
        ["delegatesToCanonicalServer"] = true
    });
}
