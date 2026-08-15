using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using QS3D.AutoCAD.Infrastructure;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfBorder = System.Windows.Controls.Border;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace QS3D.AutoCAD.UI;

internal sealed class Qs3dWorkspaceControl : WpfUserControl
{
    private const string HomePage = "home";
    private const string ModelPage = "model";
    private const string ReferencesPage = "references";
    private const string QuantitiesPage = "quantities";
    private const string ProjectPage = "project";
    private const string SearchPage = "search";

    private readonly WorkspaceTheme _theme;
    private readonly Dictionary<string, FrameworkElement> _pages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WpfButton> _navButtons = new(StringComparer.Ordinal);
    private readonly Qs3dBrowserControl _browser = new();
    private readonly ContentControl _content = new();
    private readonly WpfTextBox _search = new();
    private readonly StackPanel _searchResults = new();
    private readonly WpfTextBlock _drawingValue = new();
    private readonly WpfTextBlock _elementValue = new();
    private readonly WpfTextBlock _status = new();
    private string _activePage = HomePage;
    private bool _disposed;

    public Qs3dWorkspaceControl()
    {
        _theme = WorkspaceTheme.Detect();
        FontFamily = new FontFamily("Segoe UI");
        Background = _theme.Background;
        Foreground = _theme.Foreground;
        Content = BuildLayout();

        UiText.LanguageChanged += OnLanguageChanged;
        Unloaded += OnUnloaded;
        RefreshLanguage();
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

    private FrameworkElement BuildLayout()
    {
        var root = new Grid
        {
            Background = _theme.Background
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = BuildHeader();
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var searchBar = BuildSearchBar();
        Grid.SetRow(searchBar, 1);
        root.Children.Add(searchBar);

        var body = BuildBody();
        Grid.SetRow(body, 2);
        root.Children.Add(body);

        var footer = BuildFooter();
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        return root;
    }

    private FrameworkElement BuildHeader()
    {
        var border = new WpfBorder
        {
            Background = _theme.Header,
            BorderBrush = _theme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(18, 14, 14, 12)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var identity = new StackPanel();
        identity.Children.Add(new WpfTextBlock
        {
            Text = "QS3D",
            FontSize = 21,
            FontWeight = FontWeights.SemiBold,
            Foreground = _theme.Foreground
        });
        identity.Children.Add(new WpfTextBlock
        {
            Text = "AutoCAD workspace",
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = _theme.Muted
        });
        grid.Children.Add(identity);

        var runtime = new WpfBorder
        {
            Background = _theme.Card,
            BorderBrush = _theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 5, 10, 5),
            VerticalAlignment = VerticalAlignment.Center
        };
        runtime.Child = new WpfTextBlock
        {
            Text = $"CLR {Environment.Version}",
            FontSize = 10.5,
            Foreground = _theme.Muted
        };
        Grid.SetColumn(runtime, 1);
        grid.Children.Add(runtime);

        border.Child = grid;
        return border;
    }

    private FrameworkElement BuildSearchBar()
    {
        var host = new WpfBorder
        {
            Background = _theme.Background,
            Padding = new Thickness(14, 10, 14, 10)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _search.Height = 34;
        _search.Padding = new Thickness(10, 6, 10, 6);
        _search.Background = _theme.Input;
        _search.Foreground = _theme.Foreground;
        _search.BorderBrush = _theme.Border;
        _search.BorderThickness = new Thickness(1);
        _search.VerticalContentAlignment = VerticalAlignment.Center;
        _search.ToolTip = UiText.Get("commandSearchHint");
        _search.TextChanged += (_, _) => ApplySearch();
        grid.Children.Add(_search);

        var language = CreateToolbarButton(() => UiText.Toggle());
        language.Tag = "language";
        language.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(language, 1);
        grid.Children.Add(language);

        host.Child = grid;
        return host;
    }

    private FrameworkElement BuildBody()
    {
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(152) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var nav = new WpfBorder
        {
            Background = _theme.Sidebar,
            BorderBrush = _theme.Border,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(8, 10, 8, 10)
        };
        var navStack = new StackPanel();
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
        _content.Margin = new Thickness(0);
        Grid.SetColumn(_content, 1);
        body.Children.Add(_content);

        return body;
    }

    private FrameworkElement BuildHomePage()
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var stack = new StackPanel { Margin = new Thickness(16) };

        var overviewTitle = CreateSectionTitle("projectSummary");
        stack.Children.Add(overviewTitle);

        var summary = new Grid { Margin = new Thickness(0, 8, 0, 18) };
        summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var drawingCard = CreateMetricCard("activeDrawing", _drawingValue);
        summary.Children.Add(drawingCard);
        var elementCard = CreateMetricCard("elementCount", _elementValue);
        Grid.SetColumn(elementCard, 1);
        elementCard.Margin = new Thickness(8, 0, 0, 0);
        summary.Children.Add(elementCard);
        stack.Children.Add(summary);

        stack.Children.Add(CreateSectionTitle("quickActions"));
        var quick = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var command in Qs3dCommandCatalog.All.Where(item => item.Primary).Take(10))
        {
            quick.Children.Add(CreateCommandCard(command, compact: true));
        }
        stack.Children.Add(quick);

        scroll.Content = stack;
        return scroll;
    }

    private FrameworkElement BuildCommandPage(string titleKey, IEnumerable<Qs3dCommandDescriptor> commands)
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var stack = new StackPanel { Margin = new Thickness(16) };
        stack.Children.Add(CreateSectionTitle(titleKey));
        var wrap = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var command in commands)
        {
            wrap.Children.Add(CreateCommandCard(command));
        }
        stack.Children.Add(wrap);
        scroll.Content = stack;
        return scroll;
    }

    private FrameworkElement BuildProjectPage()
    {
        var grid = new Grid { Margin = new Thickness(12) };
        var host = new WindowsFormsHost
        {
            Child = _browser,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        grid.Children.Add(host);
        return grid;
    }

    private FrameworkElement BuildSearchPage()
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var stack = new StackPanel { Margin = new Thickness(16) };
        stack.Children.Add(CreateSectionTitle("commandSearch"));
        _searchResults.Margin = new Thickness(0, 8, 0, 0);
        stack.Children.Add(_searchResults);
        scroll.Content = stack;
        return scroll;
    }

    private FrameworkElement BuildFooter()
    {
        var border = new WpfBorder
        {
            Background = _theme.Header,
            BorderBrush = _theme.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(14, 7, 14, 7)
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _status.FontSize = 10.5;
        _status.Foreground = _theme.Muted;
        _status.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(_status);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var refresh = CreateToolbarButton(RefreshData);
        refresh.Tag = "refresh";
        actions.Children.Add(refresh);
        var ribbon = CreateToolbarButton(() => Qs3dCommandDispatcher.Execute("QS3DRIBBON"));
        ribbon.Tag = "ribbon";
        ribbon.Margin = new Thickness(6, 0, 0, 0);
        actions.Children.Add(ribbon);
        var about = CreateToolbarButton(() => Qs3dCommandDispatcher.Execute("QS3DABOUT"));
        about.Tag = "about";
        about.Margin = new Thickness(6, 0, 0, 0);
        actions.Children.Add(about);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        border.Child = grid;
        return border;
    }

    private WpfButton CreateNavButton(string page, string labelKey)
    {
        var button = new WpfButton
        {
            Tag = labelKey,
            Height = 38,
            Margin = new Thickness(0, 0, 0, 5),
            Padding = new Thickness(10, 6, 10, 6),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent,
            Foreground = _theme.Foreground,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += (_, _) => ShowPage(page);
        _navButtons[page] = button;
        return button;
    }

    private WpfButton CreateToolbarButton(Action action)
    {
        var button = new WpfButton
        {
            Height = 32,
            Padding = new Thickness(10, 4, 10, 4),
            Background = _theme.Card,
            Foreground = _theme.Foreground,
            BorderBrush = _theme.Border,
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += (_, _) => action();
        return button;
    }

    private FrameworkElement CreateCommandCard(Qs3dCommandDescriptor command, bool compact = false)
    {
        var button = new WpfButton
        {
            Tag = command.LabelKey,
            ToolTip = command.Command,
            Width = compact ? 176 : 218,
            MinHeight = compact ? 58 : 72,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(12, 9, 12, 9),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = _theme.Card,
            Foreground = _theme.Foreground,
            BorderBrush = _theme.Border,
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var content = new StackPanel();
        var title = new WpfTextBlock
        {
            Tag = command.LabelKey,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = _theme.Foreground
        };
        content.Children.Add(title);
        content.Children.Add(new WpfTextBlock
        {
            Text = command.Command,
            FontSize = 9.5,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = _theme.Muted
        });
        button.Content = content;
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
        value.FontWeight = FontWeights.SemiBold;
        value.Foreground = _theme.Foreground;
        value.TextTrimming = TextTrimming.CharacterEllipsis;

        var stack = new StackPanel();
        stack.Children.Add(new WpfTextBlock
        {
            Tag = labelKey,
            FontSize = 10.5,
            Foreground = _theme.Muted
        });
        stack.Children.Add(value);

        return new WpfBorder
        {
            Background = _theme.Card,
            BorderBrush = _theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = stack
        };
    }

    private WpfTextBlock CreateSectionTitle(string labelKey) => new()
    {
        Tag = labelKey,
        FontSize = 15,
        FontWeight = FontWeights.SemiBold,
        Foreground = _theme.Foreground
    };

    private void ShowPage(string page)
    {
        if (!_pages.TryGetValue(page, out var content))
        {
            return;
        }

        _activePage = page;
        _content.Content = content;
        UpdateNavigationState();
    }

    private void ApplySearch()
    {
        var query = _search.Text;
        if (string.IsNullOrWhiteSpace(query))
        {
            ShowPage(_activePage == SearchPage ? HomePage : _activePage);
            return;
        }

        _searchResults.Children.Clear();
        foreach (var command in Qs3dCommandCatalog.Search(query).Take(24))
        {
            _searchResults.Children.Add(CreateCommandCard(command));
        }
        _content.Content = _pages[SearchPage];
        _activePage = SearchPage;
        UpdateNavigationState();
        RefreshLanguage();
    }

    private void UpdateNavigationState()
    {
        foreach (var pair in _navButtons)
        {
            var active = string.Equals(pair.Key, _activePage, StringComparison.Ordinal);
            pair.Value.Background = active ? _theme.AccentSoft : Brushes.Transparent;
            pair.Value.Foreground = active ? _theme.AccentForeground : _theme.Foreground;
            pair.Value.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private void RefreshLanguage()
    {
        _search.ToolTip = UiText.Get("commandSearchHint");

        foreach (var element in EnumerateVisualTree(this))
        {
            if (element is WpfButton button && button.Tag is string buttonKey)
            {
                if (button.Content is StackPanel stack && stack.Children.Count > 0 && stack.Children[0] is WpfTextBlock title && title.Tag is string titleKey)
                {
                    title.Text = UiText.Get(titleKey);
                }
                else
                {
                    button.Content = UiText.Get(buttonKey);
                }
            }
            else if (element is WpfTextBlock text && text.Tag is string textKey)
            {
                text.Text = UiText.Get(textKey);
            }
        }

        RefreshData();
        UpdateNavigationState();
    }

    private static IEnumerable<DependencyObject> EnumerateVisualTree(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in EnumerateVisualTree(child))
            {
                yield return descendant;
            }
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RefreshLanguage();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UiText.LanguageChanged -= OnLanguageChanged;
    }

    private sealed class WorkspaceTheme
    {
        public required Brush Background { get; init; }
        public required Brush Header { get; init; }
        public required Brush Sidebar { get; init; }
        public required Brush Card { get; init; }
        public required Brush Input { get; init; }
        public required Brush Border { get; init; }
        public required Brush Foreground { get; init; }
        public required Brush Muted { get; init; }
        public required Brush AccentSoft { get; init; }
        public required Brush AccentForeground { get; init; }

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
                ? new WorkspaceTheme
                {
                    Background = Brush("#202225"),
                    Header = Brush("#181A1D"),
                    Sidebar = Brush("#1B1D20"),
                    Card = Brush("#2A2D31"),
                    Input = Brush("#25282C"),
                    Border = Brush("#3A3E44"),
                    Foreground = Brush("#F3F4F6"),
                    Muted = Brush("#A9AFB8"),
                    AccentSoft = Brush("#173F5F"),
                    AccentForeground = Brush("#8DCAFF")
                }
                : new WorkspaceTheme
                {
                    Background = Brush("#F3F5F7"),
                    Header = Brush("#FFFFFF"),
                    Sidebar = Brush("#F8F9FA"),
                    Card = Brush("#FFFFFF"),
                    Input = Brush("#FFFFFF"),
                    Border = Brush("#D6DADE"),
                    Foreground = Brush("#202327"),
                    Muted = Brush("#68707A"),
                    AccentSoft = Brush("#E2F1FF"),
                    AccentForeground = Brush("#0B5E9A")
                };
        }

        private static Brush Brush(string value)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
            brush.Freeze();
            return brush;
        }
    }
}
