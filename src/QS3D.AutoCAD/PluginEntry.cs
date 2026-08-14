using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;
using QS3D.AutoCAD.Commands;

[assembly: ExtensionApplication(typeof(QS3D.AutoCAD.PluginEntry))]
[assembly: CommandClass(typeof(Qs3dCommands))]

namespace QS3D.AutoCAD;

public sealed class PluginEntry : IExtensionApplication
{
    public void Initialize()
    {
        var editor = Application.DocumentManager.MdiActiveDocument?.Editor;
        editor?.WriteMessage("\nQS3D AutoCAD loaded. Run QS3D to open the command palette.\n");
    }

    public void Terminate()
    {
    }
}
