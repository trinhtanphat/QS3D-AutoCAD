using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using QS3D.AutoCAD.Infrastructure;
using QS3D.AutoCAD.Metadata;
using QS3D.AutoCAD.UI;
using QS3D.Core.Model;
using QS3D.Core.Services;

[assembly: CommandClass(typeof(QS3D.AutoCAD.Commands.Qs3dReferenceManagerCommands))]

namespace QS3D.AutoCAD.Commands;

public sealed class Qs3dReferenceManagerCommands
{
    [CommandMethod("QS3DREFERENCERENAME", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void RenameReference()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        var database = document.Database;

        var referenceId = ResolveReference(editor, database, "\nSelect QS3D Level or Grid to rename: ");
        if (referenceId.IsNull || !Qs3dDocumentIndex.TryRead(database, referenceId, out var reference)) return;

        var newName = PromptName(editor, $"{reference.Metadata.Kind} name", reference.Metadata.Name);
        if (newName is null || string.Equals(newName, reference.Metadata.Name, StringComparison.Ordinal)) return;

        var indexed = Qs3dDocumentIndex.Scan(database);
        if (HasNameCollision(indexed, reference.Metadata.Kind, newName, reference.Metadata.Id))
        {
            editor.WriteMessage($"\nA QS3D {reference.Metadata.Kind} named '{newName}' already exists.\n");
            return;
        }

        var updated = Qs3dEntityMetadata.FromCore(ReferenceManagerService.RenameReference(reference.Metadata.ToCore(), newName));
        using var transaction = database.TransactionManager.StartTransaction();
        if (transaction.GetObject(referenceId, OpenMode.ForWrite, false) is not Entity entity) return;
        updated.Attach(transaction, database, entity);
        UpdateLinkedAnnotation(transaction, database, entity.LayerId, reference.Metadata, updated);
        transaction.Commit();

        editor.SetImpliedSelection([referenceId]);
        Qs3dPalette.RefreshBrowser();
        editor.WriteMessage($"\nRenamed {updated.Kind} '{reference.Metadata.Name}' -> '{updated.Name}'. Semantic id {updated.Id:D} and all dependent references were preserved.\n");
    }

    [CommandMethod("QS3DLEVELSEQUENCE", CommandFlags.Modal)]
    public void SequenceLevels()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        var database = document.Database;

        var indexed = Qs3dDocumentIndex.Scan(database);
        var levels = indexed.Where(item => item.Metadata.Kind == ElementKind.Level).ToArray();
        if (levels.Length == 0)
        {
            editor.WriteMessage("\nNo QS3D Levels found.\n");
            return;
        }

        var directionOptions = new PromptKeywordOptions("\nLevel sequence direction [Ascending/Descending] <Ascending>: ")
        {
            AllowNone = true
        };
        directionOptions.Keywords.Add("Ascending");
        directionOptions.Keywords.Add("Descending");
        var directionResult = editor.GetKeywords(directionOptions);
        if (directionResult.Status == PromptStatus.Cancel) return;
        var descending = directionResult.Status == PromptStatus.OK &&
                         string.Equals(directionResult.StringResult, "Descending", StringComparison.OrdinalIgnoreCase);

        var prefix = PromptName(editor, "Level prefix", "L");
        if (prefix is null) return;
        var start = PromptPositiveInteger(editor, "Starting number", 1, 1, 999999);
        if (start is null) return;
        var digits = PromptPositiveInteger(editor, "Minimum number digits", 2, 1, 6);
        if (digits is null) return;

        var orderedCore = ReferenceManagerService.OrderLevels(levels.Select(item => item.Metadata.ToCore()), descending);
        var byId = levels.ToDictionary(item => item.Metadata.Id);
        var plan = orderedCore
            .Select((level, index) => new RenamePlan(byId[level.Id], $"{prefix}{(start.Value + index).ToString($"D{digits.Value}")}"))
            .ToArray();

        using var transaction = database.TransactionManager.StartTransaction();
        foreach (var item in plan)
        {
            ApplyRename(transaction, database, item.Reference, item.Name);
        }
        transaction.Commit();

        Qs3dPalette.RefreshBrowser();
        editor.WriteMessage($"\nResequenced {plan.Length} Level(s) by {(descending ? "descending" : "ascending")} elevation from {plan[0].Name} to {plan[^1].Name}. Elevations, semantic ids and dependencies are unchanged.\n");
    }

    [CommandMethod("QS3DGRIDSEQUENCE", CommandFlags.Modal)]
    public void SequenceParallelGridFamily()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        var database = document.Database;

        var seedId = ResolveReference(editor, database, "\nSelect a QS3D Grid in the family to resequence: ", ElementKind.Grid);
        if (seedId.IsNull || !Qs3dDocumentIndex.TryRead(database, seedId, out var seed)) return;

        var indexed = Qs3dDocumentIndex.Scan(database);
        var allGrids = indexed.Where(item => item.Metadata.Kind == ElementKind.Grid).ToArray();
        IReadOnlyList<Qs3dIndexedEntity> family;
        try
        {
            var coreFamily = ReferenceManagerService.SelectParallelGridFamily(
                allGrids.Select(item => item.Metadata.ToCore()),
                seed.Metadata.ToCore());
            var familyIds = coreFamily.Select(item => item.Id).ToHashSet();
            family = allGrids.Where(item => familyIds.Contains(item.Metadata.Id)).ToArray();
        }
        catch (ArgumentException exception)
        {
            editor.WriteMessage($"\nGrid family cannot be sequenced: {exception.Message}\n");
            return;
        }

        IReadOnlyList<StructuralElement> orderedCore;
        try
        {
            orderedCore = ReferenceManagerService.OrderParallelGrids(family.Select(item => item.Metadata.ToCore()));
        }
        catch (InvalidOperationException exception)
        {
            editor.WriteMessage($"\nGrid family cannot be sequenced: {exception.Message}\n");
            return;
        }

        var styleOptions = new PromptKeywordOptions("\nGrid sequence style [Numeric/Alphabetic] <Numeric>: ")
        {
            AllowNone = true
        };
        styleOptions.Keywords.Add("Numeric");
        styleOptions.Keywords.Add("Alphabetic");
        var styleResult = editor.GetKeywords(styleOptions);
        if (styleResult.Status == PromptStatus.Cancel) return;
        var alphabetic = styleResult.Status == PromptStatus.OK &&
                         string.Equals(styleResult.StringResult, "Alphabetic", StringComparison.OrdinalIgnoreCase);

        var prefix = PromptName(editor, "Grid prefix", alphabetic ? string.Empty : "G", allowEmpty: true);
        if (prefix is null) return;
        var start = PromptPositiveInteger(editor, alphabetic ? "Starting alphabet index (1=A)" : "Starting number", 1, 1, 999999);
        if (start is null) return;
        var digits = alphabetic ? 0 : PromptPositiveInteger(editor, "Minimum number digits", 1, 1, 6);
        if (!alphabetic && digits is null) return;

        var byId = family.ToDictionary(item => item.Metadata.Id);
        var plan = orderedCore.Select((grid, index) => new RenamePlan(
            byId[grid.Id],
            alphabetic
                ? $"{prefix}{ToAlphabetic(start.Value + index)}"
                : $"{prefix}{(start.Value + index).ToString($"D{digits!.Value}")}"))
            .ToArray();

        var outsideFamily = allGrids.Where(item => plan.All(rename => rename.Reference.Metadata.Id != item.Metadata.Id)).ToArray();
        foreach (var item in plan)
        {
            if (HasNameCollision(outsideFamily, ElementKind.Grid, item.Name, Guid.Empty))
            {
                editor.WriteMessage($"\nGrid resequence would collide with existing Grid '{item.Name}' outside the selected parallel family. No changes made.\n");
                return;
            }
        }

        using var transaction = database.TransactionManager.StartTransaction();
        foreach (var item in plan)
        {
            ApplyRename(transaction, database, item.Reference, item.Name);
        }
        transaction.Commit();

        Qs3dPalette.RefreshBrowser();
        editor.WriteMessage($"\nResequenced {plan.Length} parallel Grid(s) by spatial offset from {plan[0].Name} to {plan[^1].Name}. Geometry, semantic ids and dependent bindings are unchanged.\n");
    }

    private static void ApplyRename(
        Transaction transaction,
        Database database,
        Qs3dIndexedEntity reference,
        string name)
    {
        if (transaction.GetObject(reference.ObjectId, OpenMode.ForWrite, false) is not Entity entity)
        {
            throw new InvalidOperationException($"Could not open {reference.Metadata.Kind} {reference.Metadata.Name} for rename.");
        }

        var updated = Qs3dEntityMetadata.FromCore(ReferenceManagerService.RenameReference(reference.Metadata.ToCore(), name));
        updated.Attach(transaction, database, entity);
        UpdateLinkedAnnotation(transaction, database, entity.LayerId, reference.Metadata, updated);
    }

    private static void UpdateLinkedAnnotation(
        Transaction transaction,
        Database database,
        ObjectId layerId,
        Qs3dEntityMetadata original,
        Qs3dEntityMetadata updated)
    {
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
        foreach (ObjectId id in space)
        {
            if (transaction.GetObject(id, OpenMode.ForRead, false) is not DBText text) continue;
            var linked = Qs3dVisualLink.TryReadParentId(text, out var parentId) && parentId == original.Id;
            var legacyMatch = !linked && text.LayerId == layerId &&
                              Math.Abs(text.Position.X - original.Start.X) <= 1e-6 &&
                              Math.Abs(text.Position.Y - original.Start.Y) <= 1e-6 &&
                              Math.Abs(text.Position.Z - original.Start.Z) <= 1e-6;
            if (!linked && !legacyMatch) continue;

            text.UpgradeOpen();
            text.TextString = updated.Kind == ElementKind.Level
                ? $"{updated.Name}  EL={updated.Start.Z:0.###}"
                : updated.Name;
            if (!linked) Qs3dVisualLink.Attach(transaction, database, text, original.Id);
            return;
        }
    }

    private static ObjectId ResolveReference(
        Editor editor,
        Database database,
        string prompt,
        ElementKind? requiredKind = null)
    {
        var implied = editor.SelectImplied();
        if (implied.Status == PromptStatus.OK && implied.Value.Count == 1)
        {
            var id = implied.Value.GetObjectIds()[0];
            if (Qs3dDocumentIndex.TryRead(database, id, out var impliedReference) &&
                impliedReference.Metadata.Kind is ElementKind.Level or ElementKind.Grid &&
                (requiredKind is null || impliedReference.Metadata.Kind == requiredKind))
            {
                return id;
            }
        }

        var result = editor.GetEntity(new PromptEntityOptions(prompt));
        if (result.Status != PromptStatus.OK) return ObjectId.Null;
        if (!Qs3dDocumentIndex.TryRead(database, result.ObjectId, out var reference) ||
            reference.Metadata.Kind is not (ElementKind.Level or ElementKind.Grid) ||
            (requiredKind is not null && reference.Metadata.Kind != requiredKind))
        {
            editor.WriteMessage(requiredKind is null
                ? "\nSelected object is not a QS3D Level/Grid.\n"
                : $"\nSelected object is not a QS3D {requiredKind}.\n");
            return ObjectId.Null;
        }
        return result.ObjectId;
    }

    private static bool HasNameCollision(
        IEnumerable<Qs3dIndexedEntity> indexed,
        ElementKind kind,
        string name,
        Guid excludeId) =>
        indexed.Any(item => item.Metadata.Kind == kind &&
                            item.Metadata.Id != excludeId &&
                            string.Equals(item.Metadata.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string? PromptName(Editor editor, string label, string fallback, bool allowEmpty = false)
    {
        var suffix = string.IsNullOrEmpty(fallback) ? string.Empty : $" <{fallback}>";
        var result = editor.GetString(new PromptStringOptions($"\n{label}{suffix}: ") { AllowSpaces = true });
        if (result.Status == PromptStatus.Cancel) return null;
        if (result.Status == PromptStatus.OK)
        {
            var value = result.StringResult.Trim();
            if (!string.IsNullOrEmpty(value) || allowEmpty) return value;
        }
        return fallback;
    }

    private static int? PromptPositiveInteger(Editor editor, string label, int defaultValue, int lower, int upper)
    {
        var options = new PromptIntegerOptions($"\n{label} <{defaultValue}>: ")
        {
            DefaultValue = defaultValue,
            UseDefaultValue = true,
            LowerLimit = lower,
            UpperLimit = upper
        };
        var result = editor.GetInteger(options);
        return result.Status == PromptStatus.OK ? result.Value : null;
    }

    private static string ToAlphabetic(int value)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        var result = string.Empty;
        while (value > 0)
        {
            value--;
            result = (char)('A' + (value % 26)) + result;
            value /= 26;
        }
        return result;
    }

    private sealed record RenamePlan(Qs3dIndexedEntity Reference, string Name);
}
