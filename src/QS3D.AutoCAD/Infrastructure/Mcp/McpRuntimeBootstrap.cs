namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal static class McpRuntimeBootstrap
{
    internal static string Start()
    {
        McpDiagnosticHub.InitializeForPlugin();
        McpEcosystemSettings.Load();
        McpBackgroundHostRuntime.ResetForProcessStart();
        McpFirstRunExperience.EnsureInitialized();
        McpEmbeddedServerV2.EnsureStarted();
        McpTransportSupervisor.Start();
        McpRuntimeWatchdog.Start();
        McpPopupObserver.Start();
        Qs3dCodeHostLocalIpcServer.Start();
        return McpTransportSettings.LocalEndpoint;
    }

    internal static void Stop()
    {
        try { Qs3dCodeHostLocalIpcServer.Stop(); } catch { }
        try { McpPopupObserver.Stop(); } catch { }
        try { McpRuntimeWatchdog.Stop(); } catch { }
        try { McpTransportSupervisor.Stop(); } catch { }
        try { McpEmbeddedServerV2.Stop(); } catch { }
        try { McpDesktopControlSession.Disable("plugin-terminate"); } catch { }
    }
}
