using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using QS3D.AutoCAD.Infrastructure;
using QS3D.AutoCAD.Metadata;
using QS3D.AutoCAD.Persistence;
using QS3D.AutoCAD.UI;
using QS3D.Core.Geometry;
using QS3D.Core.Model;
using QS3D.Core.Services;

namespace QS3D.AutoCAD.Commands;

public sealed class Qs3dCommands
{
    [CommandMethod("QS3D", CommandFlags.Modal)]
    public void OpenPalette() => Qs3dPalette.Show();

    [CommandMethod("QS3DABOUT", CommandFlags.Modal)]
    public void About()
    {
        CurrentEditor()?.WriteMessage($"\nQS3D AutoCAD bootstrap | CLR {Environment.Version} | commands: LEVEL GRID COLUMN BEAM SLAB WALL CURTAIN SECTION BOQ\n");
    }

    [CommandMethod("QS3DINIT", CommandFlags.Modal)]
    public void InitializeProject()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var current = DwgProjectStore.GetOrCreate(document.Database);
        var result = document.Editor.GetString(new PromptStringOptions($"\nProject name <{current.Name}>: ") { AllowSpaces = true });
        var name = result.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(result.StringResult)
            ? result.StringResult
            : current.Name;
        var state = DwgProjectStore.GetOrCreate(document.Database, name);
        document.Editor.WriteMessage($"\nQS3D project: {state.Name} ({state.Id:N})\n");
    }

    [CommandMethod("QS3DLEVEL", CommandFlags.Modal)]
    public void CreateLevel()
    {
        var context = GetContext();
        if (context is null) return;
        var (database, editor) = context.Value;
        var pointResult = editor.GetPoint("\nLevel marker position: ");
        if (pointResult.Status != PromptStatus.OK) return;
        var elevation = PromptDouble(editor, "Elevation", pointResult.Value.Z, allowZero: true);
        if (elevation is null) return;
        var name = PromptName(editor, "Level name", $"LEVEL-{elevation.Value:0.###}");
        if (name is null) return;
        var point = new Point3d(pointResult.Value.X, pointResult.Value.Y, elevation.Value);

        using var transaction = database.TransactionManager.StartTransaction();
        var layer = AutoCadDrawing.EnsureLayer(transaction, database, "QS3D-LEVEL", 2);
        var marker = new DBPoint(point);
        AutoCadDrawing.Append(transaction, database, marker, layer);
        var text = new DBText { Position = point, Height = 250, TextString = $"{name}  EL={elevation.Value:0.###}" };
        AutoCadDrawing.Append(transaction, database, text, layer);
        new Qs3dEntityMetadata(Guid.NewGuid(), ElementKind.Level, name, ToCore(point), ToCore(point), 0, 0, 0, 0).Attach(transaction, database, marker);
        transaction.Commit();
    }

    [CommandMethod("QS3DGRID", CommandFlags.Modal)]
    public void CreateGrid()
    {
        CreateLinearMarker(ElementKind.Grid, "QS3D-GRID", 4, "Grid");
    }

    [CommandMethod("QS3DSECTION", CommandFlags.Modal)]
    public void CreateSection()
    {
        CreateLinearMarker(ElementKind.Section, "QS3D-SECTION", 1, "Section");
    }

    [CommandMethod("QS3DCOLUMN", CommandFlags.Modal)]
    public void CreateColumn()
    {
        var context = GetContext();
        if (context is null) return;
        var (database, editor) = context.Value;
        var pointResult = editor.GetPoint("\nColumn base point: ");
        if (pointResult.Status != PromptStatus.OK) return;
        var width = PromptDouble(editor, "Column width", 300); if (width is null) return;
        var depth = PromptDouble(editor, "Column depth", 300); if (depth is null) return;
        var height = PromptDouble(editor, "Column height", 3000); if (height is null) return;
        var name = PromptName(editor, "Column name", "C"); if (name is null) return;

        using var transaction = database.TransactionManager.StartTransaction();
        var layer = AutoCadDrawing.EnsureLayer(transaction, database, "QS3D-COLUMN", 3);
        var solid = AutoCadDrawing.CreateAxisAlignedBox(pointResult.Value, width.Value, depth.Value, height.Value);
        AutoCadDrawing.Append(transaction, database, solid, layer);
        new Qs3dEntityMetadata(Guid.NewGuid(), ElementKind.Column, name, ToCore(pointResult.Value), ToCore(pointResult.Value), width.Value, depth.Value, height.Value, 0).Attach(transaction, database, solid);
        transaction.Commit();
    }

    [CommandMethod("QS3DBEAM", CommandFlags.Modal)]
    public void CreateBeam()
    {
        CreateLinearSolid(ElementKind.Beam, "QS3D-BEAM", 5, "Beam", 200, 400);
    }

    [CommandMethod("QS3DWALL", CommandFlags.Modal)]
    public void CreateWall()
    {
        CreateLinearSolid(ElementKind.Wall, "QS3D-WALL", 6, "Wall", 200, 3000);
    }

    [CommandMethod("QS3DSLAB", CommandFlags.Modal)]
    public void CreateSlab()
    {
        var context = GetContext();
        if (context is null) return;
        var (database, editor) = context.Value;
        var first = editor.GetPoint("\nSlab first corner: ");
        if (first.Status != PromptStatus.OK) return;
        var secondOptions = new PromptCornerOptions("\nSlab opposite corner: ", first.Value);
        var second = editor.GetCorner(secondOptions);
        if (second.Status != PromptStatus.OK) return;
        var thickness = PromptDouble(editor, "Slab thickness", 150); if (thickness is null) return;
        var name = PromptName(editor, "Slab name", "S"); if (name is null) return;

        var min = new Point3d(Math.Min(first.Value.X, second.Value.X), Math.Min(first.Value.Y, second.Value.Y), Math.Min(first.Value.Z, second.Value.Z));
        var x = Math.Abs(second.Value.X - first.Value.X);
        var y = Math.Abs(second.Value.Y - first.Value.Y);
        if (x <= Tolerance.Global.EqualPoint || y <= Tolerance.Global.EqualPoint)
        {
            editor.WriteMessage("\nSlab corners must define a non-zero rectangle.\n");
            return;
        }

        using var transaction = database.TransactionManager.StartTransaction();
        var layer = AutoCadDrawing.EnsureLayer(transaction, database, "QS3D-SLAB", 8);
        var solid = AutoCadDrawing.CreateAxisAlignedBox(min, x, y, thickness.Value);
        AutoCadDrawing.Append(transaction, database, solid, layer);
        new Qs3dEntityMetadata(Guid.NewGuid(), ElementKind.Slab, name, ToCore(first.Value), ToCore(second.Value), 0, 0, 0, thickness.Value).Attach(transaction, database, solid);
        transaction.Commit();
    }

    [CommandMethod("QS3DCURTAIN", CommandFlags.Modal)]
    public void CreateCurtain()
    {
        var context = GetContext();
        if (context is null) return;
        var (database, editor) = context.Value;
        var start = editor.GetPoint("\nCurtain start point: "); if (start.Status != PromptStatus.OK) return;
        var end = editor.GetPoint(new PromptPointOptions("\nCurtain end point: ") { BasePoint = start.Value, UseBasePoint = true }); if (end.Status != PromptStatus.OK) return;
        var module = PromptDouble(editor, "Maximum panel width", 1200); if (module is null) return;
        var thickness = PromptDouble(editor, "Panel thickness", 50); if (thickness is null) return;
        var height = PromptDouble(editor, "Curtain height", 3000); if (height is null) return;
        var name = PromptName(editor, "Curtain name", "CW"); if (name is null) return;

        var dx = end.Value.X - start.Value.X;
        var dy = end.Value.Y - start.Value.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length <= Tolerance.Global.EqualPoint) return;
        var panelCount = Math.Max(1, (int)Math.Ceiling(length / module.Value));

        using var transaction = database.TransactionManager.StartTransaction();
        var layer = AutoCadDrawing.EnsureLayer(transaction, database, "QS3D-CURTAIN", 4);
        for (var index = 0; index < panelCount; index++)
        {
            var t0 = (double)index / panelCount;
            var t1 = (double)(index + 1) / panelCount;
            var panelStart = new Point3d(start.Value.X + (dx * t0), start.Value.Y + (dy * t0), start.Value.Z);
            var panelEnd = new Point3d(start.Value.X + (dx * t1), start.Value.Y + (dy * t1), start.Value.Z);
            var solid = AutoCadDrawing.CreatePlanOrientedBox(panelStart, panelEnd, thickness.Value, height.Value);
            AutoCadDrawing.Append(transaction, database, solid, layer);
            new Qs3dEntityMetadata(Guid.NewGuid(), ElementKind.Curtain, $"{name}-{index + 1}", ToCore(panelStart), ToCore(panelEnd), 0, 0, height.Value, thickness.Value).Attach(transaction, database, solid);
        }
        transaction.Commit();
        editor.WriteMessage($"\nCreated {panelCount} curtain panel(s).\n");
    }

    [CommandMethod("QS3DBOQ", CommandFlags.Modal)]
    public void QuantityTakeoff()
    {
        var context = GetContext();
        if (context is null) return;
        var (database, editor) = context.Value;
        var elements = new List<StructuralElement>();

        using var transaction = database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
        foreach (ObjectId id in space)
        {
            if (transaction.GetObject(id, OpenMode.ForRead) is Entity entity && Qs3dEntityMetadata.TryRead(entity, out var metadata))
            {
                elements.Add(metadata.ToCore());
            }
        }
        transaction.Commit();

        var summaries = QuantityService.Summarize(elements);
        editor.WriteMessage($"\nQS3D BOQ — {elements.Count} tagged element(s) in current space\n");
        foreach (var item in summaries)
        {
            editor.WriteMessage($"  {item.Kind,-9} count={item.Count,4} area={item.Area:0.###} du² volume={item.Volume:0.###} du³\n");
        }
    }

    private static void CreateLinearMarker(ElementKind kind, string layerName, short colorIndex, string label)
    {
        var context = GetContext();
        if (context is null) return;
        var (database, editor) = context.Value;
        var start = editor.GetPoint($"\n{label} start point: "); if (start.Status != PromptStatus.OK) return;
        var end = editor.GetPoint(new PromptPointOptions($"\n{label} end point: ") { BasePoint = start.Value, UseBasePoint = true }); if (end.Status != PromptStatus.OK) return;
        if (start.Value.DistanceTo(end.Value) <= Tolerance.Global.EqualPoint) return;
        var name = PromptName(editor, $"{label} name", label[..1].ToUpperInvariant()); if (name is null) return;

        using var transaction = database.TransactionManager.StartTransaction();
        var layer = AutoCadDrawing.EnsureLayer(transaction, database, layerName, colorIndex);
        var line = new Line(start.Value, end.Value);
        AutoCadDrawing.Append(transaction, database, line, layer);
        var text = new DBText { Position = start.Value, Height = 250, TextString = name };
        AutoCadDrawing.Append(transaction, database, text, layer);
        new Qs3dEntityMetadata(Guid.NewGuid(), kind, name, ToCore(start.Value), ToCore(end.Value), 0, 0, 0, 0).Attach(transaction, database, line);
        transaction.Commit();
    }

    private static void CreateLinearSolid(ElementKind kind, string layerName, short colorIndex, string label, double defaultWidth, double defaultHeight)
    {
        var context = GetContext();
        if (context is null) return;
        var (database, editor) = context.Value;
        var start = editor.GetPoint($"\n{label} start point: "); if (start.Status != PromptStatus.OK) return;
        var end = editor.GetPoint(new PromptPointOptions($"\n{label} end point: ") { BasePoint = start.Value, UseBasePoint = true }); if (end.Status != PromptStatus.OK) return;
        var width = PromptDouble(editor, kind == ElementKind.Wall ? "Wall thickness" : "Beam width", defaultWidth); if (width is null) return;
        var height = PromptDouble(editor, $"{label} height", defaultHeight); if (height is null) return;
        var name = PromptName(editor, $"{label} name", label[..1].ToUpperInvariant()); if (name is null) return;

        try
        {
            using var transaction = database.TransactionManager.StartTransaction();
            var layer = AutoCadDrawing.EnsureLayer(transaction, database, layerName, colorIndex);
            var solid = AutoCadDrawing.CreatePlanOrientedBox(start.Value, end.Value, width.Value, height.Value);
            AutoCadDrawing.Append(transaction, database, solid, layer);
            var metadata = kind == ElementKind.Beam
                ? new Qs3dEntityMetadata(Guid.NewGuid(), kind, name, ToCore(start.Value), ToCore(end.Value), width.Value, 0, height.Value, 0)
                : new Qs3dEntityMetadata(Guid.NewGuid(), kind, name, ToCore(start.Value), ToCore(end.Value), 0, 0, height.Value, width.Value);
            metadata.Attach(transaction, database, solid);
            transaction.Commit();
        }
        catch (ArgumentException exception)
        {
            editor.WriteMessage($"\n{exception.Message}\n");
        }
    }

    private static (Database Database, Editor Editor)? GetContext()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        return document is null ? null : (document.Database, document.Editor);
    }

    private static Editor? CurrentEditor() => Application.DocumentManager.MdiActiveDocument?.Editor;

    private static double? PromptDouble(Editor editor, string label, double defaultValue, bool allowZero = false)
    {
        var options = new PromptDoubleOptions($"\n{label} <{defaultValue:0.###}>: ")
        {
            DefaultValue = defaultValue,
            UseDefaultValue = true,
            AllowNegative = false,
            AllowZero = allowZero
        };
        var result = editor.GetDouble(options);
        return result.Status == PromptStatus.OK ? result.Value : null;
    }

    private static string? PromptName(Editor editor, string label, string fallback)
    {
        var result = editor.GetString(new PromptStringOptions($"\n{label} <{fallback}>: ") { AllowSpaces = true });
        if (result.Status == PromptStatus.Cancel) return null;
        return string.IsNullOrWhiteSpace(result.StringResult) ? fallback : result.StringResult.Trim();
    }

    private static Point3 ToCore(Point3d point) => new(point.X, point.Y, point.Z);
}
