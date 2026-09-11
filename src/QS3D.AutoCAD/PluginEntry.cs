using Autodesk.AutoCAD.Runtime;
using QS3D.AutoCAD.Commands;
using QS3D.AutoCAD.Infrastructure.Mcp;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: ExtensionApplication(typeof(QS3D.AutoCAD.PluginEntry))]
[assembly: CommandClass(typeof(Qs3dCommands))]

namespace QS3D.AutoCAD;

public sealed class PluginEntry : IExtensionApplication
{
    public void Initialize()
    {
        var editor = AcApplication.DocumentManager.MdiActiveDocument?.Editor;
        try
        {
            McpDiagnosticHub.InitializeForPlugin();
            McpDesktopAutomationRuntime.DisableForeground("process-start");
            McpEcosystemSettings.Load();
            McpCloudflareOnboarding.Load();
            McpEmbeddedServerV2.EnsureStarted();
            McpRuntimeWatchdog.Start();
            McpTransportSupervisor.Start();
            try { McpPopupObserver.Start(); } catch (System.Exception ex) { McpDiagnosticHub.Log("popup-observer", "start failed: " + ex.Message); }
            try { McpProjectRecovery.Start(); } catch (System.Exception ex) { McpDiagnosticHub.Log("recovery", "start failed: " + ex.Message); }
            editor?.WriteMessage(
                "\nQS3D AutoCAD loaded. Run QS3D to open the command palette. MCP: "
                + McpTransportSettings.LocalEndpoint + "\n");
        }
        catch (System.Exception exception)
        {
            McpDiagnosticHub.Log("mcp-startup", "startup failed: " + exception.Message);
            editor?.WriteMessage(
                "\nQS3D AutoCAD loaded, but the local MCP endpoint did not start: "
                + exception.Message + "\n");
        }
    }

    public void Terminate()
    {
        try { McpProjectRecovery.Stop(); } catch { }
        try { McpPopupObserver.Stop(); } catch { }
        try { McpTransportSupervisor.Stop(); } catch { }
        try { McpSecureTunnelRuntime.StopForHostShutdown(); } catch { }
        try { McpOAuthConsentStore.RevokeAll("host-shutdown"); } catch { }
        try { McpRuntimeWatchdog.Stop(); } catch { }
        try { McpEmbeddedServerV2.Stop(); } catch { }
    }
}
