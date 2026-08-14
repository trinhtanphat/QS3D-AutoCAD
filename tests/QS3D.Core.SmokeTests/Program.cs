using QS3D.Core.Geometry;
using QS3D.Core.Model;
using QS3D.Core.Services;

var column = ElementFactory.Column("C1", Point3.Origin, 0.3, 0.4, 3.0);
AssertNear(column.Volume, 0.36, "column volume");

var beam = ElementFactory.Beam("B1", Point3.Origin, new Point3(5, 0, 0), 0.2, 0.4);
AssertNear(beam.Volume, 0.4, "beam volume");

var slab = ElementFactory.Slab("S1", Point3.Origin, new Point3(5, 4, 0), 0.15);
AssertNear(slab.Area, 20, "slab area");
AssertNear(slab.Volume, 3, "slab volume");

var wall = ElementFactory.Wall("W1", Point3.Origin, new Point3(5, 0, 0), 0.2, 3);
var summaries = QuantityService.Summarize([column, beam, slab, wall]);
Assert(summaries.Count == 4, "expected four BOQ groups");
Assert(summaries.Single(item => item.Kind == ElementKind.Wall).Volume > 2.99, "wall BOQ volume");

var project = new ProjectModel { Name = "Smoke" };
project.Upsert(column);
project.Upsert(column with { Name = "C1-updated" });
Assert(project.Elements.Count == 1, "upsert must preserve identity");
Assert(project.Elements[0].Name == "C1-updated", "upsert must replace data");

Console.WriteLine("QS3D.Core smoke tests passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertNear(double actual, double expected, string message)
{
    if (Math.Abs(actual - expected) > 1e-9)
    {
        throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}");
    }
}
