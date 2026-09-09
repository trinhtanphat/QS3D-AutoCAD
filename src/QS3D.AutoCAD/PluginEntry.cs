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
            McpEmbeddedServer.Start();
            McpTransportSupervisor.Start();
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
        try { McpTransportSupervisor.Stop(); } catch { }
        try { McpEmbeddedServer.Stop(); } catch { }
    }
}
