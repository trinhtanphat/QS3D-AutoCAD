namespace QS3D.Core.Model;

public sealed class ProjectModel
{
    private readonly List<StructuralElement> _elements = [];

    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "Untitled QS3D Project";
    public int SchemaVersion { get; init; } = 1;
    public DateTimeOffset UpdatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<StructuralElement> Elements => _elements;

    public void Upsert(StructuralElement element)
    {
        var index = _elements.FindIndex(item => item.Id == element.Id);
        if (index >= 0)
        {
            _elements[index] = element;
        }
        else
        {
            _elements.Add(element);
        }

        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public bool Remove(Guid id)
    {
        var removed = _elements.RemoveAll(item => item.Id == id) > 0;
        if (removed)
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        return removed;
    }
}
