using Autodesk.AutoCAD.Runtime;
using QS3D.AutoCAD.UI;

[assembly: CommandClass(typeof(QS3D.AutoCAD.Commands.Qs3dRibbonCommands))]

namespace QS3D.AutoCAD.Commands;

public sealed class Qs3dRibbonCommands
{
    [CommandMethod("QS3DRIBBON", CommandFlags.Modal)]
    public void EnsureRibbon()
    {
        var editor = Application.DocumentManager.MdiActiveDocument?.Editor;
        if (Qs3dRibbon.TryEnsure(out var message))
        {
            editor?.WriteMessage($"\n{message}\n");
        }
        else
        {
            editor?.WriteMessage($"\n{message} QS3D palette/commands remain available.\n");
        }
    }
}
