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

var level = ElementFactory.Marker(ElementKind.Level, "L1", new Point3(0, 0, 3.6), new Point3(0, 0, 3.6));
var placedBeam = PlacementService.PlaceOnLevel(beam, level);
Assert(placedBeam.LevelId == level.Id, "level binding must preserve reference identity");
AssertNear(placedBeam.Start.Z, 3.6, "placed beam start elevation");
AssertNear(placedBeam.End.Z, 3.6, "placed beam end elevation");

var gridA = ElementFactory.Marker(ElementKind.Grid, "A", new Point3(0, -5, 0), new Point3(0, 5, 0));
var gridB = ElementFactory.Marker(ElementKind.Grid, "B", new Point3(5, -5, 0), new Point3(5, 5, 0));
var boundBeam = PlacementService.BindGrids(placedBeam, gridA, gridB);
Assert(boundBeam.StartGridId == gridA.Id, "start grid binding");
Assert(boundBeam.EndGridId == gridB.Id, "end grid binding");
Assert(PlacementService.FindDependents([level, gridA, gridB, boundBeam], level.Id).Single().Id == boundBeam.Id, "level dependency lookup");
Assert(PlacementService.FindDependents([level, gridA, gridB, boundBeam], gridA.Id).Single().Id == boundBeam.Id, "grid dependency lookup");

var shifted = PlacementService.ShiftElevation(boundBeam, 0.4);
AssertNear(shifted.Start.Z, 4.0, "shifted start elevation");
AssertNear(shifted.End.Z, 4.0, "shifted end elevation");
Assert(shifted.LevelId == level.Id, "elevation shift must preserve placement references");

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
