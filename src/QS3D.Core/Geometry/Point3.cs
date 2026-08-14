namespace QS3D.Core.Geometry;

public readonly record struct Point3(double X, double Y, double Z)
{
    public static Point3 Origin => new(0, 0, 0);

    public double DistanceTo(Point3 other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        var dz = other.Z - Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    public double PlanDistanceTo(Point3 other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
