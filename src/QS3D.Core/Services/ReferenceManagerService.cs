using QS3D.Core.Model;

namespace QS3D.Core.Services;

public static class ReferenceManagerService
{
    public static IReadOnlyList<StructuralElement> OrderLevels(
        IEnumerable<StructuralElement> elements,
        bool descending = false)
    {
        var levels = elements
            .Where(element => element.Kind == ElementKind.Level)
            .OrderBy(element => element.Start.Z)
            .ThenBy(element => element.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(element => element.Id)
            .ToArray();

        return descending ? levels.Reverse().ToArray() : levels;
    }

    public static IReadOnlyList<StructuralElement> OrderParallelGrids(
        IEnumerable<StructuralElement> elements,
        double tolerance = 1e-9)
    {
        if (!double.IsFinite(tolerance) || tolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Grid ordering tolerance must be finite and positive.");
        }

        var grids = elements.Where(element => element.Kind == ElementKind.Grid).ToArray();
        if (grids.Length == 0)
        {
            return Array.Empty<StructuralElement>();
        }

        var direction = CanonicalDirection(grids[0], tolerance);
        foreach (var grid in grids.Skip(1))
        {
            var candidate = UnitDirection(grid, tolerance);
            var cross = Math.Abs((direction.X * candidate.Y) - (direction.Y * candidate.X));
            if (cross > tolerance)
            {
                throw new InvalidOperationException("Grid resequencing requires one parallel Grid family.");
            }
        }

        var normalX = -direction.Y;
        var normalY = direction.X;
        return grids
            .Select(grid => new
            {
                Grid = grid,
                Offset = (((grid.Start.X + grid.End.X) / 2.0) * normalX) +
                         (((grid.Start.Y + grid.End.Y) / 2.0) * normalY)
            })
            .OrderBy(item => item.Offset)
            .ThenBy(item => item.Grid.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Grid.Id)
            .Select(item => item.Grid)
            .ToArray();
    }

    public static StructuralElement RenameReference(StructuralElement reference, string name)
    {
        if (reference.Kind is not (ElementKind.Level or ElementKind.Grid))
        {
            throw new ArgumentException("Only Level or Grid references can be renamed.", nameof(reference));
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Reference name cannot be empty.", nameof(name));
        }

        return reference with { Name = name.Trim() };
    }

    private static PlanDirection CanonicalDirection(StructuralElement grid, double tolerance)
    {
        var direction = UnitDirection(grid, tolerance);
        if (direction.X < -tolerance || (Math.Abs(direction.X) <= tolerance && direction.Y < 0))
        {
            return new PlanDirection(-direction.X, -direction.Y);
        }
        return direction;
    }

    private static PlanDirection UnitDirection(StructuralElement grid, double tolerance)
    {
        if (grid.Kind != ElementKind.Grid)
        {
            throw new ArgumentException("Reference element must be a Grid.", nameof(grid));
        }

        var dx = grid.End.X - grid.Start.X;
        var dy = grid.End.Y - grid.Start.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length <= tolerance)
        {
            throw new ArgumentException("Grid must have non-zero plan length.", nameof(grid));
        }
        return new PlanDirection(dx / length, dy / length);
    }

    private readonly record struct PlanDirection(double X, double Y);
}
