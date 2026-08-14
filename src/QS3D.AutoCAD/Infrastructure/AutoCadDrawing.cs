using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcColor = Autodesk.AutoCAD.Colors.Color;

namespace QS3D.AutoCAD.Infrastructure;

internal static class AutoCadDrawing
{
    public static ObjectId EnsureLayer(Transaction transaction, Database database, string name, short colorIndex)
    {
        var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (layerTable.Has(name))
        {
            return layerTable[name];
        }

        layerTable.UpgradeOpen();
        var record = new LayerTableRecord
        {
            Name = name,
            Color = AcColor.FromColorIndex(ColorMethod.ByAci, colorIndex)
        };
        var id = layerTable.Add(record);
        transaction.AddNewlyCreatedDBObject(record, true);
        return id;
    }

    public static ObjectId Append(Transaction transaction, Database database, Entity entity, ObjectId layerId)
    {
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
        entity.LayerId = layerId;
        var id = space.AppendEntity(entity);
        transaction.AddNewlyCreatedDBObject(entity, true);
        return id;
    }

    public static Solid3d CreateAxisAlignedBox(Point3d origin, double xLength, double yLength, double zLength)
    {
        var solid = new Solid3d();
        solid.CreateBox(xLength, yLength, zLength);
        solid.TransformBy(Matrix3d.Displacement(origin.GetAsVector()));
        return solid;
    }

    public static Solid3d CreatePlanOrientedBox(Point3d start, Point3d end, double width, double height)
    {
        var direction = new Vector3d(end.X - start.X, end.Y - start.Y, 0);
        if (direction.Length <= Tolerance.Global.EqualPoint)
        {
            throw new ArgumentException("Start and end points must be different.", nameof(end));
        }

        var xAxis = direction.GetNormal();
        var zAxis = Vector3d.ZAxis;
        var yAxis = zAxis.CrossProduct(xAxis).GetNormal();
        var targetOrigin = start.Subtract(yAxis.MultiplyBy(width / 2.0));

        var solid = new Solid3d();
        solid.CreateBox(direction.Length, width, height);
        solid.TransformBy(Matrix3d.AlignCoordinateSystem(
            Point3d.Origin,
            Vector3d.XAxis,
            Vector3d.YAxis,
            Vector3d.ZAxis,
            targetOrigin,
            xAxis,
            yAxis,
            zAxis));
        return solid;
    }
}
