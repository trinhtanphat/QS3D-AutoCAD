using System.Text;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal static class McpDiagnosticHub
{
    private const int MaxEvents = 256;
    private const long MaxAuditBytes = 4L * 1024L * 1024L;
    private static readonly object Sync = new();
    private static readonly Queue<DiagnosticEntry> Entries = new();
    private static int _pluginThreadId;

    internal static string AuditFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QS3D", "mcp-agent-audit.jsonl");

    internal static void InitializeForPlugin()
    {
        _pluginThreadId = Environment.CurrentManagedThreadId;
        Log("mcp", "plugin-context initialized");
    }

    internal static T InvokeInCadContext<T>(Func<T> action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (_pluginThreadId != 0 && Environment.CurrentManagedThreadId == _pluginThreadId) return action();

        T? result = default;
        Exception? error = null;
        using var completed = new ManualResetEventSlim(false);
        AcApplication.DocumentManager.ExecuteInApplicationContext(_ =>
        {
            try { result = action(); }
            catch (Exception exception) { error = exception; }
            finally { completed.Set(); }
        }, null);
        if (!completed.Wait(8000)) throw new TimeoutException("Timed out waiting for AutoCAD application context.");
        if (error is not null) throw new InvalidOperationException("AutoCAD application-context operation failed: " + error.Message, error);
        return result!;
    }

    internal static void Log(string source, string detail)
    {
        var entry = new DiagnosticEntry(DateTime.UtcNow, Bounded(source, 80), Bounded(detail, 2048));
        lock (Sync)
        {
            while (Entries.Count >= MaxEvents) Entries.Dequeue();
            Entries.Enqueue(entry);
            Monitor.PulseAll(Sync);
        }
        TryAppendAudit(entry);
    }

    internal static string TailJson(int limit)
    {
        limit = Math.Max(1, Math.Min(100, limit));
        List<DiagnosticEntry> snapshot;
        lock (Sync) snapshot = Entries.Reverse().Take(limit).Reverse().ToList();
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["count"] = snapshot.Count,
            ["events"] = snapshot.Select(ToObject).ToList()
        });
    }

    internal static string SinceJson(DateTime sinceUtc, int limit)
    {
        limit = Math.Max(1, Math.Min(100, limit));
        List<DiagnosticEntry> snapshot;
        lock (Sync) snapshot = Entries.Where(item => item.Utc >= sinceUtc).TakeLastCompat(limit).ToList();
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["sinceUtc"] = sinceUtc.ToUniversalTime().ToString("o"),
            ["count"] = snapshot.Count,
            ["events"] = snapshot.Select(ToObject).ToList()
        });
    }

    internal static string WaitJson(int timeoutMs)
    {
        timeoutMs = Math.Max(0, Math.Min(7000, timeoutMs));
        lock (Sync)
        {
            var before = Entries.Count;
            if (timeoutMs > 0) Monitor.Wait(Sync, timeoutMs);
            var latest = Entries.Count == 0 ? null : Entries.Last();
            return McpJson.Serialize(new Dictionary<string, object?>
            {
                ["changed"] = Entries.Count != before,
                ["latest"] = latest is null ? null : ToObject(latest)
            });
        }
    }

    private static Dictionary<string, object?> ToObject(DiagnosticEntry entry) => new()
    {
        ["utc"] = entry.Utc.ToString("o"),
        ["source"] = entry.Source,
        ["detail"] = entry.Detail
    };

    private static void TryAppendAudit(DiagnosticEntry entry)
    {
        try
        {
            var directory = Path.GetDirectoryName(AuditFilePath);
            if (string.IsNullOrWhiteSpace(directory)) return;
            Directory.CreateDirectory(directory);
            if (File.Exists(AuditFilePath) && new FileInfo(AuditFilePath).Length > MaxAuditBytes)
            {
                var rotated = AuditFilePath + ".1";
                if (File.Exists(rotated)) File.Delete(rotated);
                File.Move(AuditFilePath, rotated);
            }
            var line = McpJson.Serialize(ToObject(entry)) + Environment.NewLine;
            File.AppendAllText(AuditFilePath, line, new UTF8Encoding(false));
        }
        catch
        {
            // Diagnostics must never crash AutoCAD or the MCP host.
        }
    }

    private static string Bounded(string? value, int max)
    {
        var text = value ?? string.Empty;
        return text.Length <= max ? text : text.Substring(0, max);
    }

    private sealed record DiagnosticEntry(DateTime Utc, string Source, string Detail);

    private static IEnumerable<T> TakeLastCompat<T>(this IEnumerable<T> source, int count)
    {
        var queue = new Queue<T>();
        foreach (var item in source)
        {
            if (queue.Count == count) queue.Dequeue();
            queue.Enqueue(item);
        }
        return queue;
    }
}