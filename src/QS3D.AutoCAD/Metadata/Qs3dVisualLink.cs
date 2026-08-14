using Autodesk.AutoCAD.DatabaseServices;

namespace QS3D.AutoCAD.Metadata;

internal static class Qs3dVisualLink
{
    private const string RegAppName = "QS3D_LINK";

    public static void Attach(Transaction transaction, Database database, Entity entity, Guid parentId)
    {
        EnsureRegApp(transaction, database);
        entity.XData = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegAppName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, parentId.ToString("N")));
    }

    public static bool TryReadParentId(Entity entity, out Guid parentId)
    {
        parentId = Guid.Empty;
        using var data = entity.GetXDataForApplication(RegAppName);
        if (data is null)
        {
            return false;
        }

        var values = data.AsArray();
        return values.Length >= 2 &&
               values[1].Value is string idText &&
               Guid.TryParseExact(idText, "N", out parentId);
    }

    private static void EnsureRegApp(Transaction transaction, Database database)
    {
        var table = (RegAppTable)transaction.GetObject(database.RegAppTableId, OpenMode.ForRead);
        if (table.Has(RegAppName))
        {
            return;
        }

        table.UpgradeOpen();
        var record = new RegAppTableRecord { Name = RegAppName };
        table.Add(record);
        transaction.AddNewlyCreatedDBObject(record, true);
    }
}
