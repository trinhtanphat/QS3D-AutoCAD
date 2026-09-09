namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Lifecycle facade over the canonical embedded MCP server; it does not own another tool registry.</summary>
internal static class McpEmbeddedServerV2
{
    internal static void EnsureStarted()
    {
        if (!McpEmbeddedServer.IsRunning) McpEmbeddedServer.Start();
    }

    internal static void Stop() => McpEmbeddedServer.Stop();

    internal static string StatusJson() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["running"] = McpEmbeddedServer.IsRunning,
        ["endpoint"] = McpTransportSettings.LocalEndpoint,
        ["protocol"] = McpEmbeddedServer.ProtocolVersion,
        ["canonicalServer"] = true,
        ["provenance"] = McpJson.Parse(McpRuntimeBuildProvenance.SnapshotJson())
    });
}
