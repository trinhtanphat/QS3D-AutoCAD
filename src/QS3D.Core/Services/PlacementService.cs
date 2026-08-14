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

    private static Point3 WithZ(Point3 point, double z) => new(point.X, point.Y, z);

    private static void EnsureGrid(StructuralElement grid, string parameterName)
    {
        if (grid.Kind != ElementKind.Grid)
        {
            throw new ArgumentException("Reference element must be a Grid.", parameterName);
        }
    }
}
