using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using QS3D.AutoCAD.Infrastructure;
using QS3D.AutoCAD.Metadata;
using QS3D.AutoCAD.UI;
using QS3D.Core.Geometry;
using QS3D.Core.Model;

[assembly: CommandClass(typeof(QS3D.AutoCAD.Commands.Qs3dJigCommands))]

namespace QS3D.AutoCAD.Commands;

public sealed class Qs3dJigCommands
{
    [CommandMethod("QS3DCOLUMNJIG", CommandFlags.Modal)]
    public void CreateColumnWithPreview()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        var database = document.Database;

        var width = PromptPositive(editor, "Column width", 300); if (width is null) return;
        var depth = PromptPositive(editor, "Column depth", 300); if (depth is null) return;
        var height = PromptPositive(editor, "Column height", 3000); if (height is null) return;
        var name = PromptName(editor, "Column name", "C"); if (name is null) return;

        IEnumerable<Entity> Preview(Point3d point) =>
            [AutoCadDrawing.CreateAxisAlignedBox(point, width.Value, depth.Value, height.Value)];
        Qs3dPreviewAnnotation? Annotation(Point3d _) => new(
            $"W={width.Value:0.###}  D={depth.Value:0.###}  H={height.Value:0.###}  Ang=0deg",
            LiveTextHeight(width.Value, depth.Value));

        var jig = new Qs3dPointPreviewJig(
            Point3d.Origin,
            "\nColumn base point: ",
            Preview,
            useBasePoint: false,
            annotationFactory: Annotation);
        var drag = jig.Drag(editor);
        if (drag.Status != PromptStatus.OK) return;

        var point = jig.Point;
        var finalSolid = AutoCadDrawing.CreateAxisAlignedBox(point, width.Value, depth.Value, height.Value);
        using var transaction = database.TransactionManager.StartTransaction();
        var layer = AutoCadDrawing.EnsureLayer(transaction, database, "QS3D-COLUMN", 3);
        AutoCadDrawing.Append(transaction, database, finalSolid, layer);
        new Qs3dEntityMetadata(
            Guid.NewGuid(),
            ElementKind.Column,
            name,
            ToCore(point),
            ToCore(point),
            width.Value,
            depth.Value,
            height.Value,
            0).Attach(transaction, database, finalSolid);
        transaction.Commit();
        editor.SetImpliedSelection([finalSolid.ObjectId]);
        Qs3dPalette.RefreshBrowser();
    }

    [CommandMethod("QS3DBEAMJIG", CommandFlags.Modal)]
    public void CreateBeamWithPreview() => CreateLinearSolidWithPreview(
        ElementKind.Beam,
        "QS3D-BEAM",
        5,
        "Beam",
        "Beam width",
        200,
        "Beam height",
        400,
        isThickness: false);

    [CommandMethod("QS3DWALLJIG", CommandFlags.Modal)]
    public void CreateWallWithPreview() => CreateLinearSolidWithPreview(
        ElementKind.Wall,
        "QS3D-WALL",
        6,
        "Wall",
        "Wall thickness",
        200,
        "Wall height",
        3000,
        isThickness: true);

    [CommandMethod("QS3DSLABJIG", CommandFlags.Modal)]
    public void CreateSlabWithPreview()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        var database = document.Database;

        var first = editor.GetPoint("\nSlab first corner: ");
        if (first.Status != PromptStatus.OK) return;
        var thickness = PromptPositive(editor, "Slab thickness", 150); if (thickness is null) return;
        var name = PromptName(editor, "Slab name", "S"); if (name is null) return;

        Point3d Normalize(Point3d point) => new(point.X, point.Y, first.Value.Z);
        IEnumerable<Entity> Preview(Point3d point)
        {
            var normalized = Normalize(point);
            if (!TryGetSlabBox(first.Value, normalized, thickness.Value, out var solid))
            {
                return Array.Empty<Entity>();
            }
            return [solid];
        }
        Qs3dPreviewAnnotation? Annotation(Point3d point)
        {
            var normalized = Normalize(point);
            var x = Math.Abs(normalized.X - first.Value.X);
            var y = Math.Abs(normalized.Y - first.Value.Y);
            if (x <= Tolerance.Global.EqualPoint || y <= Tolerance.Global.EqualPoint)
            {
                return null;
            }
            return new Qs3dPreviewAnnotation(
                $"X={x:0.###}  Y={y:0.###}  T={thickness.Value:0.###}  A={(x * y):0.###}  Ang=0deg",
                LiveTextHeight(thickness.Value));
        }

        var jig = new Qs3dPointPreviewJig(
            first.Value,
            "\nSlab opposite corner: ",
            Preview,
            Normalize,
            annotationFactory: Annotation);
        var drag = jig.Drag(editor);
        if (drag.Status != PromptStatus.OK) return;

        var second = jig.Point;
        if (!TryGetSlabBox(first.Value, second, thickness.Value, out var finalSolid))
        {
            editor.WriteMessage("\nSlab corners must define a non-zero rectangle.\n");
            return;
        }

        using var transaction = database.TransactionManager.StartTransaction();
        var layer = AutoCadDrawing.EnsureLayer(transaction, database, "QS3D-SLAB", 8);
        AutoCadDrawing.Append(transaction, database, finalSolid, layer);
        new Qs3dEntityMetadata(
            Guid.NewGuid(),
            ElementKind.Slab,
            name,
            ToCore(first.Value),
            ToCore(second),
            0,
            0,
            0,
            thickness.Value).Attach(transaction, database, finalSolid);
        transaction.Commit();
        editor.SetImpliedSelection([finalSolid.ObjectId]);
        Qs3dPalette.RefreshBrowser();
    }

    [CommandMethod("QS3DCURTAINJIG", CommandFlags.Modal)]
    public void CreateCurtainWithPreview()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        var database = document.Database;

        var start = editor.GetPoint("\nCurtain start point: ");
        if (start.Status != PromptStatus.OK) return;
        var module = PromptPositive(editor, "Maximum panel width", 1200); if (module is null) return;
        var thickness = PromptPositive(editor, "Panel thickness", 50); if (thickness is null) return;
        var height = PromptPositive(editor, "Curtain height", 3000); if (height is null) return;
        var name = PromptName(editor, "Curtain name", "CW"); if (name is null) return;

        IEnumerable<Entity> Preview(Point3d end) => CreateCurtainSolids(
            start.Value,
            end,
            module.Value,
            thickness.Value,
            height.Value).Cast<Entity>();
        Qs3dPreviewAnnotation? Annotation(Point3d end)
        {
            var length = PlanLength(start.Value, end);
            if (length <= Tolerance.Global.EqualPoint)
            {
                return null;
            }
            var panelCount = Math.Max(1, (int)Math.Ceiling(length / module.Value));
            var panelWidth = length / panelCount;
            return new Qs3dPreviewAnnotation(
                $"L={length:0.###}  Panel={panelWidth:0.###}  T={thickness.Value:0.###}  H={height.Value:0.###}  Ang={PlanAngleDegrees(start.Value, end):0.##}deg",
                LiveTextHeight(thickness.Value));
        }

        var jig = new Qs3dPointPreviewJig(
            start.Value,
            "\nCurtain end point: ",
            Preview,
            annotationFactory: Annotation);
        var drag = jig.Drag(editor);
        if (drag.Status != PromptStatus.OK) return;
        var end = jig.Point;

        var dx = end.X - start.Value.X;
        var dy = end.Y - start.Value.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length <= Tolerance.Global.EqualPoint)
        {
            editor.WriteMessage("\nCurtain must have non-zero plan length.\n");
            return;
        }
        var panelCount = Math.Max(1, (int)Math.Ceiling(length / module.Value));

        using var transaction = database.TransactionManager.StartTransaction();
        var layer = AutoCadDrawing.EnsureLayer(transaction, database, "QS3D-CURTAIN", 4);
        var selected = new List<ObjectId>();
        for (var index = 0; index < panelCount; index++)
        {
            var t0 = (double)index / panelCount;
            var t1 = (double)(index + 1) / panelCount;
            var panelStart = new Point3d(
                start.Value.X + dx * t0,
                start.Value.Y + dy * t0,
                start.Value.Z);
            var panelEnd = new Point3d(
                start.Value.X + dx * t1,
                start.Value.Y + dy * t1,
                start.Value.Z);
            var solid = AutoCadDrawing.CreatePlanOrientedBox(panelStart, panelEnd, thickness.Value, height.Value);
            AutoCadDrawing.Append(transaction, database, solid, layer);
            new Qs3dEntityMetadata(
                Guid.NewGuid(),
                ElementKind.Curtain,
                $"{name}-{index + 1}",
                ToCore(panelStart),
                ToCore(panelEnd),
                0,
                0,
                height.Value,
                thickness.Value).Attach(transaction, database, solid);
            selected.Add(solid.ObjectId);
        }
        transaction.Commit();
        editor.SetImpliedSelection(selected.ToArray());
        Qs3dPalette.RefreshBrowser();
        editor.WriteMessage($"\nCreated {panelCount} curtain panel(s) from transient solid preview.\n");
    }

    private static void CreateLinearSolidWithPreview(
        ElementKind kind,
        string layerName,
        short colorIndex,
        string label,
        string widthLabel,
        double defaultWidth,
        string heightLabel,
        double defaultHeight,
        bool isThickness)
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        var database = document.Database;

        var start = editor.GetPoint($"\n{label} start point: ");
        if (start.Status != PromptStatus.OK) return;
        var width = PromptPositive(editor, widthLabel, defaultWidth); if (width is null) return;
        var height = PromptPositive(editor, heightLabel, defaultHeight); if (height is null) return;
        var name = PromptName(editor, $"{label} name", label[..1].ToUpperInvariant()); if (name is null) return;

        IEnumerable<Entity> Preview(Point3d end) =>
            [AutoCadDrawing.CreatePlanOrientedBox(start.Value, end, width.Value, height.Value)];
        Qs3dPreviewAnnotation? Annotation(Point3d end)
        {
            var length = PlanLength(start.Value, end);
            if (length <= Tolerance.Global.EqualPoint)
            {
                return null;
            }
            var section = isThickness ? $"T={width.Value:0.###}" : $"W={width.Value:0.###}";
            return new Qs3dPreviewAnnotation(
                $"L={length:0.###}  {section}  H={height.Value:0.###}  Ang={PlanAngleDegrees(start.Value, end):0.##}deg",
                LiveTextHeight(width.Value, height.Value));
        }

        var jig = new Qs3dPointPreviewJig(
            start.Value,
            $"\n{label} end point: ",
            Preview,
            annotationFactory: Annotation);
        var drag = jig.Drag(editor);
        if (drag.Status != PromptStatus.OK) return;
        var end = jig.Point;

        try
        {
            var finalSolid = AutoCadDrawing.CreatePlanOrientedBox(start.Value, end, width.Value, height.Value);
            using var transaction = database.TransactionManager.StartTransaction();
            var layer = AutoCadDrawing.EnsureLayer(transaction, database, layerName, colorIndex);
            AutoCadDrawing.Append(transaction, database, finalSolid, layer);
            var metadata = isThickness
                ? new Qs3dEntityMetadata(
                    Guid.NewGuid(), kind, name, ToCore(start.Value), ToCore(end),
                    0, 0, height.Value, width.Value)
                : new Qs3dEntityMetadata(
                    Guid.NewGuid(), kind, name, ToCore(start.Value), ToCore(end),
                    width.Value, 0, height.Value, 0);
            metadata.Attach(transaction, database, finalSolid);
            transaction.Commit();
            editor.SetImpliedSelection([finalSolid.ObjectId]);
            Qs3dPalette.RefreshBrowser();
        }
        catch (ArgumentException exception)
        {
            editor.WriteMessage($"\n{exception.Message}\n");
        }
    }

    private static bool TryGetSlabBox(Point3d first, Point3d second, double thickness, out Solid3d solid)
    {
        var x = Math.Abs(second.X - first.X);
        var y = Math.Abs(second.Y - first.Y);
        if (x <= Tolerance.Global.EqualPoint || y <= Tolerance.Global.EqualPoint)
        {
            solid = null!;
            return false;
        }

        var min = new Point3d(
            Math.Min(first.X, second.X),
            Math.Min(first.Y, second.Y),
            first.Z);
        solid = AutoCadDrawing.CreateAxisAlignedBox(min, x, y, thickness);
        return true;
    }

    private static IReadOnlyList<Solid3d> CreateCurtainSolids(
        Point3d start,
        Point3d end,
        double module,
        double thickness,
        double height)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length <= Tolerance.Global.EqualPoint)
        {
            return Array.Empty<Solid3d>();
        }

        var panelCount = Math.Max(1, (int)Math.Ceiling(length / module));
        var solids = new List<Solid3d>(panelCount);
        try
        {
            for (var index = 0; index < panelCount; index++)
            {
                var t0 = (double)index / panelCount;
                var t1 = (double)(index + 1) / panelCount;
                var panelStart = new Point3d(start.X + dx * t0, start.Y + dy * t0, start.Z);
                var panelEnd = new Point3d(start.X + dx * t1, start.Y + dy * t1, start.Z);
                solids.Add(AutoCadDrawing.CreatePlanOrientedBox(panelStart, panelEnd, thickness, height));
            }
            return solids;
        }
        catch
        {
            foreach (var solid in solids) solid.Dispose();
            throw;
        }
    }

    private static double PlanLength(Point3d start, Point3d end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static double PlanAngleDegrees(Point3d start, Point3d end)
    {
        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X) * (180.0 / Math.PI);
        return angle < 0 ? angle + 360.0 : angle;
    }

    private static double LiveTextHeight(params double[] dimensions)
    {
        var reference = dimensions.Where(value => double.IsFinite(value) && value > 0).DefaultIfEmpty(1.0).Min();
        return reference * 0.05;
    }

    private static double? PromptPositive(Editor editor, string label, double defaultValue)
    {
        var options = new PromptDoubleOptions($"\n{label} <{defaultValue:0.###}>: ")
        {
            DefaultValue = defaultValue,
            UseDefaultValue = true,
            AllowNegative = false,
            AllowZero = false
        };
        var result = editor.GetDouble(options);
        return result.Status == PromptStatus.OK ? result.Value : null;
    }

    private static string? PromptName(Editor editor, string label, string fallback)
    {
        var result = editor.GetString(new PromptStringOptions($"\n{label} <{fallback}>: ") { AllowSpaces = true });
        if (result.Status == PromptStatus.Cancel) return null;
        return result.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(result.StringResult)
            ? result.StringResult.Trim()
            : fallback;
    }

    private static Point3 ToCore(Point3d point) => new(point.X, point.Y, point.Z);
}
