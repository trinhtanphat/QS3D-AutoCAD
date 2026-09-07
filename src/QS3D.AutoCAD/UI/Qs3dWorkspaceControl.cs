using QS3D.AutoCAD.Infrastructure;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using WpfBorder = System.Windows.Controls.Border;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfContentControl = System.Windows.Controls.ContentControl;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfGrid = System.Windows.Controls.Grid;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfScrollViewer = System.Windows.Controls.ScrollViewer;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfUserControl = System.Windows.Controls.UserControl;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;
using WpfWindowsFormsHost = System.Windows.Forms.Integration.WindowsFormsHost;
using WpfWrapPanel = System.Windows.Controls.WrapPanel;

namespace QS3D.AutoCAD.UI;

internal sealed class Qs3dWorkspaceControl : WpfUserControl
{
    private const string HomePage = "home";
    private const string ModelPage = "model";
    private const string ReferencesPage = "references";
    private const string QuantitiesPage = "quantities";
    private const string ProjectPage = "project";
    private const string SearchPage = "search";

    private readonly WorkspaceTheme _theme = WorkspaceTheme.Detect();
    private readonly Dictionary<string, System.Windows.FrameworkElement> _pages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WpfButton> _navButtons = new(StringComparer.Ordinal);
    private readonly List<WpfButton> _localizedButtons = [];
    private readonly List<WpfTextBlock> _localizedText = [];
    private readonly Qs3dBrowserControl _browser = new();
    private readonly WpfContentControl _content = new();
    private readonly WpfTextBox _search = new();
    private readonly WpfStackPanel _searchResults = new();
    private readonly WpfTextBlock _drawingValue = new();
    private readonly WpfTextBlock _elementValue = new();
    private readonly WpfTextBlock _status = new();
    private string _activePage = HomePage;

    public Qs3dWorkspaceControl()
    {
        FontFamily = new WpfFontFamily("Segoe UI");
        Background = _theme.Background;
        Foreground = _theme.Foreground;
        Content = BuildLayout();
        UiText.LanguageChanged += OnLanguageChanged;
        ApplyLanguage();
        RefreshData();
    }

    public void RefreshData()
    {
        _browser.RefreshData();
        var document = AcApplication.DocumentManager.MdiActiveDocument;
        _drawingValue.Text = document is null ? "—" : System.IO.Path.GetFileName(document.Name);

        var count = 0;
        if (document is not null)
        {
            try
            {
                count = Qs3dDocumentIndex.Scan(document.Database).Count;
            }
            catch
            {
                count = 0;
            }
        }

        _elementValue.Text = count.ToString(System.Globalization.CultureInfo.CurrentCulture);
        _status.Text = UiText.Get("statusReady");
    }

    private System.Windows.FrameworkElement BuildLayout()
    {
        var root = new WpfGrid { Background = _theme.Background };
        root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

        AddRow(root, BuildHeader(), 0);
        AddRow(root, BuildSearchBar(), 1);
        AddRow(root, BuildBody(), 2);
        AddRow(root, BuildFooter(), 3);
        return root;
    }

    private System.Windows.FrameworkElement BuildHeader()
    {
        var border = new WpfBorder
        {
            Background = _theme.Header,
            BorderBrush = _theme.Border,
            BorderThickness = new System.Windows.Thickness(0, 0, 0, 1),
            Padding = new System.Windows.Thickness(18, 14, 14, 12)
        };
        var grid = new WpfGrid();
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });

        var identity = new WpfStackPanel();
        identity.Children.Add(new WpfTextBlock
        {
            Text = "QS3D",
            FontSize = 21,
            FontWeight = System.Windows.FontWeights.SemiBold,
            Foreground = _theme.Foreground
        });
        identity.Children.Add(new WpfTextBlock
        {
            Text = "AutoCAD workspace",
            FontSize = 11,
            Margin = new System.Windows.Thickness(0, 2, 0, 0),
            Foreground = _theme.Muted
        });
        grid.Children.Add(identity);

        var runtime = new WpfBorder
        {
            Background = _theme.Card,
            BorderBrush = _theme.Border,
            BorderThickness = new System.Windows.Thickness(1),
            CornerRadius = new System.Windows.CornerRadius(12),
            Padding = new System.Windows.Thickness(10, 5, 10, 5),
            VerticalAlignment = WpfVerticalAlignment.Center,
            Child = new WpfTextBlock
            {
                Text = $"CLR {Environment.Version}",
                FontSize = 10.5,
                Foreground = _theme.Muted
            }
        };
        WpfGrid.SetColumn(runtime, 1);
        grid.Children.Add(runtime);
        border.Child = grid;
        return border;
    }

    private System.Windows.FrameworkElement BuildSearchBar()
    {
        var host = new WpfBorder
        {
            Background = _theme.Background,
            Padding = new System.Windows.Thickness(14, 10, 14, 10)
        };
        var grid = new WpfGrid();
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });

        _search.Height = 34;
        _search.Padding = new System.Windows.Thickness(10, 6, 10, 6);
        _search.Background = _theme.Input;
        _search.Foreground = _theme.Foreground;
        _search.BorderBrush = _theme.Border;
        _search.BorderThickness = new System.Windows.Thickness(1);
        _search.VerticalContentAlignment = WpfVerticalAlignment.Center;
        _search.TextChanged += (_, _) => ApplySearch();
        grid.Children.Add(_search);

        var language = CreateToolbarButton("language", UiText.Toggle);
        language.Margin = new System.Windows.Thickness(8, 0, 0, 0);
        WpfGrid.SetColumn(language, 1);
        grid.Children.Add(language);
        host.Child = grid;
        return host;
    }

    private System.Windows.FrameworkElement BuildBody()
    {
        var body = new WpfGrid();
        body.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(152) });
        body.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

        var nav = new WpfBorder
        {
            Background = _theme.Sidebar,
            BorderBrush = _theme.Border,
            BorderThickness = new System.Windows.Thickness(0, 0, 1, 0),
            Padding = new System.Windows.Thickness(8, 10, 8, 10)
        };
        var navStack = new WpfStackPanel();
        navStack.Children.Add(CreateNavButton(HomePage, "home"));
        navStack.Children.Add(CreateNavButton(ModelPage, "model"));
        navStack.Children.Add(CreateNavButton(ReferencesPage, "references"));
        navStack.Children.Add(CreateNavButton(QuantitiesPage, "quantities"));
        navStack.Children.Add(CreateNavButton(ProjectPage, "project"));
        nav.Child = navStack;
        body.Children.Add(nav);

        _pages[HomePage] = BuildHomePage();
        _pages[ModelPage] = BuildCommandPage("liveTools", Qs3dCommandCatalog.InSection(Qs3dCommandCatalog.SectionModel));
        _pages[ReferencesPage] = BuildCommandPage("referencesTab", Qs3dCommandCatalog.InSection(Qs3dCommandCatalog.SectionReferences));
        _pages[QuantitiesPage] = BuildCommandPage("quantities", Qs3dCommandCatalog.InSection(Qs3dCommandCatalog.SectionReview));
        _pages[ProjectPage] = BuildProjectPage();
        _pages[SearchPage] = BuildSearchPage();

        _content.Content = _pages[HomePage];
        WpfGrid.SetColumn(_content, 1);
        body.Children.Add(_content);
        return body;
    }

    private System.Windows.FrameworkElement BuildHomePage()
    {
        var stack = new WpfStackPanel { Margin = new System.Windows.Thickness(16) };
        stack.Children.Add(CreateSectionTitle("projectSummary"));

        var summary = new WpfGrid { Margin = new System.Windows.Thickness(0, 8, 0, 18) };
        summary.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        summary.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        summary.Children.Add(CreateMetricCard("activeDrawing", _drawingValue));
        var elementCard = CreateMetricCard("elementCount", _elementValue);
        elementCard.Margin = new System.Windows.Thickness(8, 0, 0, 0);
        WpfGrid.SetColumn(elementCard, 1);
        summary.Children.Add(elementCard);
        stack.Children.Add(summary);

        stack.Children.Add(CreateSectionTitle("quickActions"));
        var quick = new WpfWrapPanel { Margin = new System.Windows.Thickness(0, 8, 0, 0) };
        foreach (var command in Qs3dCommandCatalog.All.Where(item => item.Primary).Take(10))
        {
            quick.Children.Add(CreateCommandCard(command, true));
        }
        stack.Children.Add(quick);
        return Scroll(stack);
    }

    private System.Windows.FrameworkElement BuildCommandPage(string titleKey, IEnumerable<Qs3dCommandDescriptor> commands)
    {
        var stack = new WpfStackPanel { Margin = new System.Windows.Thickness(16) };
        stack.Children.Add(CreateSectionTitle(titleKey));
        var wrap = new WpfWrapPanel { Margin = new System.Windows.Thickness(0, 8, 0, 0) };
        foreach (var command in commands)
        {
            wrap.Children.Add(CreateCommandCard(command, false));
        }
        stack.Children.Add(wrap);
        return Scroll(stack);
    }

    private System.Windows.FrameworkElement BuildProjectPage()
    {
        var grid = new WpfGrid { Margin = new System.Windows.Thickness(12) };
        grid.Children.Add(new WpfWindowsFormsHost
        {
            Child = _browser,
            HorizontalAlignment = WpfHorizontalAlignment.Stretch,
            VerticalAlignment = WpfVerticalAlignment.Stretch
        });
        return grid;
    }

    private System.Windows.FrameworkElement BuildSearchPage()
    {
        var stack = new WpfStackPanel { Margin = new System.Windows.Thickness(16) };
        stack.Children.Add(CreateSectionTitle("commandSearch"));
        _searchResults.Margin = new System.Windows.Thickness(0, 8, 0, 0);
        stack.Children.Add(_searchResults);
        return Scroll(stack);
    }

    private System.Windows.FrameworkElement BuildFooter()
    {
        var border = new WpfBorder
        {
            Background = _theme.Header,
            BorderBrush = _theme.Border,
            BorderThickness = new System.Windows.Thickness(0, 1, 0, 0),
            Padding = new System.Windows.Thickness(14, 7, 14, 7)
        };
        var grid = new WpfGrid();
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });
        _status.FontSize = 10.5;
        _status.Foreground = _theme.Muted;
        _status.VerticalAlignment = WpfVerticalAlignment.Center;
        grid.Children.Add(_status);

        var actions = new WpfStackPanel { Orientation = WpfOrientation.Horizontal };
        actions.Children.Add(CreateToolbarButton("refresh", RefreshData));
        var ribbon = CreateToolbarButton("ribbon", () => Qs3dCommandDispatcher.Execute("QS3DRIBBON"));
        ribbon.Margin = new System.Windows.Thickness(6, 0, 0, 0);
        actions.Children.Add(ribbon);
        var about = CreateToolbarButton("about", () => Qs3dCommandDispatcher.Execute("QS3DABOUT"));
        about.Margin = new System.Windows.Thickness(6, 0, 0, 0);
        actions.Children.Add(about);
        WpfGrid.SetColumn(actions, 1);
        grid.Children.Add(actions);
        border.Child = grid;
        return border;
    }

    private WpfButton CreateNavButton(string page, string labelKey)
    {
        var button = new WpfButton
        {
            Tag = labelKey,
            Content = UiText.Get(labelKey),
            Height = 38,
            Margin = new System.Windows.Thickness(0, 0, 0, 5),
            Padding = new System.Windows.Thickness(10, 6, 10, 6),
            HorizontalContentAlignment = WpfHorizontalAlignment.Left,
            Background = WpfBrushes.Transparent,
            Foreground = _theme.Foreground,
            BorderBrush = WpfBrushes.Transparent,
            BorderThickness = new System.Windows.Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += (_, _) => ShowPage(page);
        _localizedButtons.Add(button);
        _navButtons[page] = button;
        return button;
    }

    private WpfButton CreateToolbarButton(string labelKey, Action action)
    {
        var button = new WpfButton
        {
            Tag = labelKey,
            Content = UiText.Get(labelKey),
            Height = 32,
            Padding = new System.Windows.Thickness(10, 4, 10, 4),
            Background = _theme.Card,
            Foreground = _theme.Foreground,
            BorderBrush = _theme.Border,
            BorderThickness = new System.Windows.Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += (_, _) => action();
        _localizedButtons.Add(button);
        return button;
    }

    private System.Windows.FrameworkElement CreateCommandCard(Qs3dCommandDescriptor command, bool compact)
    {
        var title = new WpfTextBlock
        {
            Tag = command.LabelKey,
            Text = UiText.Get(command.LabelKey),
            FontWeight = System.Windows.FontWeights.SemiBold,
            TextWrapping = System.Windows.TextWrapping.Wrap,
            Foreground = _theme.Foreground
        };
        _localizedText.Add(title);

        var content = new WpfStackPanel();
        content.Children.Add(title);
        content.Children.Add(new WpfTextBlock
        {
            Text = command.Command,
            FontSize = 9.5,
            Margin = new System.Windows.Thickness(0, 4, 0, 0),
            Foreground = _theme.Muted
        });

        var button = new WpfButton
        {
            ToolTip = command.Command,
            Width = compact ? 176 : 218,
            MinHeight = compact ? 58 : 72,
            Margin = new System.Windows.Thickness(0, 0, 8, 8),
            Padding = new System.Windows.Thickness(12, 9, 12, 9),
            HorizontalContentAlignment = WpfHorizontalAlignment.Left,
            Background = _theme.Card,
            Foreground = _theme.Foreground,
            BorderBrush = _theme.Border,
            BorderThickness = new System.Windows.Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand,
            Content = content
        };
        button.Click += (_, _) =>
        {
            _status.Text = command.Command;
            Qs3dCommandDispatcher.Execute(command.Command);
        };
        return button;
    }

    private WpfBorder CreateMetricCard(string labelKey, WpfTextBlock value)
    {
        value.FontSize = 16;
        value.FontWeight = System.Windows.FontWeights.SemiBold;
        value.Foreground = _theme.Foreground;
        value.TextTrimming = System.Windows.TextTrimming.CharacterEllipsis;
        var label = new WpfTextBlock
        {
            Tag = labelKey,
            Text = UiText.Get(labelKey),
            FontSize = 10.5,
            Foreground = _theme.Muted
        };
        _localizedText.Add(label);
        var stack = new WpfStackPanel();
        stack.Children.Add(label);
        stack.Children.Add(value);
        return new WpfBorder
        {
            Background = _theme.Card,
            BorderBrush = _theme.Border,
            BorderThickness = new System.Windows.Thickness(1),
            CornerRadius = new System.Windows.CornerRadius(8),
            Padding = new System.Windows.Thickness(12),
            Child = stack
        };
    }

    private WpfTextBlock CreateSectionTitle(string labelKey)
    {
        var text = new WpfTextBlock
        {
            Tag = labelKey,
            Text = UiText.Get(labelKey),
            FontSize = 15,
            FontWeight = System.Windows.FontWeights.SemiBold,
            Foreground = _theme.Foreground
        };
        _localizedText.Add(text);
        return text;
    }

    private void ShowPage(string page)
    {
        if (!_pages.TryGetValue(page, out var pageContent)) return;
        _activePage = page;
        _content.Content = pageContent;
        UpdateNavigationState();
    }

    private void ApplySearch()
    {
        if (string.IsNullOrWhiteSpace(_search.Text))
        {
            ShowPage(HomePage);
            return;
        }

        _searchResults.Children.Clear();
        foreach (var command in Qs3dCommandCatalog.Search(_search.Text).Take(24))
        {
            _searchResults.Children.Add(CreateCommandCard(command, false));
        }
        _activePage = SearchPage;
        _content.Content = _pages[SearchPage];
        UpdateNavigationState();
    }

    private void UpdateNavigationState()
    {
        foreach (var pair in _navButtons)
        {
            var active = string.Equals(pair.Key, _activePage, StringComparison.Ordinal);
            pair.Value.Background = active ? _theme.AccentSoft : WpfBrushes.Transparent;
            pair.Value.Foreground = active ? _theme.AccentForeground : _theme.Foreground;
            pair.Value.FontWeight = active ? System.Windows.FontWeights.SemiBold : System.Windows.FontWeights.Normal;
        }
    }

    private void ApplyLanguage()
    {
        _search.ToolTip = UiText.Get("commandSearchHint");
        foreach (var button in _localizedButtons)
        {
            if (button.Tag is string key) button.Content = UiText.Get(key);
        }
        foreach (var text in _localizedText)
        {
            if (text.Tag is string key) text.Text = UiText.Get(key);
        }
        _browser.RefreshData();
        _status.Text = UiText.Get("statusReady");
        UpdateNavigationState();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => ApplyLanguage();

    private static WpfScrollViewer Scroll(System.Windows.UIElement content) => new()
    {
        Content = content,
        VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled
    };

    private static void AddRow(WpfGrid grid, System.Windows.UIElement child, int row)
    {
        WpfGrid.SetRow(child, row);
        grid.Children.Add(child);
    }

    private sealed class WorkspaceTheme
    {
        private WorkspaceTheme(
            WpfBrush background,
            WpfBrush header,
            WpfBrush sidebar,
            WpfBrush card,
            WpfBrush input,
            WpfBrush border,
            WpfBrush foreground,
            WpfBrush muted,
            WpfBrush accentSoft,
            WpfBrush accentForeground)
        {
            Background = background;
            Header = header;
            Sidebar = sidebar;
            Card = card;
            Input = input;
            Border = border;
            Foreground = foreground;
            Muted = muted;
            AccentSoft = accentSoft;
            AccentForeground = accentForeground;
        }

        public WpfBrush Background { get; }
        public WpfBrush Header { get; }
        public WpfBrush Sidebar { get; }
        public WpfBrush Card { get; }
        public WpfBrush Input { get; }
        public WpfBrush Border { get; }
        public WpfBrush Foreground { get; }
        public WpfBrush Muted { get; }
        public WpfBrush AccentSoft { get; }
        public WpfBrush AccentForeground { get; }

        public static WorkspaceTheme Detect()
        {
            var dark = true;
            try
            {
                var value = AcApplication.GetSystemVariable("COLORTHEME");
                dark = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) != 0;
            }
            catch
            {
                dark = true;
            }

            return dark
                ? new WorkspaceTheme(
                    MakeBrush("#202225"), MakeBrush("#181A1D"), MakeBrush("#1B1D20"), MakeBrush("#2A2D31"),
                    MakeBrush("#25282C"), MakeBrush("#3A3E44"), MakeBrush("#F3F4F6"), MakeBrush("#A9AFB8"),
                    MakeBrush("#173F5F"), MakeBrush("#8DCAFF"))
                : new WorkspaceTheme(
                    MakeBrush("#F3F5F7"), MakeBrush("#FFFFFF"), MakeBrush("#F8F9FA"), MakeBrush("#FFFFFF"),
                    MakeBrush("#FFFFFF"), MakeBrush("#D6DADE"), MakeBrush("#202327"), MakeBrush("#68707A"),
                    MakeBrush("#E2F1FF"), MakeBrush("#0B5E9A"));
        }

        private static WpfBrush MakeBrush(string value)
        {
            var brush = (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFromString(value)!;
            brush.Freeze();
            return brush;
        }
    }
}
