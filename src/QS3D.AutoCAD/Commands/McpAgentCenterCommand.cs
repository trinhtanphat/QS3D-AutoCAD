using Autodesk.AutoCAD.Runtime;
using QS3D.AutoCAD.UI;

[assembly: CommandClass(typeof(QS3D.AutoCAD.Commands.McpAgentCenterCommand))]

namespace QS3D.AutoCAD.Commands;

public sealed class McpAgentCenterCommand
{
    [CommandMethod("QS3DMCPAGENTCENTER", CommandFlags.Modal)]
    public void OpenAgentCenter()
    {
        McpAgentCenterWindow.ShowSingleton();
    }
}
