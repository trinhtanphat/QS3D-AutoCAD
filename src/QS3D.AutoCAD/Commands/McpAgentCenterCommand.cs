using Autodesk.AutoCAD.Runtime;
using QS3D.AutoCAD.Infrastructure.Mcp;
using QS3D.AutoCAD.UI;

[assembly: CommandClass(typeof(QS3D.AutoCAD.Commands.McpAgentCenterCommand))]

namespace QS3D.AutoCAD.Commands;

public sealed class McpAgentCenterCommand
{
    [CommandMethod("QS3DMCPAGENTCENTER", CommandFlags.Modal)]
    public void Show()
    {
        McpFirstRunExperience.EnsureInitialized();
        var window = new McpAgentCenterWindow();
        window.Show();
    }
}
