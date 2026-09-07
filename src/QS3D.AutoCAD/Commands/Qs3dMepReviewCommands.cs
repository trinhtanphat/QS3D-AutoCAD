using Autodesk.AutoCAD.Runtime;
using QS3D.AutoCAD.UI;

namespace QS3D.AutoCAD.Commands;

public sealed class Qs3dMepReviewCommands
{
    [CommandMethod("QS3DMEPREVIEW")]
    public void ShowMepReview() => MepReviewPalette.Show();
}
