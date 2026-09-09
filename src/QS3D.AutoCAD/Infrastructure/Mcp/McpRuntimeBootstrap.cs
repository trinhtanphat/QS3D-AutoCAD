namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal static class McpRuntimeBootstrap
{
    internal static string Start()
    {
        McpDiagnosticHub.InitializeForPlugin();
        McpEmbeddedServer.Start();
        McpTransportSupervisor.Start();
        return McpTransportSettings.LocalEndpoint;
    }

    internal static void Stop()
    {
        McpTransportSupervisor.Stop();
        McpEmbeddedServer.Stop();
    }
}
