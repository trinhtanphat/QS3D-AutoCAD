using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.Windows;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QS3D.AutoCAD.UI;

internal static class Qs3dPalette
{
    private static readonly Guid PaletteId = new("C31AAE0B-9B83-4A32-95CC-31B26E41B776");
    private static PaletteSet? _palette;
    private static FlowLayoutPanel? _toolsPanel;
    private static Qs3dBrowserControl? _browser;

    public static void Show()
    {
        _palette ??= CreatePalette();
        _browser?.RefreshData();
        _palette.Visible = true;
    }

    public static void RefreshBrowser() => _browser?.RefreshData();

    private static PaletteSet CreatePalette()
    {
        var palette = new PaletteSet("QS3D", PaletteId)
        {
            MinimumSize = new Size(280, 420),
            Size = new Size(360, 620)
        };

        _toolsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(8)
        };

        AddCommandButton(_toolsPanel, "init", "QS3DINIT");
        AddCommandButton(_toolsPanel, "level", "QS3DLEVEL");
        AddCommandButton(_toolsPanel, "grid", "QS3DGRID");
        AddCommandButton(_toolsPanel, "column", "QS3DCOLUMN");
        AddCommandButton(_toolsPanel, "beam", "QS3DBEAM");
        AddCommandButton(_toolsPanel, "slab", "QS3DSLAB");
        AddCommandButton(_toolsPanel, "wall", "QS3DWALL");
        AddCommandButton(_toolsPanel, "curtain", "QS3DCURTAIN");
        AddCommandButton(_toolsPanel, "section", "QS3DSECTION");
        AddCommandButton(_toolsPanel, "boq", "QS3DBOQ");
        AddCommandButton(_toolsPanel, "about", "QS3DABOUT");

        _browser = new Qs3dBrowserControl();
        palette.Add(UiText.Get("tools"), _toolsPanel);
        palette.Add(UiText.Get("project"), _browser);

        UiText.LanguageChanged += (_, _) => ApplyToolLanguage();
        ApplyToolLanguage();
        return palette;
    }

    private static void AddCommandButton(Control parent, string textKey, string command)
    {
        var button = new Button
        {
            Tag = textKey,
            Width = 300,
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

    private static void ApplyToolLanguage()
    {
        if (_toolsPanel is null)
        {
            return;
        }

        foreach (Control control in _toolsPanel.Controls)
        {
            if (control is Button button && button.Tag is string key)
            {
                button.Text = UiText.Get(key);
            }
        }
    }
}
