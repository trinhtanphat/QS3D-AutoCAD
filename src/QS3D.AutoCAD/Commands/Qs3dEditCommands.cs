using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using QS3D.AutoCAD.Infrastructure;
using QS3D.AutoCAD.Metadata;
using QS3D.AutoCAD.UI;
using QS3D.Core.Geometry;
using QS3D.Core.Model;

[assembly: CommandClass(typeof(QS3D.AutoCAD.Commands.Qs3dEditCommands))]

namespace QS3D.AutoCAD.Commands;

public sealed class Qs3dEditCommands
{
    [CommandMethod("QS3DREFRESH", CommandFlags.Modal)]
    public void RefreshBrowser() => Qs3dPalette.RefreshBrowser();

    [CommandMethod("QS3DEDIT", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void EditElement()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var editor = document.Editor;
        var objectId = ResolveSingleEntity(editor, document.Database);
        if (objectId.IsNull)
        {
            return;
        }

        if (!Qs3dDocumentIndex.TryRead(document.Database, objectId, out var indexed))
        {
            editor.WriteMessage("\nSelected object is not a QS3D-owned entity.\n");
            return;
        }

        var originalMetadata = indexed.Metadata;
        var name = PromptName(editor, "Name", originalMetadata.Name);
        if (name is null)
        {
            return;
        }

        var updatedMetadata = PromptDimensions(editor, originalMetadata, name);
        if (updatedMetadata is null)
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        if (transaction.GetObject(objectId, OpenMode.ForWrite, false) is not Entity originalEntity)
        {
            return;
        }

        ObjectId resultingId;
        if (NeedsSolidRebuild(updatedMetadata.Kind))
        {
            var replacement = CreateReplacementSolid(updatedMetadata);
            resultingId = AutoCadDrawing.Append(transaction, document.Database, replacement, originalEntity.LayerId);
            updatedMetadata.Attach(transaction, document.Database, replacement);
            originalEntity.Erase();
        }
        else
        {
            updatedMetadata.Attach(transaction, document.Database, originalEntity);
            UpdateMarkerAnnotation(transaction, document.Database, originalEntity.LayerId, updatedMetadata);
            resultingId = originalEntity.ObjectId;
        }

        transaction.Commit();
        editor.SetImpliedSelection([resultingId]);
        editor.WriteMessage($"\nUpdated {updatedMetadata.Kind} {updatedMetadata.Name}.\n");
    }

    private static ObjectId ResolveSingleEntity(Editor editor, Database database)
    {
        var implied = editor.SelectImplied();
        if (implied.Status == PromptStatus.OK && implied.Value.Count == 1)
        {
            var id = implied.Value.GetObjectIds()[0];
            if (Qs3dDocumentIndex.TryRead(database, id, out _))
            {
                return id;
            }
        }

        var options = new PromptEntityOptions("\nSelect a QS3D element: ");
        var result = editor.GetEntity(options);
        return result.Status == PromptStatus.OK ? result.ObjectId : ObjectId.Null;
    }

    private static Qs3dEntityMetadata? PromptDimensions(Editor editor, Qs3dEntityMetadata metadata, string name)
    {
        switch (metadata.Kind)
        {
            case ElementKind.Column:
            {
                var width = PromptPositive(editor, "Width", metadata.Width); if (width is null) return null;
                var depth = PromptPositive(editor, "Depth", metadata.Depth); if (depth is null) return null;
                var height = PromptPositive(editor, "Height", metadata.Height); if (height is null) return null;
                return metadata with { Name = name, Width = width.Value, Depth = depth.Value, Height = height.Value };
            }
            case ElementKind.Beam:
            {
                var width = PromptPositive(editor, "Width", metadata.Width); if (width is null) return null;
                var height = PromptPositive(editor, "Height", metadata.Height); if (height is null) return null;
                return metadata with { Name = name, Width = width.Value, Height = height.Value };
            }
            case ElementKind.Slab:
            {
                var thickness = PromptPositive(editor, "Thickness", metadata.Thickness); if (thickness is null) return null;
                return metadata with { Name = name, Thickness = thickness.Value };
            }
            case ElementKind.Wall:
            case ElementKind.Curtain:
            {
                var thickness = PromptPositive(editor, "Thickness", metadata.Thickness); if (thickness is null) return null;
                var height = PromptPositive(editor, "Height", metadata.Height); if (height is null) return null;
                return metadata with { Name = name, Thickness = thickness.Value, Height = height.Value };
            }
            case ElementKind.Level:
            case ElementKind.Grid:
            case ElementKind.Section:
                return metadata with { Name = name };
            default:
                editor.WriteMessage($"\nEditing is not supported for {metadata.Kind}.\n");
                return null;
        }
    }

    private static bool NeedsSolidRebuild(ElementKind kind) => kind is
        ElementKind.Column or ElementKind.Beam or ElementKind.Slab or ElementKind.Wall or ElementKind.Curtain;

    private static Solid3d CreateReplacementSolid(Qs3dEntityMetadata metadata)
    {
        var start = ToAcad(metadata.Start);
        var end = ToAcad(metadata.End);
        return metadata.Kind switch
        {
            ElementKind.Column => AutoCadDrawing.CreateAxisAlignedBox(start, metadata.Width, metadata.Depth, metadata.Height),
            ElementKind.Beam => AutoCadDrawing.CreatePlanOrientedBox(start, end, metadata.Width, metadata.Height),
            ElementKind.Wall or ElementKind.Curtain => AutoCadDrawing.CreatePlanOrientedBox(start, end, metadata.Thickness, metadata.Height),
            ElementKind.Slab => CreateSlab(metadata),
            _ => throw new InvalidOperationException($"Cannot rebuild {metadata.Kind} as a solid.")
        };
    }

    private static Solid3d CreateSlab(Qs3dEntityMetadata metadata)
    {
        var min = new Point3d(
            Math.Min(metadata.Start.X, metadata.End.X),
            Math.Min(metadata.Start.Y, metadata.End.Y),
            Math.Min(metadata.Start.Z, metadata.End.Z));
        var x = Math.Abs(metadata.End.X - metadata.Start.X);
        var y = Math.Abs(metadata.End.Y - metadata.Start.Y);
        if (x <= Tolerance.Global.EqualPoint || y <= Tolerance.Global.EqualPoint)
        {
            throw new InvalidOperationException("Stored slab geometry has a zero plan dimension.");
        }

        return AutoCadDrawing.CreateAxisAlignedBox(min, x, y, metadata.Thickness);
    }

    private static void UpdateMarkerAnnotation(
        Transaction transaction,
        Database database,
        ObjectId layerId,
        Qs3dEntityMetadata metadata)
    {
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
        var anchor = ToAcad(metadata.Start);
        foreach (ObjectId id in space)
        {
            if (transaction.GetObject(id, OpenMode.ForRead, false) is not DBText text ||
                text.LayerId != layerId ||
                text.Position.DistanceTo(anchor) > 1e-6)
            {
                continue;
            }

            text.UpgradeOpen();
            text.TextString = metadata.Kind == ElementKind.Level
                ? $"{metadata.Name}  EL={metadata.Start.Z:0.###}"
                : metadata.Name;
            return;
        }
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
        if (result.Status == PromptStatus.Cancel)
        {
            return null;
        }

        return result.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(result.StringResult)
            ? result.StringResult.Trim()
            : fallback;
    }

    private static Point3d ToAcad(Point3 point) => new(point.X, point.Y, point.Z);
}
