using Autodesk.AutoCAD.DatabaseServices;
using QS3D.AutoCAD.Metadata;

namespace QS3D.AutoCAD.Infrastructure;

internal sealed record Qs3dIndexedEntity(ObjectId ObjectId, string Handle, Qs3dEntityMetadata Metadata);

internal static class Qs3dDocumentIndex
{
    public static IReadOnlyList<Qs3dIndexedEntity> Scan(Database database)
    {
        var result = new List<Qs3dIndexedEntity>();
        using var transaction = database.TransactionManager.StartOpenCloseTransaction();
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);

        foreach (ObjectId id in space)
        {
            if (transaction.GetObject(id, OpenMode.ForRead) is not Entity entity ||
                !Qs3dEntityMetadata.TryRead(entity, out var metadata))
            {
                continue;
            }

            result.Add(new Qs3dIndexedEntity(id, id.Handle.ToString(), metadata));
        }

        return result
            .OrderBy(item => item.Metadata.Kind)
            .ThenBy(item => item.Metadata.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static bool TryRead(Database database, ObjectId id, out Qs3dIndexedEntity indexed)
    {
        indexed = null!;
        if (id.IsNull || id.IsErased || !id.IsValid)
        {
            return false;
        }

        using var transaction = database.TransactionManager.StartOpenCloseTransaction();
        if (transaction.GetObject(id, OpenMode.ForRead, false) is not Entity entity ||
            !Qs3dEntityMetadata.TryRead(entity, out var metadata))
        {
            return false;
        }

        indexed = new Qs3dIndexedEntity(id, id.Handle.ToString(), metadata);
        return true;
    }
}
