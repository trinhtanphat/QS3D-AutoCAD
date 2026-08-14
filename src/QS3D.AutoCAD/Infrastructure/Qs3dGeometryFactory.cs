using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using QS3D.AutoCAD.Metadata;
using QS3D.Core.Model;

namespace QS3D.AutoCAD.Infrastructure;

internal static class Qs3dGeometryFactory
{
    public static bool IsSolid(ElementKind kind) => kind is
        ElementKind.Column or ElementKind.Beam or ElementKind.Slab or ElementKind.Wall or ElementKind.Curtain;

    public static Solid3d CreateSolid(Qs3dEntityMetadata metadata)
    {
        var start = ToAcad(metadata.Start);
        var end = ToAcad(metadata.End);
        return metadata.Kind switch
        {
            ElementKind.Column => AutoCadDrawing.CreateAxisAlignedBox(start, metadata.Width, metadata.Depth, metadata.Height),
            ElementKind.Beam => AutoCadDrawing.CreatePlanOrientedBox(start, end, metadata.Width, metadata.Height),
            ElementKind.Wall or ElementKind.Curtain => AutoCadDrawing.CreatePlanOrientedBox(start, end, metadata.Thickness, metadata.Height),
            ElementKind.Slab => CreateSlab(metadata),
            _ => throw new InvalidOperationException($"Cannot build {metadata.Kind} as a solid.")
        };
    }

    private static Solid3d CreateSlab(Qs3dEntityMetadata metadata)
    {
        var min = new Point3d(
            Math.Min(metadata.Start.X, metadata.End.X),
            Math.Min(metadata.Start.Y, metadata.End.Y),
            Math.Min(metadata.Start.Z, metadata.End.Z));
        var x = Math.Abs(metadata.End.X - metadata.Start.X);
        var y = Math.Abs(metadata.End.Y - metadata.Start.Y);
        if (x <= Tolerance.Global.EqualPoint || y <= Tolerance.Global.EqualPoint)
        {
            throw new InvalidOperationException("Stored slab geometry has a zero plan dimension.");
        }

        return AutoCadDrawing.CreateAxisAlignedBox(min, x, y, metadata.Thickness);
    }

    private static Point3d ToAcad(QS3D.Core.Geometry.Point3 point) => new(point.X, point.Y, point.Z);
}
