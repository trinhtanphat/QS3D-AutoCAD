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

var offGridBeam = placedBeam with
{
    Start = new Point3(1, 2, 3.6),
    End = new Point3(6, 2, 3.6)
};
var translatedToOneGrid = PlacementService.SnapToGrids(offGridBeam, gridA);
AssertNear(translatedToOneGrid.Start.X, 0, "one-grid snap start x");
AssertNear(translatedToOneGrid.End.X, 5, "one-grid snap preserves beam vector");
AssertNear(translatedToOneGrid.PlanLength, offGridBeam.PlanLength, "one-grid snap preserves plan length");
AssertNear(translatedToOneGrid.Start.Z, 3.6, "one-grid snap preserves level elevation");
Assert(translatedToOneGrid.LevelId == level.Id, "one-grid snap preserves level binding");
Assert(translatedToOneGrid.StartGridId == gridA.Id, "one-grid snap stores reference");
Assert(translatedToOneGrid.EndGridId is null, "one-grid snap clears second reference");

var reshapedBeam = PlacementService.SnapToGrids(offGridBeam, gridA, gridB);
AssertNear(reshapedBeam.Start.X, 0, "two-grid snap start projection");
AssertNear(reshapedBeam.End.X, 5, "two-grid snap end projection");
AssertNear(reshapedBeam.Start.Z, 3.6, "two-grid snap preserves start z");
AssertNear(reshapedBeam.End.Z, 3.6, "two-grid snap preserves end z");
Assert(reshapedBeam.StartGridId == gridA.Id && reshapedBeam.EndGridId == gridB.Id, "two-grid snap stores both references");

var gridHorizontal = ElementFactory.Marker(ElementKind.Grid, "1", new Point3(-5, 3, 0), new Point3(5, 3, 0));
var elevatedColumn = column with { Start = new Point3(2, 1, 3.6), End = new Point3(2, 1, 3.6), LevelId = level.Id };
var snappedColumn = PlacementService.SnapToGrids(elevatedColumn, gridA, gridHorizontal);
AssertNear(snappedColumn.Start.X, 0, "column grid intersection x");
AssertNear(snappedColumn.Start.Y, 3, "column grid intersection y");
AssertNear(snappedColumn.Start.Z, 3.6, "column grid intersection preserves z");
Assert(snappedColumn.LevelId == level.Id, "column grid intersection preserves level binding");

AssertThrows<InvalidOperationException>(
    () => PlacementService.SnapToGrids(elevatedColumn, gridA, gridB),
    "parallel grids must not fabricate a column intersection");

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

static void AssertThrows<TException>(Action action, string message) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}
