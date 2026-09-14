using Autodesk.AutoCAD.Runtime;
using QS3D.AutoCAD.UI;

[assembly: CommandClass(typeof(QS3D.AutoCAD.Commands.Qs3dMepReviewCommands))]

namespace QS3D.AutoCAD.Commands;

public sealed class Qs3dMepReviewCommands
{
    [CommandMethod("QS3DMEPREVIEW")]
    public void ShowMepReview() => MepReviewPalette.Show();
}
