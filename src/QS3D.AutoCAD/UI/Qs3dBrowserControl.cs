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
    private readonly ListView _list = new();
    private readonly PropertyGrid _properties = new();
    private readonly Button _refresh = new();
    private readonly Button _select = new();
    private readonly Button _edit = new();
    private readonly Button _language = new();
    private Document? _boundDocument;
    private bool _syncingSelection;

    public Qs3dBrowserControl()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(8);

        _title.AutoSize = true;
        _title.Font = new Font(Font, FontStyle.Bold);
        _title.Dock = DockStyle.Top;
        _title.Padding = new Padding(0, 0, 0, 6);

        _list.Dock = DockStyle.Top;
        _list.Height = 210;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.HideSelection = false;
        _list.MultiSelect = false;
        _list.Columns.Add("Kind", 70);
        _list.Columns.Add("Name", 130);
        _list.Columns.Add("Handle", 70);
        _list.SelectedIndexChanged += (_, _) => ShowSelectedProperties();
        _list.DoubleClick += (_, _) => SelectSelectedInDrawing();

        _properties.Dock = DockStyle.Fill;
        _properties.HelpVisible = false;
        _properties.ToolbarVisible = false;

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 74,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = false
        };

        ConfigureButton(_refresh, (_, _) => RefreshData());
        ConfigureButton(_select, (_, _) => SelectSelectedInDrawing());
        ConfigureButton(_edit, (_, _) => EditSelected());
        ConfigureButton(_language, (_, _) => UiText.Toggle());
        actions.Controls.AddRange([_refresh, _select, _edit, _language]);

        Controls.Add(_properties);
        Controls.Add(_list);
        Controls.Add(_title);
        Controls.Add(actions);

        UiText.LanguageChanged += OnLanguageChanged;
        ApplyLanguage();
        RefreshData();
    }

    public void RefreshData()
    {
        var document = AcApplication.DocumentManager.MdiActiveDocument;
        BindDocument(document);

        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            _properties.SelectedObject = null;
            if (document is null)
            {
                return;
            }

            foreach (var item in Qs3dDocumentIndex.Scan(document.Database))
            {
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

        SyncFromDrawingSelection();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UiText.LanguageChanged -= OnLanguageChanged;
            BindDocument(null);
        }

        base.Dispose(disposing);
    }

    private static void ConfigureButton(Button button, EventHandler handler)
    {
        button.AutoSize = true;
        button.Height = 30;
        button.Margin = new Padding(2);
        button.Click += handler;
    }

    private void BindDocument(Document? document)
    {
        if (ReferenceEquals(_boundDocument, document))
        {
            return;
        }

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

    private void OnImpliedSelectionChanged(object? sender, EventArgs e)
    {
        SyncFromDrawingSelection();
    }

    private void OnCommandEnded(object sender, CommandEventArgs e)
    {
        if (e.GlobalCommandName.StartsWith("QS3D", StringComparison.OrdinalIgnoreCase))
        {
            RefreshData();
        }
    }

    private void SyncFromDrawingSelection()
    {
        if (_syncingSelection || _boundDocument is null)
        {
            return;
        }

        var implied = _boundDocument.Editor.SelectImplied();
        if (implied.Status != PromptStatus.OK || implied.Value.Count != 1)
        {
            return;
        }

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
        if (_syncingSelection)
        {
            return;
        }

        _properties.SelectedObject = SelectedEntity is { } item ? new EntityPropertyView(item) : null;
    }

    private void SelectSelectedInDrawing()
    {
        if (SelectedEntity is not { } item)
        {
            return;
        }

        var document = AcApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

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
        if (SelectedEntity is null)
        {
            return;
        }

        SelectSelectedInDrawing();
        AcApplication.DocumentManager.MdiActiveDocument?.SendStringToExecute("QS3DEDIT ", true, false, false);
    }

    private Qs3dIndexedEntity? SelectedEntity =>
        _list.SelectedItems.Count == 1 ? _list.SelectedItems[0].Tag as Qs3dIndexedEntity : null;

    private void OnLanguageChanged(object? sender, EventArgs e) => ApplyLanguage();

    private void ApplyLanguage()
    {
        _title.Text = UiText.Get("elements");
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

        public EntityPropertyView(Qs3dIndexedEntity entity)
        {
            _entity = entity;
        }

        [Category("Identity")]
        public string Name => _entity.Metadata.Name;

        [Category("Identity")]
        public ElementKind Kind => _entity.Metadata.Kind;

        [Category("Identity")]
        public string Id => _entity.Metadata.Id.ToString("D");

        [Category("DWG")]
        public string Handle => _entity.Handle;

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
    }
}
