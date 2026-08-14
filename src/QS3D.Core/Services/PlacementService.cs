using QS3D.Core.Geometry;
using QS3D.Core.Model;

namespace QS3D.Core.Services;

public static class PlacementService
{
    public static bool SupportsLevelPlacement(ElementKind kind) => kind is
        ElementKind.Column or
        ElementKind.Beam or
        ElementKind.Slab or
        ElementKind.Wall or
        ElementKind.Curtain;

    public static StructuralElement PlaceOnLevel(StructuralElement element, StructuralElement level)
    {
        if (level.Kind != ElementKind.Level)
        {
            throw new ArgumentException("Reference element must be a Level.", nameof(level));
        }

        if (!SupportsLevelPlacement(element.Kind))
        {
            throw new ArgumentException($"{element.Kind} does not support level placement.", nameof(element));
        }

        var elevation = level.Start.Z;
        return element with
        {
            Start = WithZ(element.Start, elevation),
            End = WithZ(element.End, elevation),
            LevelId = level.Id
        };
    }

    public static StructuralElement BindGrids(
        StructuralElement element,
        StructuralElement startGrid,
        StructuralElement? endGrid = null)
    {
        EnsureGrid(startGrid, nameof(startGrid));
        if (endGrid is not null)
        {
            EnsureGrid(endGrid, nameof(endGrid));
        }

        if (element.Kind is ElementKind.Level or ElementKind.Grid or ElementKind.Section)
        {
            throw new ArgumentException($"{element.Kind} cannot be bound to structural grids.", nameof(element));
        }

        return element with
        {
            StartGridId = startGrid.Id,
            EndGridId = endGrid?.Id
        };
    }

    public static StructuralElement SnapToGrids(
        StructuralElement element,
        StructuralElement startGrid,
        StructuralElement? endGrid = null)
    {
        var bound = BindGrids(element, startGrid, endGrid);

        if (endGrid is null)
        {
            var snappedStart = ProjectToGrid(element.Start, startGrid);
            var dx = snappedStart.X - element.Start.X;
            var dy = snappedStart.Y - element.Start.Y;
            return bound with
            {
                Start = TranslatePlan(element.Start, dx, dy),
                End = TranslatePlan(element.End, dx, dy)
            };
        }

        if (element.Kind == ElementKind.Column)
        {
            var intersection = IntersectGridLines(startGrid, endGrid);
            return bound with
            {
                Start = new Point3(intersection.X, intersection.Y, element.Start.Z),
                End = new Point3(intersection.X, intersection.Y, element.End.Z)
            };
        }

        return bound with
        {
            Start = ProjectToGrid(element.Start, startGrid),
            End = ProjectToGrid(element.End, endGrid)
        };
    }

    public static Point3 ProjectToGrid(Point3 point, StructuralElement grid)
    {
        EnsureGrid(grid, nameof(grid));
        var dx = grid.End.X - grid.Start.X;
        var dy = grid.End.Y - grid.Start.Y;
        var denominator = (dx * dx) + (dy * dy);
        if (denominator <= 1e-18)
        {
            throw new ArgumentException("Grid must have non-zero plan length.", nameof(grid));
        }

        var px = point.X - grid.Start.X;
        var py = point.Y - grid.Start.Y;
        var t = ((px * dx) + (py * dy)) / denominator;
        return new Point3(grid.Start.X + (t * dx), grid.Start.Y + (t * dy), point.Z);
    }

    public static bool References(StructuralElement element, Guid referenceId) =>
        element.LevelId == referenceId ||
        element.StartGridId == referenceId ||
        element.EndGridId == referenceId;

    public static IReadOnlyList<StructuralElement> FindDependents(
        IEnumerable<StructuralElement> elements,
        Guid referenceId) =>
        elements.Where(element => element.Id != referenceId && References(element, referenceId)).ToArray();

    public static StructuralElement ShiftElevation(StructuralElement element, double delta)
    {
        if (!double.IsFinite(delta))
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Elevation delta must be finite.");
        }

        return element with
        {
            Start = WithZ(element.Start, element.Start.Z + delta),
            End = WithZ(element.End, element.End.Z + delta)
        };
    }

    private static Point3 IntersectGridLines(StructuralElement first, StructuralElement second)
    {
        EnsureGrid(first, nameof(first));
        EnsureGrid(second, nameof(second));

        var ax = first.End.X - first.Start.X;
        var ay = first.End.Y - first.Start.Y;
        var bx = second.End.X - second.Start.X;
        var by = second.End.Y - second.Start.Y;
        if (((ax * ax) + (ay * ay)) <= 1e-18 || ((bx * bx) + (by * by)) <= 1e-18)
        {
            throw new ArgumentException("Grid must have non-zero plan length.");
        }

        var determinant = (ax * by) - (ay * bx);
        if (Math.Abs(determinant) <= 1e-12)
        {
            throw new InvalidOperationException("Two-grid Column snapping requires non-parallel Grid lines.");
        }

        var qx = second.Start.X - first.Start.X;
        var qy = second.Start.Y - first.Start.Y;
        var t = ((qx * by) - (qy * bx)) / determinant;
        return new Point3(first.Start.X + (t * ax), first.Start.Y + (t * ay), 0);
    }

    private static Point3 TranslatePlan(Point3 point, double dx, double dy) =>
        new(point.X + dx, point.Y + dy, point.Z);

    private static Point3 WithZ(Point3 point, double z) => new(point.X, point.Y, z);

    private static void EnsureGrid(StructuralElement grid, string parameterName)
    {
        if (grid.Kind != ElementKind.Grid)
        {
            throw new ArgumentException("Reference element must be a Grid.", parameterName);
        }
    }
}
