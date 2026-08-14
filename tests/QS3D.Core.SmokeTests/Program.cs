using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QS3D.Core.Commercial;
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

var levelLow = ElementFactory.Marker(ElementKind.Level, "Roof", new Point3(0, 0, 0), new Point3(0, 0, 0));
var levelMid = ElementFactory.Marker(ElementKind.Level, "Ground", new Point3(0, 0, 3.6), new Point3(0, 0, 3.6));
var levelHigh = ElementFactory.Marker(ElementKind.Level, "Mezz", new Point3(0, 0, 7.2), new Point3(0, 0, 7.2));
var orderedLevels = ReferenceManagerService.OrderLevels([levelHigh, beam, levelLow, levelMid]);
Assert(orderedLevels.Select(item => item.Id).SequenceEqual([levelLow.Id, levelMid.Id, levelHigh.Id]), "levels must order by elevation, not DB/input order");
var descendingLevels = ReferenceManagerService.OrderLevels([levelLow, levelHigh, levelMid], descending: true);
Assert(descendingLevels.Select(item => item.Id).SequenceEqual([levelHigh.Id, levelMid.Id, levelLow.Id]), "descending Level manager order");
var renamedLevel = ReferenceManagerService.RenameReference(levelMid, " Level 02 ");
Assert(renamedLevel.Name == "Level 02", "reference rename trims name");
Assert(renamedLevel.Id == levelMid.Id, "reference rename must preserve semantic identity");

var gridLeft = ElementFactory.Marker(ElementKind.Grid, "Z", new Point3(-3, -5, 0), new Point3(-3, 5, 0));
var gridCenterReversed = ElementFactory.Marker(ElementKind.Grid, "Y", new Point3(0, 5, 0), new Point3(0, -5, 0));
var gridRight = ElementFactory.Marker(ElementKind.Grid, "X", new Point3(4, -5, 0), new Point3(4, 5, 0));
var orderedGrids = ReferenceManagerService.OrderParallelGrids([gridRight, gridCenterReversed, gridLeft]);
Assert(orderedGrids.Select(item => item.Id).SequenceEqual([gridLeft.Id, gridCenterReversed.Id, gridRight.Id]), "parallel Grids must order by spatial offset regardless of line direction/input order");
AssertThrows<InvalidOperationException>(
    () => ReferenceManagerService.OrderParallelGrids([gridLeft, gridHorizontal]),
    "non-parallel Grid families must not be silently resequenced together");

var licenseNow = new DateTimeOffset(2026, 8, 14, 16, 0, 0, TimeSpan.Zero);
var lease = new LicenseLeaseSnapshot(
    "account-1",
    "subscription-1",
    "device-1",
    "seat-1",
    licenseNow.AddHours(-1),
    licenseNow.AddHours(1),
    licenseNow.AddDays(2));
var activeLicense = LicensePolicy.Evaluate(lease, licenseNow, "device-1");
Assert(activeLicense.Access == LicenseAccess.Active && activeLicense.CanAuthor, "valid lease must authorize during online validity");
var graceLicense = LicensePolicy.Evaluate(lease, licenseNow.AddHours(2), "device-1");
Assert(graceLicense.Access == LicenseAccess.OfflineGrace && graceLicense.CanAuthor, "expired online lease may authorize only inside explicit offline grace");
var expiredLicense = LicensePolicy.Evaluate(lease, licenseNow.AddDays(3), "device-1");
Assert(expiredLicense.Access == LicenseAccess.Denied && !expiredLicense.CanAuthor && expiredLicense.Reason == "expired", "lease must fail closed after offline grace");
var wrongDevice = LicensePolicy.Evaluate(lease, licenseNow, "device-2");
Assert(wrongDevice.Access == LicenseAccess.Denied && wrongDevice.Reason == "device_mismatch", "device mismatch must fail closed");
Assert(LicensePolicy.Evaluate(null, licenseNow, "device-1").Access == LicenseAccess.Denied, "missing lease must fail closed");
Assert(
    LicensePolicy.Evaluate(lease with { OfflineGraceUntilUtc = lease.ValidUntilUtc.AddMinutes(-1) }, licenseNow, "device-1").Reason == "invalid_lease",
    "invalid lease timestamp ordering must fail closed");
AssertThrows<ArgumentException>(
    () => LicensePolicy.Evaluate(lease, licenseNow, " "),
    "missing expected device identity must be rejected");

var packageBytes = Encoding.UTF8.GetBytes("qs3d-update-package-v1");
var packageSha256 = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();
var updatePayload = new UpdateManifestPayload(
    1,
    "stable",
    "1.2.3",
    2025,
    2027,
    "https://updates.example.invalid/QS3D-AutoCAD-1.2.3.zip",
    packageSha256,
    licenseNow);
using var updateSigningKey = RSA.Create();
updateSigningKey.KeySize = 2048;
var updaterPublicKey = updateSigningKey.ExportSubjectPublicKeyInfoPem();
var signedManifest = SignManifest(updatePayload, updateSigningKey);
var verifiedUpdate = UpdateManifestVerifier.Verify(signedManifest, updaterPublicKey, 2026, "stable");
Assert(verifiedUpdate.Version == "1.2.3" && verifiedUpdate.PackageSha256 == packageSha256, "signed update manifest must preserve verified payload");
UpdateManifestVerifier.VerifyPackage(packageBytes, verifiedUpdate.PackageSha256);
AssertThrows<InvalidDataException>(
    () => UpdateManifestVerifier.VerifyPackage(Encoding.UTF8.GetBytes("tampered-package"), verifiedUpdate.PackageSha256),
    "tampered update package must fail SHA-256 verification");
AssertThrows<InvalidDataException>(
    () => UpdateManifestVerifier.Verify(signedManifest, updaterPublicKey, 2028, "stable"),
    "manifest outside AutoCAD generation range must be rejected");
AssertThrows<InvalidDataException>(
    () => UpdateManifestVerifier.Verify(signedManifest, updaterPublicKey, 2026, "preview"),
    "manifest from another update channel must be rejected");
var tamperedPayloadBytes = JsonSerializer.SerializeToUtf8Bytes(updatePayload with { Version = "9.9.9" });
var tamperedEnvelope = JsonSerializer.Serialize(new UpdateManifestEnvelope(
    Convert.ToBase64String(tamperedPayloadBytes),
    JsonSerializer.Deserialize<UpdateManifestEnvelope>(signedManifest)!.SignatureBase64));
AssertThrows<InvalidDataException>(
    () => UpdateManifestVerifier.Verify(tamperedEnvelope, updaterPublicKey, 2026, "stable"),
    "manifest payload tampering must invalidate signature");
AssertThrows<InvalidDataException>(
    () => UpdateManifestVerifier.Verify(
        SignManifest(updatePayload with { PackageUri = "http://updates.example.invalid/QS3D.zip" }, updateSigningKey),
        updaterPublicKey,
        2026,
        "stable"),
    "non-HTTPS package URI must be rejected even with a valid signature");
AssertThrows<InvalidDataException>(
    () => UpdateManifestVerifier.Verify(
        SignManifest(updatePayload with { PackageSha256 = "abcd" }, updateSigningKey),
        updaterPublicKey,
        2026,
        "stable"),
    "malformed package hash must be rejected even with a valid signature");

Console.WriteLine("QS3D.Core smoke tests passed.");

static string SignManifest(UpdateManifestPayload payload, RSA key)
{
    var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
    var signature = key.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
    return JsonSerializer.Serialize(new UpdateManifestEnvelope(
        Convert.ToBase64String(payloadBytes),
        Convert.ToBase64String(signature)));
}

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
