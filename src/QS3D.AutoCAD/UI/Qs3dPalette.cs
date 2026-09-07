using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Autodesk.AutoCAD.Windows;

namespace QS3D.AutoCAD.UI;

internal static class Qs3dPalette
{
    private static readonly Guid PaletteId = new("C31AAE0B-9B83-4A32-95CC-31B26E41B776");
    private static PaletteSet? _palette;
    private static ElementHost? _host;
    private static Qs3dWorkspaceControl? _workspace;

    public static void Show()
    {
        _palette ??= CreatePalette();
        _workspace?.RefreshData();
        _palette.Visible = true;
    }

    public static void RefreshBrowser() => _workspace?.RefreshData();

    private static PaletteSet CreatePalette()
    {
        var palette = new PaletteSet("QS3D", PaletteId)
        {
            MinimumSize = new Size(380, 480),
            Size = new Size(520, 720)
        };

        _workspace = new Qs3dWorkspaceControl();
        _host = new ElementHost
        {
            Dock = DockStyle.Fill,
            Child = _workspace
        };

        palette.Add("QS3D", _host);
        return palette;
    }
}