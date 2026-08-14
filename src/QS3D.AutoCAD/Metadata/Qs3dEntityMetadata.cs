using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using QS3D.Core.Geometry;
using QS3D.Core.Model;

namespace QS3D.AutoCAD.Metadata;

internal sealed record Qs3dEntityMetadata(
    Guid Id,
    ElementKind Kind,
    string Name,
    Point3 Start,
    Point3 End,
    double Width,
    double Depth,
    double Height,
    double Thickness,
    int Count = 1)
{
    public const string RegAppName = "QS3D";

    public StructuralElement ToCore() => new(Id, Kind, Name, Start, End, Width, Depth, Height, Thickness, Count);

    public void Attach(Transaction transaction, Database database, Entity entity)
    {
        EnsureRegApp(transaction, database);
        entity.XData = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegAppName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, Serialize()));
    }

    public static bool TryRead(Entity entity, out Qs3dEntityMetadata metadata)
    {
        metadata = null!;
        var data = entity.GetXDataForApplication(RegAppName);
        if (data is null)
        {
            return false;
        }

        var values = data.AsArray();
        var payload = values
            .FirstOrDefault(value => value.TypeCode == (int)DxfCode.ExtendedDataAsciiString)
            .Value as string;
        return payload is not null && TryParse(payload, out metadata);
    }

    private string Serialize()
    {
        var invariant = CultureInfo.InvariantCulture;
        var safeName = Name.Replace('|', '_').Replace('=', '_');
        if (safeName.Length > 32)
        {
            safeName = safeName[..32];
        }

        return string.Join('|',
            "v=1",
            $"id={Id:N}",
            $"k={(int)Kind}",
            $"n={safeName}",
            $"sx={Start.X.ToString("R", invariant)}",
            $"sy={Start.Y.ToString("R", invariant)}",
            $"sz={Start.Z.ToString("R", invariant)}",
            $"ex={End.X.ToString("R", invariant)}",
            $"ey={End.Y.ToString("R", invariant)}",
            $"ez={End.Z.ToString("R", invariant)}",
            $"w={Width.ToString("R", invariant)}",
            $"d={Depth.ToString("R", invariant)}",
            $"h={Height.ToString("R", invariant)}",
            $"t={Thickness.ToString("R", invariant)}",
            $"c={Count.ToString(invariant)}");
    }

    private static bool TryParse(string payload, out Qs3dEntityMetadata metadata)
    {
        metadata = null!;
        var fields = payload
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

        if (!fields.TryGetValue("id", out var idText) || !Guid.TryParseExact(idText, "N", out var id) ||
            !fields.TryGetValue("k", out var kindText) || !int.TryParse(kindText, out var kindValue) ||
            !Enum.IsDefined(typeof(ElementKind), kindValue))
        {
            return false;
        }

        var name = fields.GetValueOrDefault("n", ((ElementKind)kindValue).ToString());
        metadata = new Qs3dEntityMetadata(
            id,
            (ElementKind)kindValue,
            name,
            new Point3(ReadDouble(fields, "sx"), ReadDouble(fields, "sy"), ReadDouble(fields, "sz")),
            new Point3(ReadDouble(fields, "ex"), ReadDouble(fields, "ey"), ReadDouble(fields, "ez")),
            ReadDouble(fields, "w"),
            ReadDouble(fields, "d"),
            ReadDouble(fields, "h"),
            ReadDouble(fields, "t"),
            Math.Max(1, ReadInt(fields, "c", 1)));
        return true;
    }

    private static double ReadDouble(IReadOnlyDictionary<string, string> fields, string key)
    {
        return fields.TryGetValue(key, out var value) &&
               double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static int ReadInt(IReadOnlyDictionary<string, string> fields, string key, int fallback)
    {
        return fields.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;
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
