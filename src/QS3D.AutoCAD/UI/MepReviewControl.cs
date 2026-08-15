using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using QS3D.Platform.Parity;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfDataGrid = System.Windows.Controls.DataGrid;
using WpfPanel = System.Windows.Controls.Panel;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace QS3D.AutoCAD.UI;

internal sealed class MepReviewControl : WpfUserControl
{
    private readonly ObservableCollection<RuleRow> _rows = new();
    private readonly WpfDataGrid _grid = new();
    private readonly TextBlock _status = new();

    internal MepReviewControl()
    {
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        Content = BuildLayout();
        RefreshProfile();
    }

    internal void RefreshProfile()
    {
        _rows.Clear();
        foreach (var rule in MepRecognitionProfileProvider.Current.Rules)
            _rows.Add(RuleRow.FromRule(rule));
        _grid.ItemsSource = _rows;
        var error = MepRecognitionProfileProvider.LastError;
        _status.Text = string.IsNullOrWhiteSpace(error)
            ? (MepRecognitionProfileProvider.IsCustom ? "Custom recognition profile loaded." : "Built-in recognition profile active.")
            : error;
    }

    private FrameworkElement BuildLayout()
    {
        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock
        {
            Text = "QS3D MEP Review",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        AddAction(actions, "Takeoff", "QS3DMEPTAKEOFF");
        AddAction(actions, "Broad Clash", "QS3DMEPCLASH");
        AddAction(actions, "Clash Locate", "QS3DMEPCLASHLOCATE");
        AddAction(actions, "Exact Clash", "QS3DMEPEXACTCLASH");
        AddAction(actions, "Zoom Selection", "QS3DMEPZOOMSELECTION");
        Grid.SetRow(actions, 1);
        root.Children.Add(actions);

        ConfigureGrid();
        Grid.SetRow(_grid, 2);
        root.Children.Add(_grid);

        var editorActions = new WrapPanel { Margin = new Thickness(0, 10, 0, 6) };
        editorActions.Children.Add(Button("Add Rule", (_, _) => AddRule()));
        editorActions.Children.Add(Button("Remove Selected", (_, _) => RemoveSelected()));
        editorActions.Children.Add(Button("Save Profile", (_, _) => SaveProfile()));
        editorActions.Children.Add(Button("Reload", (_, _) => ReloadProfile()));
        editorActions.Children.Add(Button("Built-in Defaults", (_, _) => SaveDefaults()));
        Grid.SetRow(editorActions, 3);
        root.Children.Add(editorActions);

        _status.TextWrapping = TextWrapping.Wrap;
        _status.Margin = new Thickness(0, 4, 0, 0);
        Grid.SetRow(_status, 4);
        root.Children.Add(_status);
        return root;
    }

    private void ConfigureGrid()
    {
        _grid.AutoGenerateColumns = false;
        _grid.CanUserAddRows = false;
        _grid.CanUserDeleteRows = false;
        _grid.SelectionMode = DataGridSelectionMode.Single;
        _grid.HeadersVisibility = DataGridHeadersVisibility.Column;
        _grid.Columns.Add(TextColumn("Id", nameof(RuleRow.Id), 150));
        _grid.Columns.Add(TextColumn("Priority", nameof(RuleRow.Priority), 70));
        _grid.Columns.Add(TextColumn("Discipline", nameof(RuleRow.Discipline), 90));
        _grid.Columns.Add(TextColumn("Category", nameof(RuleRow.Category), 110));
        _grid.Columns.Add(TextColumn("Source", nameof(RuleRow.Source), 130));
        _grid.Columns.Add(TextColumn("MEP Kind", nameof(RuleRow.MepKind), 95));
        _grid.Columns.Add(TextColumn("Tokens (;)", nameof(RuleRow.Tokens), 220));
    }

    private static DataGridTextColumn TextColumn(string header, string property, double width) => new()
    {
        Header = header,
        Binding = new Binding(property) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
        Width = new DataGridLength(width)
    };

    private static WpfButton Button(string label, RoutedEventHandler handler)
    {
        var button = new WpfButton
        {
            Content = label,
            MinWidth = 96,
            Margin = new Thickness(0, 0, 8, 6),
            Padding = new Thickness(10, 5, 10, 5)
        };
        button.Click += handler;
        return button;
    }

    private static void AddAction(WpfPanel panel, string label, string command)
    {
        panel.Children.Add(Button(label, (_, _) => QueueCommand(command)));
    }

    private static void QueueCommand(string command)
    {
        var document = AcApplication.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        document.SendStringToExecute(command + " ", true, false, false);
    }

    private void AddRule()
    {
        var suffix = _rows.Count + 1;
        _rows.Add(new RuleRow
        {
            Id = "custom.rule." + suffix.ToString(CultureInfo.InvariantCulture),
            Priority = "100",
            Discipline = MepDiscipline.Mep.ToString(),
            Category = "Custom",
            Source = MepRecognitionSource.LayerOrBlockName.ToString(),
            MepKind = MepElementKind.Equipment.ToString(),
            Tokens = "CUSTOM"
        });
        _grid.SelectedItem = _rows[_rows.Count - 1];
        _grid.ScrollIntoView(_grid.SelectedItem);
        _status.Text = "New rule added locally. Save Profile to persist it.";
    }

    private void RemoveSelected()
    {
        if (_grid.SelectedItem is not RuleRow row)
        {
            _status.Text = "Select a rule to remove.";
            return;
        }
        if (_rows.Count <= 1)
        {
            _status.Text = "At least one recognition rule is required.";
            return;
        }
        _rows.Remove(row);
        _status.Text = "Rule removed locally. Save Profile to persist the change.";
    }

    private void SaveProfile()
    {
        try
        {
            _grid.CommitEdit(DataGridEditingUnit.Cell, true);
            _grid.CommitEdit(DataGridEditingUnit.Row, true);
            var rules = new List<MepRecognitionRule>(_rows.Count);
            foreach (var row in _rows) rules.Add(row.ToRule());
            var profile = new MepRecognitionProfile(rules);
            MepRecognitionProfileProvider.Save(profile);
            RefreshProfile();
            _status.Text = "Recognition profile saved atomically and activated for subsequent MEP commands.";
        }
        catch (Exception ex)
        {
            _status.Text = "Profile save refused: " + ex.Message;
        }
    }

    private void ReloadProfile()
    {
        MepRecognitionProfileProvider.Reload();
        RefreshProfile();
    }

    private void SaveDefaults()
    {
        try
        {
            MepRecognitionProfileProvider.SaveDefault();
            RefreshProfile();
            _status.Text = "Built-in recognition rules saved and activated.";
        }
        catch (Exception ex)
        {
            _status.Text = "Default profile save refused: " + ex.Message;
        }
    }

    private sealed class RuleRow
    {
        public string Id { get; set; } = string.Empty;
        public string Priority { get; set; } = "0";
        public string Discipline { get; set; } = MepDiscipline.Mep.ToString();
        public string Category { get; set; } = string.Empty;
        public string Source { get; set; } = MepRecognitionSource.LayerOrBlockName.ToString();
        public string MepKind { get; set; } = MepElementKind.Equipment.ToString();
        public string Tokens { get; set; } = string.Empty;

        internal static RuleRow FromRule(MepRecognitionRule rule) => new()
        {
            Id = rule.Id,
            Priority = rule.Priority.ToString(CultureInfo.InvariantCulture),
            Discipline = rule.Discipline.ToString(),
            Category = rule.Category,
            Source = rule.Source.ToString(),
            MepKind = rule.MepKind?.ToString() ?? string.Empty,
            Tokens = string.Join(";", rule.Tokens)
        };

        internal MepRecognitionRule ToRule()
        {
            var id = Required(Id, "rule id");
            if (!int.TryParse(Priority, NumberStyles.Integer, CultureInfo.InvariantCulture, out var priority))
                throw new InvalidOperationException("Rule " + id + " priority must be an integer.");
            if (!Enum.TryParse(Discipline, true, out MepDiscipline discipline) || !Enum.IsDefined(typeof(MepDiscipline), discipline))
                throw new InvalidOperationException("Rule " + id + " discipline is invalid.");
            var category = Required(Category, "category");
            if (!Enum.TryParse(Source, true, out MepRecognitionSource source) || source == MepRecognitionSource.None ||
                (source & ~MepRecognitionSource.LayerOrBlockName) != MepRecognitionSource.None)
                throw new InvalidOperationException("Rule " + id + " recognition source is invalid.");

            MepElementKind? mepKind = null;
            if (discipline == MepDiscipline.Mep)
            {
                if (!Enum.TryParse(MepKind, true, out MepElementKind parsedKind) || !Enum.IsDefined(typeof(MepElementKind), parsedKind))
                    throw new InvalidOperationException("Rule " + id + " requires a valid MEP kind.");
                mepKind = parsedKind;
            }
            else if (!string.IsNullOrWhiteSpace(MepKind))
                throw new InvalidOperationException("Rule " + id + " must leave MEP Kind blank for non-MEP disciplines.");

            var tokens = (Tokens ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(static token => token.Trim())
                .Where(static token => token.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (tokens.Length == 0) throw new InvalidOperationException("Rule " + id + " requires at least one token.");
            return new MepRecognitionRule(id, priority, discipline, category, tokens, source, mepKind);
        }

        private static string Required(string? value, string label)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0) throw new InvalidOperationException(label + " is required.");
            return normalized;
        }
    }
}
