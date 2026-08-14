using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using QS3D.AutoCAD.Infrastructure;
using QS3D.AutoCAD.Metadata;
using QS3D.AutoCAD.UI;
using QS3D.Core.Model;
using QS3D.Core.Services;

[assembly: CommandClass(typeof(QS3D.AutoCAD.Commands.Qs3dGridSnapCommands))]

namespace QS3D.AutoCAD.Commands;

public sealed class Qs3dGridSnapCommands
{
    [CommandMethod("QS3DGRIDSNAP", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void SnapToBoundGrids()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        var database = document.Database;

        var targetId = ResolveTarget(editor, database);
        if (targetId.IsNull || !Qs3dDocumentIndex.TryRead(database, targetId, out var target)) return;
        if (!Qs3dGeometryFactory.IsSolid(target.Metadata.Kind))
        {
            editor.WriteMessage($"\n{target.Metadata.Kind} does not support Grid geometry snapping.\n");
            return;
        }
        if (target.Metadata.StartGridId is null)
        {
            editor.WriteMessage("\nElement has no Grid binding. Run QS3DBINDGRID first.\n");
            return;
        }

        var indexed = Qs3dDocumentIndex.Scan(database);
        var startGrid = FindGrid(indexed, target.Metadata.StartGridId.Value);
        if (startGrid is null)
        {
            editor.WriteMessage("\nStart Grid reference is missing or no longer points to a QS3D Grid.\n");
            return;
        }

        Qs3dIndexedEntity? endGrid = null;
        if (target.Metadata.EndGridId is Guid endGridId)
        {
            endGrid = FindGrid(indexed, endGridId);
            if (endGrid is null)
            {
                editor.WriteMessage("\nEnd Grid reference is missing or no longer points to a QS3D Grid.\n");
                return;
            }
        }

        Qs3dEntityMetadata updated;
        try
        {
            var snapped = PlacementService.SnapToGrids(
                target.Metadata.ToCore(),
                startGrid.Metadata.ToCore(),
                endGrid?.Metadata.ToCore());
            updated = Qs3dEntityMetadata.FromCore(snapped);
        }
        catch (System.Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            editor.WriteMessage($"\nGrid snap rejected: {exception.Message}\n");
            return;
        }

        using var transaction = database.TransactionManager.StartTransaction();
        if (transaction.GetObject(targetId, OpenMode.ForWrite, false) is not Entity original)
        {
            return;
        }

        Solid3d replacement;
        try
        {
            replacement = Qs3dGeometryFactory.CreateSolid(updated);
        }
        catch (System.Exception exception) when (exception is ArgumentException or InvalidOperationException or Autodesk.AutoCAD.Runtime.Exception)
        {
            editor.WriteMessage($"\nGrid snap could not rebuild geometry: {exception.Message}\n");
            return;
        }

        var resultingId = AutoCadDrawing.Append(transaction, database, replacement, original.LayerId);
        updated.Attach(transaction, database, replacement);
        original.Erase();
        transaction.Commit();

        editor.SetImpliedSelection([resultingId]);
        Qs3dPalette.RefreshBrowser();
        editor.WriteMessage($"\nSnapped {updated.Kind} {updated.Name} to Grid {startGrid.Metadata.Name}{(endGrid is null ? string.Empty : $" -> {endGrid.Metadata.Name}")} and rebuilt QS3D geometry/metadata together.\n");
    }

    private static Qs3dIndexedEntity? FindGrid(IReadOnlyList<Qs3dIndexedEntity> indexed, Guid semanticId) =>
        indexed.FirstOrDefault(item => item.Metadata.Id == semanticId && item.Metadata.Kind == ElementKind.Grid);

    private static ObjectId ResolveTarget(Editor editor, Database database)
    {
        var implied = editor.SelectImplied();
        if (implied.Status == PromptStatus.OK && implied.Value.Count == 1)
        {
            var impliedId = implied.Value.GetObjectIds()[0];
            if (Qs3dDocumentIndex.TryRead(database, impliedId, out _)) return impliedId;
        }

        var result = editor.GetEntity(new PromptEntityOptions("\nSelect QS3D structural element to snap to bound Grid(s): "));
        if (result.Status != PromptStatus.OK) return ObjectId.Null;
        if (!Qs3dDocumentIndex.TryRead(database, result.ObjectId, out _))
        {
            editor.WriteMessage("\nSelected object is not a QS3D-owned entity.\n");
            return ObjectId.Null;
        }
        return result.ObjectId;
    }
}
