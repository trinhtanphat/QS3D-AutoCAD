using Autodesk.AutoCAD.DatabaseServices;

namespace QS3D.AutoCAD.Persistence;

internal sealed record DwgProjectState(Guid Id, string Name);

internal static class DwgProjectStore
{
    private const string DictionaryKey = "QS3D_PROJECT";

    public static DwgProjectState GetOrCreate(Database database, string? requestedName = null)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        var dictionary = (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);
        DwgProjectState state;

        if (dictionary.Contains(DictionaryKey))
        {
            var xrecord = (Xrecord)transaction.GetObject(dictionary.GetAt(DictionaryKey), OpenMode.ForWrite);
            state = Parse(xrecord.Data) ?? new DwgProjectState(Guid.NewGuid(), "QS3D Project");
            if (!string.IsNullOrWhiteSpace(requestedName) && !string.Equals(state.Name, requestedName, StringComparison.Ordinal))
            {
                state = state with { Name = requestedName!.Trim() };
                xrecord.Data = Serialize(state);
            }
        }
        else
        {
            dictionary.UpgradeOpen();
            state = new DwgProjectState(Guid.NewGuid(), string.IsNullOrWhiteSpace(requestedName) ? "QS3D Project" : requestedName!.Trim());
            var xrecord = new Xrecord { Data = Serialize(state) };
            dictionary.SetAt(DictionaryKey, xrecord);
            transaction.AddNewlyCreatedDBObject(xrecord, true);
        }

        transaction.Commit();
        return state;
    }

    private static ResultBuffer Serialize(DwgProjectState state) => new(
        new TypedValue((int)DxfCode.Text, state.Id.ToString("N")),
        new TypedValue((int)DxfCode.Text, state.Name));

    private static DwgProjectState? Parse(ResultBuffer? buffer)
    {
        if (buffer is null)
        {
            return null;
        }

        var values = buffer.AsArray();
        if (values.Length < 2 || values[0].Value is not string idText || values[1].Value is not string name ||
            !Guid.TryParseExact(idText, "N", out var id))
        {
            return null;
        }

        return new DwgProjectState(id, name);
    }
}
