using System.Collections.ObjectModel;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using QS3D.AutoCAD;
using QS3D.Platform.Parity;

namespace QS3D.AutoCAD.Commands;

public sealed class Qs3dMepCommands
{
    private const string DefaultRegion = "DRAWING";
    private const int MaxLocatePairs = 200;
    private const int MaxExactSolids = 500;
    private const int MaxExactPairs = 100000;

    [CommandMethod("QS3DMEPTAKEOFF", CommandFlags.UsePickSet)]
    public void Takeoff()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        try
        {
            if (!TryGetMetersPerUnit(document.Database, out var metersPerUnit, out var error))
            {
                editor.WriteMessage("\nQS3DMEPTAKEOFF: " + error + "\n");
                return;
            }
            var ids = GetImpliedSelection(editor);
            if (ids.Count == 0)
            {
                editor.WriteMessage("\nQS3DMEPTAKEOFF: select MEP entities first.\n");
                return;
            }

            var captured = new List<MepElement>();
            var skipped = 0;
            using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var id in ids)
                {
                    try
                    {
                        var entity = transaction.GetObject(id, OpenMode.ForRead, false) as Entity;
                        if (entity is null || entity.IsErased || !TryRecognize(transaction, entity, out var recognition) ||
                            recognition.Discipline != MepDiscipline.Mep || !recognition.MepKind.HasValue)
                        {
                            skipped++;
                            continue;
                        }
                        var lengthM = TryGetCurveLength(entity, out var length) ? length * metersPerUnit : 0d;
                        var areaM2 = TryGetArea(entity, out var area) ? area * metersPerUnit * metersPerUnit : 0d;
                        var volumeM3 = TryGetVolume(entity, out var volume) ? volume * metersPerUnit * metersPerUnit * metersPerUnit : 0d;
                        if (!FiniteNonNegative(lengthM) || !FiniteNonNegative(areaM2) || !FiniteNonNegative(volumeM3))
                        {
                            skipped++;
                            continue;
                        }
                        var category = recognition.Category ?? recognition.MepKind.Value.ToString();
                        captured.Add(new MepElement(
                            entity.Handle.ToString(),
                            recognition.MepKind.Value,
                            CanonicalOrFallback(entity.Layer, category),
                            Specification(transaction, entity),
                            DefaultRegion,
                            1,
                            lengthM,
                            areaM2,
                            volumeM3));
                    }
                    catch (System.Exception ex) when (IsRecoverableEntityFailure(ex))
                    {
                        skipped++;
                    }
                }
                transaction.Commit();
            }

            if (captured.Count == 0)
            {
                editor.WriteMessage("\nQS3DMEPTAKEOFF: no unambiguous MEP entity was recognized; unknown/ambiguous entities were skipped.\n");
                return;
            }
            var rows = new MepQuantityService().Aggregate(captured);
            editor.WriteMessage("\nQS3DMEPTAKEOFF: recognized=" + captured.Count + " groups=" + rows.Count + " skipped=" + skipped + ".\n");
            foreach (var row in rows)
            {
                editor.WriteMessage(
                    "  " + row.Region + " | " + row.System + " | " + row.Specification + " | " + row.Kind +
                    " | entities=" + row.ElementCount + " count=" + row.QuantityCount +
                    " L=" + row.LengthM.ToString("0.###", CultureInfo.InvariantCulture) + " m" +
                    " A=" + row.AreaM2.ToString("0.###", CultureInfo.InvariantCulture) + " m2" +
                    " V=" + row.VolumeM3.ToString("0.###", CultureInfo.InvariantCulture) + " m3\n");
            }
        }
        catch (System.Exception ex)
        {
            editor.WriteMessage("\nQS3DMEPTAKEOFF failed: " + ex.Message + "\n");
        }
    }

    [CommandMethod("QS3DMEPCLASH", CommandFlags.UsePickSet)]
    public void Clash()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        try
        {
            if (!TryGetMetersPerUnit(document.Database, out var metersPerUnit, out var error))
            {
                editor.WriteMessage("\nQS3DMEPCLASH: " + error + "\n");
                return;
            }
            var ids = GetImpliedSelection(editor);
            if (ids.Count < 2)
            {
                editor.WriteMessage("\nQS3DMEPCLASH: select at least two MEP/Structure/Architecture entities.\n");
                return;
            }
            if (!TryPromptClearance(editor, out var clearanceDrawingUnits)) return;
            var candidates = ReadCoordination(document.Database, ids, metersPerUnit, out var skipped);
            if (candidates.Count < 2)
            {
                editor.WriteMessage("\nQS3DMEPCLASH: fewer than two recognized entities with valid extents; skipped=" + skipped + ".\n");
                return;
            }
            var clearanceM = clearanceDrawingUnits * metersPerUnit;
            if (!FiniteNonNegative(clearanceM))
            {
                editor.WriteMessage("\nQS3DMEPCLASH: clearance overflow after unit conversion.\n");
                return;
            }
            var clashes = DetectRelevant(candidates, clearanceM);
            editor.WriteMessage("\nQS3DMEPCLASH: candidates=" + candidates.Count + " clashes=" + clashes.Count + " skipped=" + skipped + ".\n");
            for (var i = 0; i < clashes.Count; i++) WriteClash(editor, clashes[i], null);
        }
        catch (System.Exception ex)
        {
            editor.WriteMessage("\nQS3DMEPCLASH failed: " + ex.Message + "\n");
        }
    }

    [CommandMethod("QS3DMEPCLASHLOCATE", CommandFlags.UsePickSet)]
    public void ClashLocate()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        try
        {
            if (!TryGetMetersPerUnit(document.Database, out var metersPerUnit, out var error))
            {
                editor.WriteMessage("\nQS3DMEPCLASHLOCATE: " + error + "\n");
                return;
            }
            var ids = GetImpliedSelection(editor);
            if (ids.Count < 2)
            {
                editor.WriteMessage("\nQS3DMEPCLASHLOCATE: select at least two entities first.\n");
                return;
            }
            if (!TryPromptClearance(editor, out var clearanceDrawingUnits)) return;
            var clearanceM = clearanceDrawingUnits * metersPerUnit;
            if (!FiniteNonNegative(clearanceM)) return;
            var candidates = ReadCoordination(document.Database, ids, metersPerUnit, out var skipped);
            var clashes = DetectRelevant(candidates, clearanceM);
            if (clashes.Count == 0)
            {
                editor.WriteMessage("\nQS3DMEPCLASHLOCATE: no relevant clash found; skipped=" + skipped + ".\n");
                return;
            }
            var reviewCount = Math.Min(clashes.Count, MaxLocatePairs);
            editor.WriteMessage("\nQS3DMEPCLASHLOCATE: clashes=" + clashes.Count + " review=" + reviewCount + ".\n");
            for (var i = 0; i < reviewCount; i++) WriteClash(editor, clashes[i], i + 1);
            var options = new PromptIntegerOptions("\nClash number to locate [1-" + reviewCount.ToString(CultureInfo.InvariantCulture) + "]: ")
            {
                AllowNegative = false,
                AllowZero = false,
                AllowNone = false,
                LowerLimit = 1,
                UpperLimit = reviewCount
            };
            var selected = editor.GetInteger(options);
            if (selected.Status != PromptStatus.OK) return;
            var clash = clashes[selected.Value - 1];
            if (!TryResolvePair(document.Database, clash.LeftElementId, clash.RightElementId, out var pair))
            {
                editor.WriteMessage("\nQS3DMEPCLASHLOCATE: pair is stale; existing implied selection was preserved.\n");
                return;
            }
            editor.SetImpliedSelection(pair);
            editor.WriteMessage("\nQS3DMEPCLASHLOCATE: selected exactly 2 live entities.\n");
        }
        catch (System.Exception ex)
        {
            editor.WriteMessage("\nQS3DMEPCLASHLOCATE failed: " + ex.Message + "\n");
        }
    }

    [CommandMethod("QS3DMEPEXACTCLASH", CommandFlags.UsePickSet)]
    public void ExactClash()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        try
        {
            var ids = GetImpliedSelection(editor);
            if (ids.Count < 2)
            {
                editor.WriteMessage("\nQS3DMEPEXACTCLASH: select at least two recognized Solid3d entities.\n");
                return;
            }
            var pairs = new List<ExactPair>();
            var skipped = 0;
            var broadPairs = 0;
            using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var candidates = new List<ExactCandidate>();
                foreach (var id in ids)
                {
                    try
                    {
                        var solid = transaction.GetObject(id, OpenMode.ForRead, false) as Solid3d;
                        if (solid is null || solid.IsErased || !TryRecognize(transaction, solid, out var recognition) || !recognition.Discipline.HasValue)
                        {
                            skipped++;
                            continue;
                        }
                        var extents = solid.GeometricExtents;
                        if (!FiniteExtents(extents))
                        {
                            skipped++;
                            continue;
                        }
                        candidates.Add(new ExactCandidate(solid.Handle.ToString(), recognition.Discipline.Value, solid, extents));
                        if (candidates.Count > MaxExactSolids)
                            throw new InvalidOperationException("QS3DMEPEXACTCLASH limit is " + MaxExactSolids + " recognized solids per run.");
                    }
                    catch (System.Exception ex) when (IsRecoverableEntityFailure(ex))
                    {
                        skipped++;
                    }
                }
                candidates.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Handle, right.Handle));
                for (var i = 0; i < candidates.Count; i++)
                {
                    for (var j = i + 1; j < candidates.Count; j++)
                    {
                        var left = candidates[i];
                        var right = candidates[j];
                        if (left.Discipline != MepDiscipline.Mep && right.Discipline != MepDiscipline.Mep) continue;
                        if (!ExtentsIntersect(left.Extents, right.Extents)) continue;
                        broadPairs++;
                        if (broadPairs > MaxExactPairs)
                            throw new InvalidOperationException("QS3DMEPEXACTCLASH broad-phase limit is " + MaxExactPairs + " pairs per run.");
                        try
                        {
                            if (left.Solid.CheckInterference(right.Solid)) pairs.Add(new ExactPair(left.Handle, right.Handle));
                        }
                        catch (System.Exception ex) when (IsRecoverableEntityFailure(ex))
                        {
                            skipped++;
                        }
                    }
                }
                transaction.Commit();
            }
            editor.WriteMessage("\nQS3DMEPEXACTCLASH: broad-phase=" + broadPairs + " exact=" + pairs.Count + " skipped=" + skipped + ".\n");
            foreach (var pair in pairs) editor.WriteMessage("  ExactHard | " + pair.Left + " <-> " + pair.Right + "\n");
        }
        catch (System.Exception ex)
        {
            editor.WriteMessage("\nQS3DMEPEXACTCLASH failed: " + ex.Message + "\n");
        }
    }

    [CommandMethod("QS3DMEPZOOMSELECTION", CommandFlags.UsePickSet)]
    public void ZoomSelection()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null) return;
        var editor = document.Editor;
        try
        {
            var ids = GetImpliedSelection(editor);
            if (ids.Count == 0)
            {
                editor.WriteMessage("\nQS3DMEPZOOMSELECTION: select at least one live entity.\n");
                return;
            }
            var corners = new List<Point3d>();
            using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var id in ids)
                {
                    try
                    {
                        var entity = transaction.GetObject(id, OpenMode.ForRead, false) as Entity;
                        if (entity is null || entity.IsErased) continue;
                        var extents = entity.GeometricExtents;
                        if (!FiniteExtents(extents)) continue;
                        AddCorners(corners, extents);
                    }
                    catch (System.Exception ex) when (IsRecoverableEntityFailure(ex))
                    {
                    }
                }
                transaction.Commit();
            }
            if (corners.Count == 0)
            {
                editor.WriteMessage("\nQS3DMEPZOOMSELECTION: selected entities do not expose usable geometric extents.\n");
                return;
            }
            using var view = editor.GetCurrentView();
            var worldToDcs = Matrix3d.PlaneToWorld(view.ViewDirection);
            worldToDcs = Matrix3d.Displacement(view.Target - Point3d.Origin) * worldToDcs;
            worldToDcs = Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target) * worldToDcs;
            worldToDcs = worldToDcs.Inverse();
            var first = corners[0].TransformBy(worldToDcs);
            var minX = first.X;
            var maxX = first.X;
            var minY = first.Y;
            var maxY = first.Y;
            for (var i = 1; i < corners.Count; i++)
            {
                var point = corners[i].TransformBy(worldToDcs);
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }
            var width = Math.Max(maxX - minX, 1e-9);
            var height = Math.Max(maxY - minY, 1e-9);
            var aspect = view.Width > 0d && view.Height > 0d ? view.Width / view.Height : 1d;
            if (width / height > aspect) height = width / aspect;
            else width = height * aspect;
            view.Width = width * 1.10d;
            view.Height = height * 1.10d;
            view.CenterPoint = new Point2d((minX + maxX) * 0.5d, (minY + maxY) * 0.5d);
            editor.SetCurrentView(view);
            editor.WriteMessage("\nQS3DMEPZOOMSELECTION: fit " + ids.Count + " selected object(s).\n");
        }
        catch (System.Exception ex)
        {
            editor.WriteMessage("\nQS3DMEPZOOMSELECTION failed: " + ex.Message + "\n");
        }
    }

    private static IReadOnlyList<ObjectId> GetImpliedSelection(Editor editor)
    {
        var result = editor.SelectImplied();
        return result.Status == PromptStatus.OK && result.Value is not null ? result.Value.GetObjectIds() : Array.Empty<ObjectId>();
    }

    private static bool TryPromptClearance(Editor editor, out double clearance)
    {
        var options = new PromptDistanceOptions("\nClearance in drawing units (0 = hard clash only): ")
        {
            AllowNegative = false,
            AllowZero = true,
            AllowNone = false
        };
        var result = editor.GetDistance(options);
        clearance = result.Status == PromptStatus.OK ? result.Value : 0d;
        return result.Status == PromptStatus.OK;
    }

    private static IReadOnlyList<CoordinationElement> ReadCoordination(Database database, IReadOnlyList<ObjectId> ids, double metersPerUnit, out int skipped)
    {
        var result = new List<CoordinationElement>();
        skipped = 0;
        using (var transaction = database.TransactionManager.StartOpenCloseTransaction())
        {
            foreach (var id in ids)
            {
                try
                {
                    var entity = transaction.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity is null || entity.IsErased || !TryRecognize(transaction, entity, out var recognition) ||
                        !recognition.Discipline.HasValue || string.IsNullOrWhiteSpace(recognition.Category))
                    {
                        skipped++;
                        continue;
                    }
                    var extents = entity.GeometricExtents;
                    if (!FiniteExtents(extents))
                    {
                        skipped++;
                        continue;
                    }
                    if (!TryScale(extents.MinPoint.X, metersPerUnit, out var minX) ||
                        !TryScale(extents.MinPoint.Y, metersPerUnit, out var minY) ||
                        !TryScale(extents.MinPoint.Z, metersPerUnit, out var minZ) ||
                        !TryScale(extents.MaxPoint.X, metersPerUnit, out var maxX) ||
                        !TryScale(extents.MaxPoint.Y, metersPerUnit, out var maxY) ||
                        !TryScale(extents.MaxPoint.Z, metersPerUnit, out var maxZ))
                    {
                        skipped++;
                        continue;
                    }
                    var category = recognition.Category!;
                    result.Add(new CoordinationElement(
                        entity.Handle.ToString(),
                        recognition.Discipline.Value,
                        category,
                        CanonicalOrFallback(entity.Layer, category),
                        DefaultRegion,
                        new AxisAlignedBox(minX, minY, minZ, maxX, maxY, maxZ)));
                }
                catch (System.Exception ex) when (IsRecoverableEntityFailure(ex))
                {
                    skipped++;
                }
            }
            transaction.Commit();
        }
        result.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.ElementId, right.ElementId));
        return new ReadOnlyCollection<CoordinationElement>(result);
    }

    private static IReadOnlyList<ClashResult> DetectRelevant(IReadOnlyList<CoordinationElement> candidates, double clearanceM)
    {
        var disciplines = candidates.ToDictionary(static item => item.ElementId, static item => item.Discipline, StringComparer.OrdinalIgnoreCase);
        return new ClashDetectionService().Detect(candidates, clearanceM, includeSameDiscipline: true)
            .Where(item =>
                (disciplines.TryGetValue(item.LeftElementId, out var left) && left == MepDiscipline.Mep) ||
                (disciplines.TryGetValue(item.RightElementId, out var right) && right == MepDiscipline.Mep))
            .ToArray();
    }

    private static void WriteClash(Editor editor, ClashResult clash, int? index)
    {
        editor.WriteMessage(
            "  " + (index.HasValue ? index.Value.ToString(CultureInfo.InvariantCulture) + ". " : string.Empty) +
            clash.Kind + " | " + clash.LeftElementId + " <-> " + clash.RightElementId +
            " | gap=" + clash.SeparationM.ToString("0.###", CultureInfo.InvariantCulture) + " m\n");
    }

    private static bool TryRecognize(Transaction transaction, Entity entity, out MepRecognitionResult recognition)
    {
        var blockName = string.Empty;
        if (entity is BlockReference block)
        {
            try
            {
                var record = transaction.GetObject(block.BlockTableRecord, OpenMode.ForRead, false) as BlockTableRecord;
                blockName = record?.Name ?? string.Empty;
            }
            catch (System.Exception ex) when (IsRecoverableEntityFailure(ex))
            {
            }
        }
        recognition = MepRecognitionProfileProvider.Current.Recognize(entity.Layer, blockName);
        return recognition.Status == MepRecognitionStatus.Matched;
    }

    private static string Specification(Transaction transaction, Entity entity)
    {
        if (entity is BlockReference block)
        {
            try
            {
                var record = transaction.GetObject(block.BlockTableRecord, OpenMode.ForRead, false) as BlockTableRecord;
                if (record is not null && !string.IsNullOrWhiteSpace(record.Name)) return record.Name.Trim();
            }
            catch (System.Exception ex) when (IsRecoverableEntityFailure(ex))
            {
            }
        }
        return entity.GetRXClass()?.Name ?? entity.GetType().Name;
    }

    private static bool TryGetCurveLength(Entity entity, out double value)
    {
        value = 0d;
        if (entity is not Curve curve) return false;
        try
        {
            value = curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam);
            return FiniteNonNegative(value);
        }
        catch (System.Exception ex) when (IsRecoverableEntityFailure(ex))
        {
            value = 0d;
            return false;
        }
    }

    private static bool TryGetArea(Entity entity, out double value)
    {
        value = 0d;
        try
        {
            switch (entity)
            {
                case Polyline polyline when polyline.Closed:
                    value = polyline.Area;
                    break;
                case Circle circle:
                    value = Math.PI * circle.Radius * circle.Radius;
                    break;
                case Autodesk.AutoCAD.DatabaseServices.Region region:
                    value = region.Area;
                    break;
                default:
                    return false;
            }
            return FiniteNonNegative(value);
        }
        catch (System.Exception ex) when (IsRecoverableEntityFailure(ex))
        {
            value = 0d;
            return false;
        }
    }

    private static bool TryGetVolume(Entity entity, out double value)
    {
        value = 0d;
        if (entity is not Solid3d solid) return false;
        try
        {
            value = solid.MassProperties.Volume;
            return FiniteNonNegative(value);
        }
        catch (System.Exception ex) when (IsRecoverableEntityFailure(ex))
        {
            value = 0d;
            return false;
        }
    }

    private static bool TryGetMetersPerUnit(Database database, out double metersPerUnit, out string error)
    {
        metersPerUnit = ((int)database.Insunits) switch
        {
            1 => 0.0254d,
            2 => 0.3048d,
            3 => 1609.344d,
            4 => 0.001d,
            5 => 0.01d,
            6 => 1d,
            7 => 1000d,
            8 => 0.0000000254d,
            9 => 0.0000254d,
            10 => 0.9144d,
            11 => 1e-10d,
            12 => 1e-9d,
            13 => 1e-6d,
            14 => 0.1d,
            15 => 10d,
            16 => 100d,
            17 => 1e9d,
            18 => 149597870700d,
            19 => 9460730472580800d,
            20 => 3.0856775814913673e16d,
            21 => 1200d / 3937d,
            22 => 100d / 3937d,
            23 => 3600d / 3937d,
            24 => 6336000d / 3937d,
            _ => 0d
        };
        if (metersPerUnit > 0d && IsFinite(metersPerUnit))
        {
            error = string.Empty;
            return true;
        }
        error = "drawing INSUNITS is Unitless/unsupported; set a real drawing unit before MEP quantity or clearance calculation.";
        return false;
    }

    private static bool TryResolvePair(Database database, string left, string right, out ObjectId[] ids)
    {
        ids = Array.Empty<ObjectId>();
        if (!TryResolveHandle(database, left, out var leftId) || !TryResolveHandle(database, right, out var rightId) || leftId == rightId) return false;
        try
        {
            using var transaction = database.TransactionManager.StartOpenCloseTransaction();
            var leftEntity = transaction.GetObject(leftId, OpenMode.ForRead, false) as Entity;
            var rightEntity = transaction.GetObject(rightId, OpenMode.ForRead, false) as Entity;
            if (leftEntity is null || rightEntity is null || leftEntity.IsErased || rightEntity.IsErased) return false;
            transaction.Commit();
            ids = new[] { leftId, rightId };
            return true;
        }
        catch (System.Exception ex) when (IsRecoverableEntityFailure(ex))
        {
            return false;
        }
    }

    private static bool TryResolveHandle(Database database, string text, out ObjectId id)
    {
        id = ObjectId.Null;
        if (!long.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) || value <= 0) return false;
        try
        {
            id = database.GetObjectId(false, new Handle(value), 0);
            return !id.IsNull && id.IsValid;
        }
        catch (System.Exception ex) when (IsRecoverableEntityFailure(ex))
        {
            id = ObjectId.Null;
            return false;
        }
    }

    private static bool FiniteExtents(Extents3d extents) =>
        IsFinite(extents.MinPoint.X) && IsFinite(extents.MinPoint.Y) && IsFinite(extents.MinPoint.Z) &&
        IsFinite(extents.MaxPoint.X) && IsFinite(extents.MaxPoint.Y) && IsFinite(extents.MaxPoint.Z) &&
        extents.MaxPoint.X >= extents.MinPoint.X && extents.MaxPoint.Y >= extents.MinPoint.Y && extents.MaxPoint.Z >= extents.MinPoint.Z;

    private static bool ExtentsIntersect(Extents3d left, Extents3d right) =>
        left.MaxPoint.X >= right.MinPoint.X && right.MaxPoint.X >= left.MinPoint.X &&
        left.MaxPoint.Y >= right.MinPoint.Y && right.MaxPoint.Y >= left.MinPoint.Y &&
        left.MaxPoint.Z >= right.MinPoint.Z && right.MaxPoint.Z >= left.MinPoint.Z;

    private static void AddCorners(List<Point3d> result, Extents3d extents)
    {
        var min = extents.MinPoint;
        var max = extents.MaxPoint;
        result.Add(new Point3d(min.X, min.Y, min.Z));
        result.Add(new Point3d(min.X, min.Y, max.Z));
        result.Add(new Point3d(min.X, max.Y, min.Z));
        result.Add(new Point3d(min.X, max.Y, max.Z));
        result.Add(new Point3d(max.X, min.Y, min.Z));
        result.Add(new Point3d(max.X, min.Y, max.Z));
        result.Add(new Point3d(max.X, max.Y, min.Z));
        result.Add(new Point3d(max.X, max.Y, max.Z));
    }

    private static bool TryScale(double value, double factor, out double result)
    {
        result = value * factor;
        return IsFinite(result);
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool FiniteNonNegative(double value) => IsFinite(value) && value >= 0d;

    private static string CanonicalOrFallback(string? value, string fallback)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length == 0 ? fallback : text;
    }

    private static bool IsRecoverableEntityFailure(System.Exception exception) =>
        exception is not OutOfMemoryException && exception is not StackOverflowException && exception is not AccessViolationException;

    private sealed class ExactCandidate
    {
        public ExactCandidate(string handle, MepDiscipline discipline, Solid3d solid, Extents3d extents)
        {
            Handle = handle;
            Discipline = discipline;
            Solid = solid;
            Extents = extents;
        }
        public string Handle { get; }
        public MepDiscipline Discipline { get; }
        public Solid3d Solid { get; }
        public Extents3d Extents { get; }
    }

    private sealed class ExactPair
    {
        public ExactPair(string left, string right)
        {
            Left = left;
            Right = right;
        }
        public string Left { get; }
        public string Right { get; }
    }
}
