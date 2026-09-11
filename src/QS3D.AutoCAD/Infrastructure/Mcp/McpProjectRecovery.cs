using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Versioned recovery copies of already-saved DWGs; never overwrites the active drawing.</summary>
internal static class McpProjectRecovery
{
    internal const int MaxSnapshotsPerProject = 30;
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);
    private static readonly object Sync = new();
    private static System.Threading.Timer? _timer;
    private static DateTime _lastAttemptUtc;
    private static DateTime _lastBackupUtc;
    private static string _lastBackupPath = string.Empty;
    private static string _lastError = string.Empty;
    private static string _lastSource = string.Empty;
    private static long _lastLength = -1;
    private static long _lastWriteTicks = -1;

    internal static string BackupRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QS3D", "AutoCAD", "Recovery");

    internal static void Start()
    {
        lock (Sync)
        {
            if (_timer is not null) return;
            _timer = new System.Threading.Timer(_ => Tick(), null, TickInterval, TickInterval);
        }
        McpDiagnosticHub.Log("recovery", "versioned saved-DWG recovery copies enabled; drawingOverwrite=false");
    }

    internal static void Stop()
    {
        System.Threading.Timer? timer;
        lock (Sync) { timer = _timer; _timer = null; }
        try { timer?.Dispose(); } catch { }
    }
    internal static string SnapshotJson()
    {
        lock (Sync)
        {
            return McpJson.Serialize(new Dictionary<string, object?>
            {
                ["running"] = _timer is not null,
                ["backupRoot"] = BackupRoot,
                ["lastBackupUtc"] = _lastBackupUtc == DateTime.MinValue ? null : _lastBackupUtc.ToString("o", CultureInfo.InvariantCulture),
                ["lastBackupPath"] = _lastBackupPath,
                ["lastError"] = _lastError,
                ["maxSnapshotsPerProject"] = MaxSnapshotsPerProject,
                ["overwritesActiveDrawing"] = false
            });
        }
    }

    internal static string BuildPlanJson()
    {
        var source = TryGetActiveSource();
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["planBeforeMutation"] = true,
            ["drawingMutation"] = false,
            ["source"] = source?.Path,
            ["sourceAvailable"] = source is not null,
            ["actions"] = new[] { "capture-saved-copy", "recover-latest-to-new-copy" },
            ["automaticOverwrite"] = false
        });
    }

    internal static string CaptureNowFromLocalUser()
    {
        var captured = CaptureSavedCopy(true, out var message);
        return McpJson.Serialize(new Dictionary<string, object?> { ["captured"] = captured, ["message"] = message });
    }

    internal static string RecoverLatestToCopyFromLocalUser()
    {
        var source = TryGetActiveSource() ?? throw new InvalidOperationException("Active AutoCAD drawing has no saved DWG path.");
        var snapshots = ListSnapshots(source.Path);
        if (snapshots.Length == 0) throw new InvalidOperationException("No QS3D recovery copy exists for the active drawing.");
        var recovered = Path.Combine(BackupRoot, "Recovered");
        Directory.CreateDirectory(recovered);
        var destination = UniquePath(Path.Combine(recovered,
            SafeStem(Path.GetFileNameWithoutExtension(source.Path)) + "-RECOVERED-"
            + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".dwg"));
        CopyStableFile(snapshots[snapshots.Length - 1].FullName, destination);
        McpDiagnosticHub.Log("recovery", "explicit local recovery copy created; sourceSnapshot=" + snapshots[snapshots.Length - 1].Name);
        return McpJson.Serialize(new Dictionary<string, object?> { ["recoveredPath"] = destination, ["activeDrawingOverwritten"] = false });
    }
    private static void Tick()
    {
        try
        {
            lock (Sync)
            {
                if (_timer is null || DateTime.UtcNow - _lastAttemptUtc < SnapshotInterval) return;
                _lastAttemptUtc = DateTime.UtcNow;
            }
            CaptureSavedCopy(false, out _);
        }
        catch (Exception ex) { SetError("periodic recovery: " + ex.Message); }
    }

    private static bool CaptureSavedCopy(bool force, out string message)
    {
        var source = TryGetActiveSource();
        if (source is null) { message = "No idle saved DWG is available for recovery copy."; return false; }
        var before = new FileInfo(source.Path);
        if (!before.Exists || before.Length <= 0) { message = "Saved DWG is missing or empty."; return false; }
        lock (Sync)
        {
            if (!force && string.Equals(_lastSource, source.Path, StringComparison.OrdinalIgnoreCase)
                && _lastLength == before.Length && _lastWriteTicks == before.LastWriteTimeUtc.Ticks)
            { message = "Saved DWG has not changed since the last recovery copy."; return false; }
        }
        var folder = ProjectFolder(source.Path);
        Directory.CreateDirectory(folder);
        var destination = UniquePath(Path.Combine(folder, SafeStem(Path.GetFileNameWithoutExtension(source.Path)) + "-"
            + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".dwg"));
        CopyStableFile(source.Path, destination);
        var after = new FileInfo(source.Path);
        if (!after.Exists || before.Length != after.Length || before.LastWriteTimeUtc.Ticks != after.LastWriteTimeUtc.Ticks)
        {
            try { File.Delete(destination); } catch { }
            message = "DWG changed while the recovery copy was being captured; intermediate copy discarded.";
            return false;
        }
        lock (Sync)
        {
            _lastSource = source.Path;
            _lastLength = after.Length;
            _lastWriteTicks = after.LastWriteTimeUtc.Ticks;
            _lastBackupUtc = DateTime.UtcNow;
            _lastBackupPath = destination;
            _lastError = string.Empty;
        }
        TrimRetention(folder);
        message = "Recovery copy created: " + Path.GetFileName(destination);
        return true;
    }

    private static SourceState? TryGetActiveSource()
    {
        try
        {
            return McpDiagnosticHub.InvokeInCadContext(() =>
            {
                if (Convert.ToInt32(AcApplication.GetSystemVariable("CMDACTIVE"), CultureInfo.InvariantCulture) != 0) return null;
                var document = AcApplication.DocumentManager.MdiActiveDocument;
                if (document is null || string.IsNullOrWhiteSpace(document.Name)) return null;
                var path = Path.GetFullPath(document.Name);
                if (!Path.IsPathRooted(path) || !File.Exists(path)
                    || !string.Equals(Path.GetExtension(path), ".dwg", StringComparison.OrdinalIgnoreCase)) return null;
                return new SourceState(path);
            });
        }
        catch { return null; }
    }

    private static FileInfo[] ListSnapshots(string source)
    {
        var folder = ProjectFolder(source);
        if (!Directory.Exists(folder)) return Array.Empty<FileInfo>();
        return Directory.GetFiles(folder, "*.dwg", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path)).Where(info => info.Exists && info.Length > 0)
            .OrderBy(info => info.LastWriteTimeUtc).ToArray();
    }

    private static void CopyStableFile(string source, string destination)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        input.CopyTo(output, 1024 * 1024);
        output.Flush(true);
    }
    private static string ProjectFolder(string source)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(source).ToUpperInvariant()));
        var key = BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
        return Path.Combine(BackupRoot, SafeStem(Path.GetFileNameWithoutExtension(source)) + "-" + key);
    }

    private static void TrimRetention(string folder)
    {
        try
        {
            var files = Directory.GetFiles(folder, "*.dwg", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path)).OrderByDescending(info => info.LastWriteTimeUtc).ToArray();
            for (var index = MaxSnapshotsPerProject; index < files.Length; index++)
                try { files[index].Delete(); } catch { }
        }
        catch { }
    }

    private static string SafeStem(string? value)
    {
        var name = (value ?? string.Empty).Trim();
        if (name.Length == 0) name = "drawing";
        foreach (var invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        return name.Length <= 80 ? name : name.Substring(0, 80);
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var directory = Path.GetDirectoryName(path) ?? BackupRoot;
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var index = 1; index < 1000; index++)
        {
            var candidate = Path.Combine(directory, stem + "-" + index.ToString(CultureInfo.InvariantCulture) + extension);
            if (!File.Exists(candidate)) return candidate;
        }
        throw new IOException("Unable to allocate a unique recovery-copy path.");
    }

    private static void SetError(string message)
    {
        lock (Sync) _lastError = message ?? string.Empty;
        McpDiagnosticHub.Log("recovery-error", message ?? string.Empty);
    }

    private sealed record SourceState(string Path);
}
