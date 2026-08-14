using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using QS3D.AutoCAD.Infrastructure;
using QS3D.AutoCAD.Metadata;
using QS3D.AutoCAD.UI;

[assembly: CommandClass(typeof(QS3D.AutoCAD.Commands.Qs3dReferenceClearCommands))]

namespace QS3D.AutoCAD.Commands;

public sealed class Qs3dReferenceClearCommands
{
    [CommandMethod("QS3DCLEARREFS", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void ClearReferences()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        var database = document.Database;

        var objectId = ResolveTarget(editor, database);
        if (objectId.IsNull || !Qs3dDocumentIndex.TryRead(database, objectId, out var indexed)) return;

        var options = new PromptKeywordOptions("\nClear placement references [Level/Grid/All] <All>: ");
        options.Keywords.Add("Level");
        options.Keywords.Add("Grid");
        options.Keywords.Add("All");
        options.Keywords.Default = "All";
        options.AllowNone = true;
        var result = editor.GetKeywords(options);
        if (result.Status is not (PromptStatus.OK or PromptStatus.None)) return;
        var choice = result.Status == PromptStatus.None ? "All" : result.StringResult;

        var updated = choice switch
        {
            "Level" => indexed.Metadata with { LevelId = null },
            "Grid" => indexed.Metadata with { StartGridId = null, EndGridId = null },
            _ => indexed.Metadata with { LevelId = null, StartGridId = null, EndGridId = null }
        };

        using var transaction = database.TransactionManager.StartTransaction();
        if (transaction.GetObject(objectId, OpenMode.ForWrite, false) is not Entity entity) return;
        updated.Attach(transaction, database, entity);
        transaction.Commit();
        Qs3dPalette.RefreshBrowser();
        editor.WriteMessage($"\nCleared {choice.ToLowerInvariant()} placement references from {updated.Name}; geometry was not moved.\n");
    }

    private static ObjectId ResolveTarget(Editor editor, Database database)
    {
        var implied = editor.SelectImplied();
        if (implied.Status == PromptStatus.OK && implied.Value.Count == 1)
        {
            var id = implied.Value.GetObjectIds()[0];
            if (Qs3dDocumentIndex.TryRead(database, id, out _)) return id;
        }

        var result = editor.GetEntity(new PromptEntityOptions("\nSelect QS3D element: "));
        if (result.Status != PromptStatus.OK || !Qs3dDocumentIndex.TryRead(database, result.ObjectId, out _))
        {
            return ObjectId.Null;
        }
        return result.ObjectId;
    }
}
