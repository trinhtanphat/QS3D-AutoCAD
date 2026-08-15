using QS3D.Core.Model;

namespace QS3D.Core.Services;

public sealed record QuantitySummary(ElementKind Kind, int Count, double Area, double Volume);

public static class QuantityService
{
    public static IReadOnlyList<QuantitySummary> Summarize(IEnumerable<StructuralElement> elements)
    {
        if (elements is null)
        {
            throw new ArgumentNullException(nameof(elements));
        }

        return elements
            .GroupBy(element => element.Kind)
            .OrderBy(group => group.Key)
            .Select(group => new QuantitySummary(
                group.Key,
                group.Sum(element => element.Count),
                group.Sum(element => element.Area * element.Count),
                group.Sum(element => element.Volume * element.Count)))
            .ToArray();
    }
}
