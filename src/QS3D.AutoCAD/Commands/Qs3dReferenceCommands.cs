using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using QS3D.AutoCAD.Infrastructure;
using QS3D.AutoCAD.Metadata;
using QS3D.AutoCAD.UI;
using QS3D.Core.Geometry;
using QS3D.Core.Model;
using QS3D.Core.Services;

[assembly: CommandClass(typeof(QS3D.AutoCAD.Commands.Qs3dReferenceCommands))]

namespace QS3D.AutoCAD.Commands;

public sealed class Qs3dReferenceCommands
{
    [CommandMethod("QS3DASSIGNLEVEL", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void AssignLevel()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        var database = document.Database;

        var targetId = ResolveTarget(editor, database, "\nSelect QS3D structural element: ");
        if (targetId.IsNull) return;
        if (!Qs3dDocumentIndex.TryRead(database, targetId, out var target)) return;
        if (!PlacementService.SupportsLevelPlacement(target.Metadata.Kind))
        {
            editor.WriteMessage($"\n{target.Metadata.Kind} cannot be assigned to a Level.\n");
            return;
        }

        var levelId = PromptReference(editor, database, ElementKind.Level, "\nSelect target QS3D Level: ");
        if (levelId.IsNull) return;
        if (!Qs3dDocumentIndex.TryRead(database, levelId, out var level)) return;

        var placed = PlacementService.PlaceOnLevel(target.Metadata.ToCore(), level.Metadata.ToCore());
        var updated = Qs3dEntityMetadata.FromCore(placed);

        using var transaction = database.TransactionManager.StartTransaction();
        if (transaction.GetObject(targetId, OpenMode.ForWrite, false) is not Entity original)
        {
            return;
        }

        var resultingId = ReplaceOrUpdate(transaction, database, original, updated);
        transaction.Commit();
        editor.SetImpliedSelection([resultingId]);
        Qs3dPalette.RefreshBrowser();
        editor.WriteMessage($"\nAssigned {updated.Kind} {updated.Name} to Level {level.Metadata.Name} at Z={updated.Start.Z:0.###}.\n");
    }

    [CommandMethod("QS3DLEVELMOVE", CommandFlags.Modal)]
    public void MoveLevel()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        var database = document.Database;

        var levelId = PromptReference(editor, database, ElementKind.Level, "\nSelect QS3D Level to move: ");
        if (levelId.IsNull || !Qs3dDocumentIndex.TryRead(database, levelId, out var level)) return;

        var elevation = PromptElevation(editor, level.Metadata.Start.Z);
        if (elevation is null) return;
        var delta = elevation.Value - level.Metadata.Start.Z;
        if (Math.Abs(delta) <= 1e-9)
        {
            editor.WriteMessage("\nLevel elevation is unchanged.\n");
            return;
        }

        var indexed = Qs3dDocumentIndex.Scan(database);
        var dependents = indexed
            .Where(item => item.Metadata.Id != level.Metadata.Id && item.Metadata.LevelId == level.Metadata.Id)
            .ToArray();

        using var transaction = database.TransactionManager.StartTransaction();
        if (transaction.GetObject(levelId, OpenMode.ForWrite, false) is not Entity levelEntity)
        {
            return;
        }

        var shiftedLevelCore = PlacementService.ShiftElevation(level.Metadata.ToCore(), delta);
        var shiftedLevel = Qs3dEntityMetadata.FromCore(shiftedLevelCore);
        levelEntity.TransformBy(Matrix3d.Displacement(new Vector3d(0, 0, delta)));
        shiftedLevel.Attach(transaction, database, levelEntity);
        MoveAndUpdateAnnotation(transaction, database, levelEntity.LayerId, level.Metadata, shiftedLevel, delta);

        foreach (var dependent in dependents)
        {
            if (dependent.ObjectId.IsErased ||
                transaction.GetObject(dependent.ObjectId, OpenMode.ForWrite, false) is not Entity entity)
            {
                continue;
            }

            var shifted = Qs3dEntityMetadata.FromCore(PlacementService.ShiftElevation(dependent.Metadata.ToCore(), delta));
            ReplaceOrUpdate(transaction, database, entity, shifted);
        }

        transaction.Commit();
        Qs3dPalette.RefreshBrowser();
        editor.WriteMessage($"\nMoved Level {shiftedLevel.Name} to Z={elevation.Value:0.###} and updated {dependents.Length} dependent element(s).\n");
    }

    [CommandMethod("QS3DBINDGRID", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void BindGrid()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        var database = document.Database;

        var targetId = ResolveTarget(editor, database, "\nSelect QS3D structural element to bind: ");
        if (targetId.IsNull || !Qs3dDocumentIndex.TryRead(database, targetId, out var target)) return;
        if (target.Metadata.Kind is ElementKind.Level or ElementKind.Grid or ElementKind.Section)
        {
            editor.WriteMessage($"\n{target.Metadata.Kind} cannot be bound to structural grids.\n");
            return;
        }

        var startGridId = PromptReference(editor, database, ElementKind.Grid, "\nSelect start/reference Grid: ");
        if (startGridId.IsNull || !Qs3dDocumentIndex.TryRead(database, startGridId, out var startGrid)) return;

        Qs3dIndexedEntity? endGrid = null;
        var endOptions = new PromptEntityOptions("\nSelect end Grid or press Enter for one-grid binding: ") { AllowNone = true };
        var endResult = editor.GetEntity(endOptions);
        if (endResult.Status == PromptStatus.OK)
        {
            if (!Qs3dDocumentIndex.TryRead(database, endResult.ObjectId, out var candidate) || candidate.Metadata.Kind != ElementKind.Grid)
            {
                editor.WriteMessage("\nSelected end reference is not a QS3D Grid.\n");
                return;
            }
            endGrid = candidate;
        }
        else if (endResult.Status != PromptStatus.None)
        {
            return;
        }

        var bound = PlacementService.BindGrids(target.Metadata.ToCore(), startGrid.Metadata.ToCore(), endGrid?.Metadata.ToCore());
        var updated = Qs3dEntityMetadata.FromCore(bound);
        using var transaction = database.TransactionManager.StartTransaction();
        if (transaction.GetObject(targetId, OpenMode.ForWrite, false) is not Entity entity) return;
        updated.Attach(transaction, database, entity);
        transaction.Commit();
        Qs3dPalette.RefreshBrowser();
        editor.WriteMessage($"\nBound {updated.Name} to Grid {startGrid.Metadata.Name}{(endGrid is null ? string.Empty : $" -> {endGrid.Metadata.Name}")}. Geometry is unchanged; the semantic references now participate in dependency checks.\n");
    }

    [CommandMethod("QS3DREFERENCEDELETE", CommandFlags.Modal)]
    public void DeleteReference()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        var database = document.Database;

        var id = ResolveTarget(editor, database, "\nSelect QS3D Level or Grid to delete: ", useImplied: false);
        if (id.IsNull || !Qs3dDocumentIndex.TryRead(database, id, out var reference)) return;
        if (reference.Metadata.Kind is not (ElementKind.Level or ElementKind.Grid))
        {
            editor.WriteMessage("\nOnly QS3D Level or Grid references can be deleted by this command.\n");
            return;
        }

        var indexed = Qs3dDocumentIndex.Scan(database);
        var dependents = PlacementService.FindDependents(indexed.Select(item => item.Metadata.ToCore()), reference.Metadata.Id);
        if (dependents.Count > 0)
        {
            var preview = string.Join(", ", dependents.Take(5).Select(item => item.Name));
            editor.WriteMessage($"\nCannot delete {reference.Metadata.Kind} {reference.Metadata.Name}: {dependents.Count} dependent element(s) reference it ({preview}{(dependents.Count > 5 ? ", ..." : string.Empty)}). Reassign references first.\n");
            return;
        }

        using var transaction = database.TransactionManager.StartTransaction();
        if (transaction.GetObject(id, OpenMode.ForWrite, false) is not Entity entity) return;
        EraseLinkedAnnotation(transaction, database, entity.LayerId, reference.Metadata);
        entity.Erase();
        transaction.Commit();
        Qs3dPalette.RefreshBrowser();
        editor.WriteMessage($"\nDeleted {reference.Metadata.Kind} {reference.Metadata.Name}.\n");
    }

    [CommandMethod("QS3DGRIDARRAY", CommandFlags.Modal)]
    public void CreateGridArray()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        var database = document.Database;

        var start = editor.GetPoint("\nGrid array base start point: ");
        if (start.Status != PromptStatus.OK) return;
        var end = editor.GetPoint(new PromptPointOptions("\nGrid array base end point: ") { BasePoint = start.Value, UseBasePoint = true });
        if (end.Status != PromptStatus.OK) return;
        var dx = end.Value.X - start.Value.X;
        var dy = end.Value.Y - start.Value.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length <= Tolerance.Global.EqualPoint)
        {
            editor.WriteMessage("\nGrid base line must have non-zero plan length.\n");
            return;
        }

        var spacing = PromptPositive(editor, "Grid spacing", 6000);
        if (spacing is null) return;
        var countOptions = new PromptIntegerOptions("\nGrid count <5>: ")
        {
            DefaultValue = 5,
            UseDefaultValue = true,
            LowerLimit = 1,
            UpperLimit = 200
        };
        var countResult = editor.GetInteger(countOptions);
        if (countResult.Status != PromptStatus.OK) return;
        var prefixResult = editor.GetString(new PromptStringOptions("\nGrid prefix <G>: ") { AllowSpaces = false });
        if (prefixResult.Status == PromptStatus.Cancel) return;
        var prefix = prefixResult.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(prefixResult.StringResult)
            ? prefixResult.StringResult.Trim()
            : "G";

        var normalX = -dy / length;
        var normalY = dx / length;
        using var transaction = database.TransactionManager.StartTransaction();
        var layer = AutoCadDrawing.EnsureLayer(transaction, database, "QS3D-GRID", 4);
        for (var index = 0; index < countResult.Value; index++)
        {
            var offset = spacing.Value * index;
            var p0 = new Point3d(start.Value.X + normalX * offset, start.Value.Y + normalY * offset, start.Value.Z);
            var p1 = new Point3d(end.Value.X + normalX * offset, end.Value.Y + normalY * offset, end.Value.Z);
            var name = $"{prefix}{index + 1}";
            var semanticId = Guid.NewGuid();
            var line = new Line(p0, p1);
            AutoCadDrawing.Append(transaction, database, line, layer);
            var text = new DBText { Position = p0, Height = 250, TextString = name };
            AutoCadDrawing.Append(transaction, database, text, layer);
            new Qs3dEntityMetadata(semanticId, ElementKind.Grid, name, ToCore(p0), ToCore(p1), 0, 0, 0, 0).Attach(transaction, database, line);
            Qs3dVisualLink.Attach(transaction, database, text, semanticId);
        }

        transaction.Commit();
        Qs3dPalette.RefreshBrowser();
        editor.WriteMessage($"\nCreated {countResult.Value} parallel Grid(s) at spacing {spacing.Value:0.###}.\n");
    }

    [CommandMethod("QS3DREFERENCES", CommandFlags.Modal)]
    public void ListReferences()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        var indexed = Qs3dDocumentIndex.Scan(document.Database);
        var core = indexed.Select(item => item.Metadata.ToCore()).ToArray();
        var references = indexed.Where(item => item.Metadata.Kind is ElementKind.Level or ElementKind.Grid).ToArray();

        editor.WriteMessage($"\nQS3D references — {references.Length} Level/Grid item(s)\n");
        foreach (var reference in references)
        {
            var dependentCount = PlacementService.FindDependents(core, reference.Metadata.Id).Count;
            var suffix = reference.Metadata.Kind == ElementKind.Level ? $" Z={reference.Metadata.Start.Z:0.###}" : string.Empty;
            editor.WriteMessage($"  {reference.Metadata.Kind,-5} {reference.Metadata.Name}{suffix} | dependents={dependentCount}\n");
        }
    }

    private static ObjectId ResolveTarget(Editor editor, Database database, string prompt, bool useImplied = true)
    {
        if (useImplied)
        {
            var implied = editor.SelectImplied();
            if (implied.Status == PromptStatus.OK && implied.Value.Count == 1)
            {
                var id = implied.Value.GetObjectIds()[0];
                if (Qs3dDocumentIndex.TryRead(database, id, out _)) return id;
            }
        }

        var result = editor.GetEntity(new PromptEntityOptions(prompt));
        if (result.Status != PromptStatus.OK) return ObjectId.Null;
        if (!Qs3dDocumentIndex.TryRead(database, result.ObjectId, out _))
        {
            editor.WriteMessage("\nSelected object is not a QS3D-owned entity.\n");
            return ObjectId.Null;
        }
        return result.ObjectId;
    }

    private static ObjectId PromptReference(Editor editor, Database database, ElementKind kind, string prompt)
    {
        var result = editor.GetEntity(new PromptEntityOptions(prompt));
        if (result.Status != PromptStatus.OK) return ObjectId.Null;
        if (!Qs3dDocumentIndex.TryRead(database, result.ObjectId, out var indexed) || indexed.Metadata.Kind != kind)
        {
            editor.WriteMessage($"\nSelected object is not a QS3D {kind}.\n");
            return ObjectId.Null;
        }
        return result.ObjectId;
    }

    private static ObjectId ReplaceOrUpdate(Transaction transaction, Database database, Entity original, Qs3dEntityMetadata updated)
    {
        if (!Qs3dGeometryFactory.IsSolid(updated.Kind))
        {
            updated.Attach(transaction, database, original);
            return original.ObjectId;
        }

        var replacement = Qs3dGeometryFactory.CreateSolid(updated);
        var id = AutoCadDrawing.Append(transaction, database, replacement, original.LayerId);
        updated.Attach(transaction, database, replacement);
        original.Erase();
        return id;
    }

    private static void MoveAndUpdateAnnotation(
        Transaction transaction,
        Database database,
        ObjectId layerId,
        Qs3dEntityMetadata original,
        Qs3dEntityMetadata updated,
        double delta)
    {
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
        var anchor = ToAcad(original.Start);
        foreach (ObjectId id in space)
        {
            if (transaction.GetObject(id, OpenMode.ForRead, false) is not DBText text) continue;
            var linked = Qs3dVisualLink.TryReadParentId(text, out var parentId) && parentId == original.Id;
            var legacyMatch = !linked && text.LayerId == layerId && text.Position.DistanceTo(anchor) <= 1e-6;
            if (!linked && !legacyMatch) continue;

            text.UpgradeOpen();
            text.TransformBy(Matrix3d.Displacement(new Vector3d(0, 0, delta)));
            text.TextString = $"{updated.Name}  EL={updated.Start.Z:0.###}";
            if (!linked) Qs3dVisualLink.Attach(transaction, database, text, original.Id);
            return;
        }
    }

    private static void EraseLinkedAnnotation(
        Transaction transaction,
        Database database,
        ObjectId layerId,
        Qs3dEntityMetadata reference)
    {
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
        var anchor = ToAcad(reference.Start);
        foreach (ObjectId id in space)
        {
            if (transaction.GetObject(id, OpenMode.ForRead, false) is not DBText text) continue;
            var linked = Qs3dVisualLink.TryReadParentId(text, out var parentId) && parentId == reference.Id;
            var legacyMatch = !linked && text.LayerId == layerId && text.Position.DistanceTo(anchor) <= 1e-6;
            if (!linked && !legacyMatch) continue;
            text.UpgradeOpen();
            text.Erase();
            return;
        }
    }

    private static double? PromptElevation(Editor editor, double defaultValue)
    {
        var options = new PromptDoubleOptions($"\nNew Level elevation <{defaultValue:0.###}>: ")
        {
            DefaultValue = defaultValue,
            UseDefaultValue = true,
            AllowNegative = true,
            AllowZero = true
        };
        var result = editor.GetDouble(options);
        return result.Status == PromptStatus.OK ? result.Value : null;
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

    private static Point3 ToCore(Point3d point) => new(point.X, point.Y, point.Z);
    private static Point3d ToAcad(Point3 point) => new(point.X, point.Y, point.Z);
}
