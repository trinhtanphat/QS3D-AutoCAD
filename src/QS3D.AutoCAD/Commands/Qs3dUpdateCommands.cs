using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(QS3D.AutoCAD.Commands.Qs3dUpdateCommands))]

namespace QS3D.AutoCAD.Commands;

public sealed class Qs3dUpdateCommands
{
    private const string LatestReleaseUrl = "https://github.com/trinhtanphat/QS3D-AutoCAD/releases/latest";

    [CommandMethod("QS3DUPDATE", CommandFlags.Modal)]
    public void OpenLatestRelease()
    {
        var editor = Application.DocumentManager.MdiActiveDocument?.Editor;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = LatestReleaseUrl,
                UseShellExecute = true
            });
            editor?.WriteMessage("\nOpened the latest QS3D release page. Download the verified Setup.exe there to update QS3D.\n");
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            editor?.WriteMessage($"\nCould not open the QS3D update page: {exception.Message}\nLatest release: {LatestReleaseUrl}\n");
        }
    }
}
