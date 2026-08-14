using QS3D.Core.Geometry;

namespace QS3D.Core.Model;

public sealed record StructuralElement(
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
    public double PlanLength => Start.PlanDistanceTo(End);

    public double Area => Kind switch
    {
        ElementKind.Slab => Math.Abs(End.X - Start.X) * Math.Abs(End.Y - Start.Y),
        ElementKind.Wall or ElementKind.Curtain => PlanLength * Height,
        _ => 0
    };

    public double Volume => Kind switch
    {
        ElementKind.Column => Width * Depth * Height,
        ElementKind.Beam => PlanLength * Width * Height,
        ElementKind.Slab => Area * Thickness,
        ElementKind.Wall => PlanLength * Thickness * Height,
        _ => 0
    };
}
