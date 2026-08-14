using QS3D.Core.Geometry;
using QS3D.Core.Model;

namespace QS3D.Core.Services;

public static class ElementFactory
{
    public static StructuralElement Column(string name, Point3 basePoint, double width, double depth, double height) =>
        Create(ElementKind.Column, name, basePoint, basePoint, width, depth, height, 0);

    public static StructuralElement Beam(string name, Point3 start, Point3 end, double width, double height) =>
        Create(ElementKind.Beam, name, start, end, width, 0, height, 0);

    public static StructuralElement Slab(string name, Point3 corner1, Point3 corner2, double thickness) =>
        Create(ElementKind.Slab, name, corner1, corner2, 0, 0, 0, thickness);

    public static StructuralElement Wall(string name, Point3 start, Point3 end, double thickness, double height) =>
        Create(ElementKind.Wall, name, start, end, 0, 0, height, thickness);

    public static StructuralElement Marker(ElementKind kind, string name, Point3 start, Point3 end) =>
        Create(kind, name, start, end, 0, 0, 0, 0);

    private static StructuralElement Create(
        ElementKind kind,
        string name,
        Point3 start,
        Point3 end,
        double width,
        double depth,
        double height,
        double thickness)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Element name is required.", nameof(name));
        }

        EnsureNonNegative(width, nameof(width));
        EnsureNonNegative(depth, nameof(depth));
        EnsureNonNegative(height, nameof(height));
        EnsureNonNegative(thickness, nameof(thickness));

        return new StructuralElement(Guid.NewGuid(), kind, name.Trim(), start, end, width, depth, height, thickness);
    }

    private static void EnsureNonNegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(name, "Dimensions must be finite and non-negative.");
        }
    }
}
