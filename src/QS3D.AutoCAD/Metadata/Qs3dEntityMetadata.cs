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
    int Count = 1,
    Guid? LevelId = null,
    Guid? StartGridId = null,
    Guid? EndGridId = null)
{
    public const string RegAppName = "QS3D";
    private const string CurrentSchema = "QS3D2";
    private const string LegacySchema = "QS3D1";

    public StructuralElement ToCore() => new(
        Id,
        Kind,
        Name,
        Start,
        End,
        Width,
        Depth,
        Height,
        Thickness,
        Count,
        LevelId,
        StartGridId,
        EndGridId);

    public static Qs3dEntityMetadata FromCore(StructuralElement element) => new(
        element.Id,
        element.Kind,
        element.Name,
        element.Start,
        element.End,
        element.Width,
        element.Depth,
        element.Height,
        element.Thickness,
        element.Count,
        element.LevelId,
        element.StartGridId,
        element.EndGridId);

    public void Attach(Transaction transaction, Database database, Entity entity)
    {
        EnsureRegApp(transaction, database);
        var safeName = Name.Replace('|', '_').Replace('=', '_');
        if (safeName.Length > 200)
        {
            safeName = safeName[..200];
        }

        entity.XData = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegAppName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, CurrentSchema),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, Id.ToString("N")),
            new TypedValue((int)DxfCode.ExtendedDataInteger16, (short)Kind),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, safeName),
            new TypedValue((int)DxfCode.ExtendedDataReal, Start.X),
            new TypedValue((int)DxfCode.ExtendedDataReal, Start.Y),
            new TypedValue((int)DxfCode.ExtendedDataReal, Start.Z),
            new TypedValue((int)DxfCode.ExtendedDataReal, End.X),
            new TypedValue((int)DxfCode.ExtendedDataReal, End.Y),
            new TypedValue((int)DxfCode.ExtendedDataReal, End.Z),
            new TypedValue((int)DxfCode.ExtendedDataReal, Width),
            new TypedValue((int)DxfCode.ExtendedDataReal, Depth),
            new TypedValue((int)DxfCode.ExtendedDataReal, Height),
            new TypedValue((int)DxfCode.ExtendedDataReal, Thickness),
            new TypedValue((int)DxfCode.ExtendedDataInteger32, Count),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, WriteGuid(LevelId)),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, WriteGuid(StartGridId)),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, WriteGuid(EndGridId)));
    }

    public static bool TryRead(Entity entity, out Qs3dEntityMetadata metadata)
    {
        metadata = null!;
        using var data = entity.GetXDataForApplication(RegAppName);
        if (data is null)
        {
            return false;
        }

        var values = data.AsArray();
        if (values.Length < 16 ||
            values[1].Value is not string schema ||
            (schema != LegacySchema && schema != CurrentSchema) ||
            values[2].Value is not string idText || !Guid.TryParseExact(idText, "N", out var id))
        {
            return false;
        }

        try
        {
            var kindValue = Convert.ToInt32(values[3].Value, CultureInfo.InvariantCulture);
            if (!Enum.IsDefined(typeof(ElementKind), kindValue))
            {
                return false;
            }

            if (schema == CurrentSchema && values.Length < 19)
            {
                return false;
            }

            var name = values[4].Value as string ?? ((ElementKind)kindValue).ToString();
            metadata = new Qs3dEntityMetadata(
                id,
                (ElementKind)kindValue,
                name,
                new Point3(ReadDouble(values[5]), ReadDouble(values[6]), ReadDouble(values[7])),
                new Point3(ReadDouble(values[8]), ReadDouble(values[9]), ReadDouble(values[10])),
                ReadDouble(values[11]),
                ReadDouble(values[12]),
                ReadDouble(values[13]),
                ReadDouble(values[14]),
                Math.Max(1, Convert.ToInt32(values[15].Value, CultureInfo.InvariantCulture)),
                schema == CurrentSchema ? ReadOptionalGuid(values[16]) : null,
                schema == CurrentSchema ? ReadOptionalGuid(values[17]) : null,
                schema == CurrentSchema ? ReadOptionalGuid(values[18]) : null);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }

    private static double ReadDouble(TypedValue value) => Convert.ToDouble(value.Value, CultureInfo.InvariantCulture);

    private static string WriteGuid(Guid? value) => value?.ToString("N") ?? string.Empty;

    private static Guid? ReadOptionalGuid(TypedValue value)
    {
        if (value.Value is not string text || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return Guid.TryParseExact(text, "N", out var parsed) ? parsed : null;
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
