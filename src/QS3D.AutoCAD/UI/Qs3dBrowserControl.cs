using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using QS3D.AutoCAD.Infrastructure;
using QS3D.Core.Model;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QS3D.AutoCAD.UI;

internal sealed class Qs3dBrowserControl : UserControl
{
    private readonly Label _title = new();
    private readonly TextBox _filter = new();
    private readonly ToolTip _filterToolTip = new();
    private readonly ComboBox _kindFilter = new();
    private readonly ListView _list = new();
    private readonly PropertyGrid _properties = new();
    private readonly Button _refresh = new();
    private readonly Button _select = new();
    private readonly Button _edit = new();
    private readonly Button _language = new();
    private IReadOnlyList<Qs3dIndexedEntity> _items = Array.Empty<Qs3dIndexedEntity>();
    private Document? _boundDocument;
    private bool _syncingSelection;
    private Qs3dThemePalette _theme = Qs3dThemeManager.Current;

    public Qs3dBrowserControl()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(10);
        AutoScaleMode = AutoScaleMode.Dpi;

        _title.AutoSize = true;
        _title.Font = new Font(Font, FontStyle.Bold);
        _title.Dock = DockStyle.Top;
        _title.Padding = new Padding(0, 0, 0, 8);

        var filters = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 0, 0, 8)
        };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

        _filter.Dock = DockStyle.Fill;
        _filter.Margin = new Padding(0, 0, 6, 0);
        _filter.BorderStyle = BorderStyle.FixedSingle;
        _filter.TextChanged += (_, _) => ApplyFilter();

        _kindFilter.Dock = DockStyle.Fill;
        _kindFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _kindFilter.FlatStyle = FlatStyle.Flat;
        _kindFilter.SelectedIndexChanged += (_, _) => ApplyFilter();
        filters.Controls.Add(_filter, 0, 0);
        filters.Controls.Add(_kindFilter, 1, 0);

        _list.Dock = DockStyle.Top;
        _list.Height = 220;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.HideSelection = false;
        _list.MultiSelect = false;
        _list.BorderStyle = BorderStyle.FixedSingle;
        _list.Columns.Add("Kind", 75);
        _list.Columns.Add("Name", 145);
        _list.Columns.Add("Handle", 76);
        _list.SelectedIndexChanged += (_, _) => ShowSelectedProperties();
        _list.DoubleClick += (_, _) => SelectSelectedInDrawing();

        _properties.Dock = DockStyle.Fill;
        _properties.HelpVisible = false;
        _properties.ToolbarVisible = false;
        _properties.PropertySort = PropertySort.Categorized;

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 76,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        ConfigureButton(_refresh, (_, _) => RefreshData());
        ConfigureButton(_select, (_, _) => SelectSelectedInDrawing());
        ConfigureButton(_edit, (_, _) => EditSelected());
        ConfigureButton(_language, (_, _) => UiText.Toggle());
        actions.Controls.AddRange([_refresh, _select, _edit, _language]);

        Controls.Add(_properties);
        Controls.Add(_list);
        Controls.Add(filters);
        Controls.Add(_title);
        Controls.Add(actions);

        Qs3dThemeManager.ThemeChanged += OnThemeChanged;
        UiText.LanguageChanged += OnLanguageChanged;
        ApplyTheme(_theme);
        ApplyLanguage();
        RefreshData();
    }

    public void RefreshData()
    {
        var document = AcApplication.DocumentManager.MdiActiveDocument;
        BindDocument(document);
        _items = document is null ? Array.Empty<Qs3dIndexedEntity>() : Qs3dDocumentIndex.Scan(document.Database);
        RefreshKindFilter();
        ApplyFilter();
        SyncFromDrawingSelection();
    }

    internal void ApplyTheme(Qs3dThemePalette? theme = null)
    {
        _theme = theme ?? Qs3dThemeManager.Current;
        BackColor = _theme.BackgroundColor;
        ForeColor = _theme.ForegroundColor;

        _title.BackColor = _theme.BackgroundColor;
        _title.ForeColor = _theme.ForegroundColor;
        _filter.BackColor = _theme.InputColor;
        _filter.ForeColor = _theme.ForegroundColor;
        _kindFilter.BackColor = _theme.InputColor;
        _kindFilter.ForeColor = _theme.ForegroundColor;
        _list.BackColor = _theme.CardColor;
        _list.ForeColor = _theme.ForegroundColor;

        _properties.BackColor = _theme.CardColor;
        _properties.ViewBackColor = _theme.CardColor;
        _properties.ViewForeColor = _theme.ForegroundColor;
        _properties.LineColor = _theme.BorderColor;
        _properties.CategoryForeColor = _theme.MutedColor;

        foreach (var button in new[] { _refresh, _select, _edit, _language })
            ApplyButtonTheme(button);

        foreach (Control child in Controls)
        {
            if (child is Panel panel) panel.BackColor = _theme.BackgroundColor;
        }
        Invalidate(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Qs3dThemeManager.ThemeChanged -= OnThemeChanged;
            UiText.LanguageChanged -= OnLanguageChanged;
            BindDocument(null);
            _filterToolTip.Dispose();
        }
        base.Dispose(disposing);
    }

    private void ConfigureButton(Button button, EventHandler handler)
    {
        button.AutoSize = true;
        button.Height = 31;
        button.Margin = new Padding(2, 2, 4, 2);
        button.Padding = new Padding(8, 2, 8, 2);
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;
        button.Click += handler;
        button.MouseEnter += (_, _) => button.BackColor = _theme.CardHoverColor;
        button.MouseLeave += (_, _) => button.BackColor = _theme.CardColor;
    }

    private void ApplyButtonTheme(Button button)
    {
        button.BackColor = _theme.CardColor;
        button.ForeColor = _theme.ForegroundColor;
        button.FlatAppearance.BorderColor = _theme.BorderColor;
        button.FlatAppearance.MouseDownBackColor = _theme.SelectionColor;
        button.FlatAppearance.MouseOverBackColor = _theme.CardHoverColor;
    }

    private void RefreshKindFilter()
    {
        var selected = _kindFilter.SelectedItem as string;
        _kindFilter.BeginUpdate();
        try
        {
            _kindFilter.Items.Clear();
            _kindFilter.Items.Add(UiText.Get("allKinds"));
            foreach (var kind in _items.Select(item => item.Metadata.Kind.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase))
                _kindFilter.Items.Add(kind);
            var index = selected is null ? 0 : _kindFilter.Items.IndexOf(selected);
            _kindFilter.SelectedIndex = index >= 0 ? index : 0;
        }
        finally
        {
            _kindFilter.EndUpdate();
        }
    }

    private void ApplyFilter()
    {
        var query = _filter.Text.Trim();
        var selectedKind = _kindFilter.SelectedIndex <= 0 ? null : _kindFilter.SelectedItem as string;
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            _properties.SelectedObject = null;
            foreach (var item in _items)
            {
                if (selectedKind is not null && !string.Equals(item.Metadata.Kind.ToString(), selectedKind, StringComparison.OrdinalIgnoreCase)) continue;
                if (query.Length > 0 &&
                    item.Metadata.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0 &&
                    item.Metadata.Kind.ToString().IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0 &&
                    item.Handle.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

                var row = new ListViewItem(item.Metadata.Kind.ToString()) { Tag = item };
                row.SubItems.Add(item.Metadata.Name);
                row.SubItems.Add(item.Handle);
                _list.Items.Add(row);
            }
        }
        finally
        {
            _list.EndUpdate();
        }
    }

    private void BindDocument(Document? document)
    {
        if (ReferenceEquals(_boundDocument, document)) return;
        if (_boundDocument is not null)
        {
            _boundDocument.ImpliedSelectionChanged -= OnImpliedSelectionChanged;
            _boundDocument.CommandEnded -= OnCommandEnded;
        }
        _boundDocument = document;
        if (_boundDocument is not null)
        {
            _boundDocument.ImpliedSelectionChanged += OnImpliedSelectionChanged;
            _boundDocument.CommandEnded += OnCommandEnded;
        }
    }

    private void OnImpliedSelectionChanged(object? sender, EventArgs e) => SyncFromDrawingSelection();

    private void OnCommandEnded(object sender, CommandEventArgs e)
    {
        if (e.GlobalCommandName.StartsWith("QS3D", StringComparison.OrdinalIgnoreCase)) RefreshData();
    }

    private void SyncFromDrawingSelection()
    {
        if (_syncingSelection || _boundDocument is null) return;
        var implied = _boundDocument.Editor.SelectImplied();
        if (implied.Status != PromptStatus.OK || implied.Value.Count != 1) return;
        var id = implied.Value.GetObjectIds()[0];
        foreach (ListViewItem row in _list.Items)
        {
            if (row.Tag is Qs3dIndexedEntity indexed && indexed.ObjectId == id)
            {
                _syncingSelection = true;
                try
                {
                    row.Selected = true;
                    row.Focused = true;
                    row.EnsureVisible();
                    _properties.SelectedObject = new EntityPropertyView(indexed);
                }
                finally
                {
                    _syncingSelection = false;
                }
                return;
            }
        }
    }

    private void ShowSelectedProperties()
    {
        if (_syncingSelection) return;
        _properties.SelectedObject = SelectedEntity is { } item ? new EntityPropertyView(item) : null;
    }

    private void SelectSelectedInDrawing()
    {
        if (SelectedEntity is not { } item) return;
        var document = AcApplication.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        _syncingSelection = true;
        try
        {
            document.Editor.SetImpliedSelection([item.ObjectId]);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void EditSelected()
    {
        if (SelectedEntity is null) return;
        SelectSelectedInDrawing();
        Qs3dCommandDispatcher.Execute("QS3DEDIT");
    }

    private Qs3dIndexedEntity? SelectedEntity =>
        _list.SelectedItems.Count == 1 ? _list.SelectedItems[0].Tag as Qs3dIndexedEntity : null;

    private void OnThemeChanged(object? sender, Qs3dThemeChangedEventArgs e)
    {
        if (IsDisposed) return;
        if (IsHandleCreated && InvokeRequired)
        {
            BeginInvoke(new Action(() => ApplyTheme(e.Palette)));
            return;
        }
        ApplyTheme(e.Palette);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ApplyLanguage();
        RefreshKindFilter();
    }

    private void ApplyLanguage()
    {
        _title.Text = UiText.Get("elements");
        _filterToolTip.SetToolTip(_filter, UiText.Get("browserSearchHint"));
        _refresh.Text = UiText.Get("refresh");
        _select.Text = UiText.Get("select");
        _edit.Text = UiText.Get("edit");
        _language.Text = UiText.Get("language");
        _list.Columns[0].Text = UiText.Get("kind");
        _list.Columns[1].Text = UiText.Get("name");
        _list.Columns[2].Text = UiText.Get("handle");
    }

    private sealed class EntityPropertyView
    {
        private readonly Qs3dIndexedEntity _entity;
        public EntityPropertyView(Qs3dIndexedEntity entity) => _entity = entity;

        [Category("Identity")]
        public string Name => _entity.Metadata.Name;
        [Category("Identity")]
        public ElementKind Kind => _entity.Metadata.Kind;
        [Category("Identity")]
        public string Id => _entity.Metadata.Id.ToString("D");
        [Category("DWG")]
        public string Handle => _entity.Handle;
        [Category("Placement")]
        public string LevelId => FormatReference(_entity.Metadata.LevelId);
        [Category("Placement")]
        public string StartGridId => FormatReference(_entity.Metadata.StartGridId);
        [Category("Placement")]
        public string EndGridId => FormatReference(_entity.Metadata.EndGridId);
        [Category("Geometry")]
        public double Width => _entity.Metadata.Width;
        [Category("Geometry")]
        public double Depth => _entity.Metadata.Depth;
        [Category("Geometry")]
        public double Height => _entity.Metadata.Height;
        [Category("Geometry")]
        public double Thickness => _entity.Metadata.Thickness;
        [Category("Quantity")]
        public double PlanLength => _entity.Metadata.ToCore().PlanLength;
        [Category("Quantity")]
        public double Area => _entity.Metadata.ToCore().Area;
        [Category("Quantity")]
        public double Volume => _entity.Metadata.ToCore().Volume;

        private static string FormatReference(Guid? value) => value?.ToString("D") ?? "—";
    }
}
