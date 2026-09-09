using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using DrawingColor = System.Drawing.Color;
using WpfBorder = System.Windows.Controls.Border;
using WpfBrush = System.Windows.Media.Brush;
using WpfControl = System.Windows.Controls.Control;
using WpfPanel = System.Windows.Controls.Panel;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace QS3D.AutoCAD.UI;

internal enum Qs3dThemeMode
{
    System,
    Light,
    Dark
}

internal enum Qs3dResolvedTheme
{
    Light,
    Dark
}

internal sealed class Qs3dThemeChangedEventArgs : EventArgs
{
    internal Qs3dThemeChangedEventArgs(Qs3dThemeMode mode, Qs3dThemePalette palette)
    {
        Mode = mode;
        Palette = palette;
    }

    internal Qs3dThemeMode Mode { get; }
    internal Qs3dThemePalette Palette { get; }
}

internal sealed class Qs3dThemePalette
{
    private Qs3dThemePalette(
        Qs3dResolvedTheme resolved,
        string background,
        string header,
        string sidebar,
        string card,
        string cardHover,
        string input,
        string border,
        string foreground,
        string muted,
        string accent,
        string accentSoft,
        string accentForeground,
        string selection,
        string danger,
        string success)
    {
        Resolved = resolved;
        Background = Brush(background);
        Header = Brush(header);
        Sidebar = Brush(sidebar);
        Card = Brush(card);
        CardHover = Brush(cardHover);
        Input = Brush(input);
        Border = Brush(border);
        Foreground = Brush(foreground);
        Muted = Brush(muted);
        Accent = Brush(accent);
        AccentSoft = Brush(accentSoft);
        AccentForeground = Brush(accentForeground);
        Selection = Brush(selection);
        Danger = Brush(danger);
        Success = Brush(success);

        BackgroundColor = DrawingColorTranslator(background);
        HeaderColor = DrawingColorTranslator(header);
        SidebarColor = DrawingColorTranslator(sidebar);
        CardColor = DrawingColorTranslator(card);
        CardHoverColor = DrawingColorTranslator(cardHover);
        InputColor = DrawingColorTranslator(input);
        BorderColor = DrawingColorTranslator(border);
        ForegroundColor = DrawingColorTranslator(foreground);
        MutedColor = DrawingColorTranslator(muted);
        AccentColor = DrawingColorTranslator(accent);
        AccentSoftColor = DrawingColorTranslator(accentSoft);
        AccentForegroundColor = DrawingColorTranslator(accentForeground);
        SelectionColor = DrawingColorTranslator(selection);
        DangerColor = DrawingColorTranslator(danger);
        SuccessColor = DrawingColorTranslator(success);
    }

    internal static Qs3dThemePalette Dark { get; } = new(
        Qs3dResolvedTheme.Dark,
        "#1B1D21", "#15171A", "#181A1E", "#25282D", "#2D3137", "#202329", "#3A3F47",
        "#F3F5F7", "#A7AFBA", "#4DA3E6", "#163F61", "#9AD2FF", "#244C68", "#E57373", "#69C889");

    internal static Qs3dThemePalette Light { get; } = new(
        Qs3dResolvedTheme.Light,
        "#F4F6F8", "#FFFFFF", "#F8F9FB", "#FFFFFF", "#F1F5F9", "#FFFFFF", "#D5DAE0",
        "#20242A", "#68717D", "#1677B8", "#E3F2FD", "#0A5D95", "#D8EBF8", "#B42318", "#138A4A");

    internal Qs3dResolvedTheme Resolved { get; }
    internal bool IsDark => Resolved == Qs3dResolvedTheme.Dark;

    internal WpfBrush Background { get; }
    internal WpfBrush Header { get; }
    internal WpfBrush Sidebar { get; }
    internal WpfBrush Card { get; }
    internal WpfBrush CardHover { get; }
    internal WpfBrush Input { get; }
    internal WpfBrush Border { get; }
    internal WpfBrush Foreground { get; }
    internal WpfBrush Muted { get; }
    internal WpfBrush Accent { get; }
    internal WpfBrush AccentSoft { get; }
    internal WpfBrush AccentForeground { get; }
    internal WpfBrush Selection { get; }
    internal WpfBrush Danger { get; }
    internal WpfBrush Success { get; }

    internal DrawingColor BackgroundColor { get; }
    internal DrawingColor HeaderColor { get; }
    internal DrawingColor SidebarColor { get; }
    internal DrawingColor CardColor { get; }
    internal DrawingColor CardHoverColor { get; }
    internal DrawingColor InputColor { get; }
    internal DrawingColor BorderColor { get; }
    internal DrawingColor ForegroundColor { get; }
    internal DrawingColor MutedColor { get; }
    internal DrawingColor AccentColor { get; }
    internal DrawingColor AccentSoftColor { get; }
    internal DrawingColor AccentForegroundColor { get; }
    internal DrawingColor SelectionColor { get; }
    internal DrawingColor DangerColor { get; }
    internal DrawingColor SuccessColor { get; }

    private static WpfBrush Brush(string value)
    {
        var brush = (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFromString(value)!;
        brush.Freeze();
        return brush;
    }

    private static DrawingColor DrawingColorTranslator(string value) =>
        System.Drawing.ColorTranslator.FromHtml(value);
}

internal static class Qs3dThemeManager
{
    private static readonly object Gate = new();
    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QS3D",
        "ui-theme.txt");

    private static Qs3dThemeMode _mode;
    private static Qs3dResolvedTheme _resolved;

    static Qs3dThemeManager()
    {
        _mode = LoadPreference();
        _resolved = Resolve(_mode);
        AcApplication.SystemVariableChanged += new SystemVariableChangedEventHandler(OnSystemVariableChanged);
    }

    internal static event EventHandler<Qs3dThemeChangedEventArgs>? ThemeChanged;

    internal static Qs3dThemeMode Mode
    {
        get
        {
            lock (Gate) return _mode;
        }
    }

    internal static Qs3dThemePalette Current
    {
        get
        {
            lock (Gate) return Palette(_resolved);
        }
    }

    internal static string? LastPersistenceError { get; private set; }

    internal static void SetMode(Qs3dThemeMode mode)
    {
        if (!Enum.IsDefined(typeof(Qs3dThemeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));

        Qs3dThemePalette palette;
        lock (Gate)
        {
            if (_mode == mode) return;
            _mode = mode;
            _resolved = Resolve(mode);
            palette = Palette(_resolved);
        }

        TrySavePreference(mode);
        ThemeChanged?.Invoke(null, new Qs3dThemeChangedEventArgs(mode, palette));
    }

    internal static Qs3dResolvedTheme ResolveHostTheme(int colorTheme) =>
        colorTheme == 0 ? Qs3dResolvedTheme.Dark : Qs3dResolvedTheme.Light;

    private static Qs3dResolvedTheme Resolve(Qs3dThemeMode mode)
    {
        if (mode == Qs3dThemeMode.Dark) return Qs3dResolvedTheme.Dark;
        if (mode == Qs3dThemeMode.Light) return Qs3dResolvedTheme.Light;

        try
        {
            var value = AcApplication.GetSystemVariable("COLORTHEME");
            var colorTheme = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            return ResolveHostTheme(colorTheme);
        }
        catch
        {
            return Qs3dResolvedTheme.Dark;
        }
    }

    private static void OnSystemVariableChanged(object sender, SystemVariableChangedEventArgs args)
    {
        if (!string.Equals(args.Name, "COLORTHEME", StringComparison.OrdinalIgnoreCase)) return;

        Qs3dThemePalette? palette = null;
        Qs3dThemeMode mode;
        lock (Gate)
        {
            mode = _mode;
            if (mode != Qs3dThemeMode.System) return;
            var next = Resolve(Qs3dThemeMode.System);
            if (next == _resolved) return;
            _resolved = next;
            palette = Palette(next);
        }

        ThemeChanged?.Invoke(null, new Qs3dThemeChangedEventArgs(mode, palette));
    }

    private static Qs3dThemePalette Palette(Qs3dResolvedTheme resolved) =>
        resolved == Qs3dResolvedTheme.Dark ? Qs3dThemePalette.Dark : Qs3dThemePalette.Light;

    private static Qs3dThemeMode LoadPreference()
    {
        try
        {
            if (!File.Exists(PreferencePath)) return Qs3dThemeMode.System;
            var info = new FileInfo(PreferencePath);
            if (info.Length <= 0 || info.Length > 64) return Qs3dThemeMode.System;
            var value = File.ReadAllText(PreferencePath).Trim();
            return Enum.TryParse(value, true, out Qs3dThemeMode parsed) && Enum.IsDefined(typeof(Qs3dThemeMode), parsed)
                ? parsed
                : Qs3dThemeMode.System;
        }
        catch
        {
            return Qs3dThemeMode.System;
        }
    }

    private static void TrySavePreference(Qs3dThemeMode mode)
    {
        try
        {
            var directory = Path.GetDirectoryName(PreferencePath) ?? throw new InvalidOperationException("QS3D theme preference directory is unavailable.");
            Directory.CreateDirectory(directory);
            var tempPath = Path.Combine(directory, ".ui-theme-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(tempPath, mode.ToString(), new UTF8Encoding(false));
                if (File.Exists(PreferencePath))
                    File.Replace(tempPath, PreferencePath, null);
                else
                    File.Move(tempPath, PreferencePath);
                LastPersistenceError = null;
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            LastPersistenceError = exception.Message;
        }
    }
}

internal static class Qs3dThemeStyler
{
    internal static void SwapWpfTheme(System.Windows.DependencyObject root, Qs3dThemePalette previous, Qs3dThemePalette current)
    {
        ApplyOne(root, previous, current);
        var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
            SwapWpfTheme(System.Windows.Media.VisualTreeHelper.GetChild(root, index), previous, current);
    }

    private static void ApplyOne(System.Windows.DependencyObject item, Qs3dThemePalette previous, Qs3dThemePalette current)
    {
        if (item is WpfControl control)
        {
            control.Background = Swap(control.Background, previous, current);
            control.Foreground = Swap(control.Foreground, previous, current);
            control.BorderBrush = Swap(control.BorderBrush, previous, current);
        }
        else if (item is WpfPanel panel)
        {
            panel.Background = Swap(panel.Background, previous, current);
        }
        else if (item is WpfBorder border)
        {
            border.Background = Swap(border.Background, previous, current);
            border.BorderBrush = Swap(border.BorderBrush, previous, current);
        }
        else if (item is WpfTextBlock text)
        {
            text.Foreground = Swap(text.Foreground, previous, current);
        }
    }

    private static WpfBrush? Swap(WpfBrush? brush, Qs3dThemePalette previous, Qs3dThemePalette current)
    {
        if (brush is null) return null;
        if (ReferenceEquals(brush, previous.Background)) return current.Background;
        if (ReferenceEquals(brush, previous.Header)) return current.Header;
        if (ReferenceEquals(brush, previous.Sidebar)) return current.Sidebar;
        if (ReferenceEquals(brush, previous.Card)) return current.Card;
        if (ReferenceEquals(brush, previous.CardHover)) return current.CardHover;
        if (ReferenceEquals(brush, previous.Input)) return current.Input;
        if (ReferenceEquals(brush, previous.Border)) return current.Border;
        if (ReferenceEquals(brush, previous.Foreground)) return current.Foreground;
        if (ReferenceEquals(brush, previous.Muted)) return current.Muted;
        if (ReferenceEquals(brush, previous.Accent)) return current.Accent;
        if (ReferenceEquals(brush, previous.AccentSoft)) return current.AccentSoft;
        if (ReferenceEquals(brush, previous.AccentForeground)) return current.AccentForeground;
        if (ReferenceEquals(brush, previous.Selection)) return current.Selection;
        if (ReferenceEquals(brush, previous.Danger)) return current.Danger;
        if (ReferenceEquals(brush, previous.Success)) return current.Success;
        return brush;
    }
}
