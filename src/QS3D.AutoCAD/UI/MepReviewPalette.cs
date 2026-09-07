using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Autodesk.AutoCAD.Windows;

namespace QS3D.AutoCAD.UI;

internal static class MepReviewPalette
{
    private static readonly Guid PaletteId = new("C3902606-699E-4438-B3D4-F739E2474BB7");
    private static PaletteSet? _palette;
    private static ElementHost? _host;
    private static MepReviewControl? _control;

    internal static void Show()
    {
        _palette ??= CreatePalette();
        _control?.RefreshProfile();
        _palette.Visible = true;
    }

    internal static void RefreshProfile() => _control?.RefreshProfile();

    private static PaletteSet CreatePalette()
    {
        var palette = new PaletteSet("QS3D MEP Review", PaletteId)
        {
            MinimumSize = new Size(420, 520),
            Size = new Size(620, 760)
        };
        _control = new MepReviewControl();
        _host = new ElementHost
        {
            Dock = DockStyle.Fill,
            Child = _control
        };
        palette.Add("MEP Review", _host);
        return palette;
    }
}
