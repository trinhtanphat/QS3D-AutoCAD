using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.Windows;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QS3D.AutoCAD.UI;

internal static class Qs3dPalette
{
    private static readonly Guid PaletteId = new("C31AAE0B-9B83-4A32-95CC-31B26E41B776");
    private static PaletteSet? _palette;

    public static void Show()
    {
        _palette ??= CreatePalette();
        _palette.Visible = true;
    }

    private static PaletteSet CreatePalette()
    {
        var palette = new PaletteSet("QS3D", PaletteId)
        {
            MinimumSize = new Size(240, 360),
            Size = new Size(300, 520)
        };

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(8)
        };

        AddButton(panel, "Initialize Project", "QS3DINIT");
        AddButton(panel, "Level", "QS3DLEVEL");
        AddButton(panel, "Grid", "QS3DGRID");
        AddButton(panel, "Column", "QS3DCOLUMN");
        AddButton(panel, "Beam", "QS3DBEAM");
        AddButton(panel, "Slab", "QS3DSLAB");
        AddButton(panel, "Wall", "QS3DWALL");
        AddButton(panel, "Curtain", "QS3DCURTAIN");
        AddButton(panel, "Section", "QS3DSECTION");
        AddButton(panel, "Quantity Takeoff", "QS3DBOQ");
        AddButton(panel, "About", "QS3DABOUT");

        palette.Add("Tools", panel);
        return palette;
    }

    private static void AddButton(Control parent, string label, string command)
    {
        var button = new Button
        {
            Text = label,
            Width = 248,
            Height = 36,
            Margin = new Padding(4)
        };
        button.Click += (_, _) =>
        {
            var document = AcApplication.DocumentManager.MdiActiveDocument;
            document?.SendStringToExecute(command + " ", true, false, false);
        };
        parent.Controls.Add(button);
    }
}
