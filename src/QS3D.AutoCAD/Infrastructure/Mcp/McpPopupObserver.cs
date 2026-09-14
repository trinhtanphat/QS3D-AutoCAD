using Timer = System.Threading.Timer;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Bounded same-process popup observation. It never dismisses or invokes a window automatically.</summary>
internal static class McpPopupObserver
{
    private const int MaxEvents = 64;
    private static readonly object Sync = new();
    private static readonly Queue<Dictionary<string, object?>> Events = new();
    private static readonly Dictionary<string, string> LastFingerprints = new(StringComparer.Ordinal);
    private static Timer? _timer;

    internal static void Start()
    {
        lock (Sync)
        {
            if (_timer is not null) return;
            _timer = new Timer(_ => Poll(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }

    internal static void Stop()
    {
        Timer? timer;
        lock (Sync) { timer = _timer; _timer = null; Events.Clear(); LastFingerprints.Clear(); }
        try { timer?.Dispose(); } catch { }
    }

    internal static string SnapshotJson()
    {
        lock (Sync)
        {
            return McpJson.Serialize(new Dictionary<string, object?>
            {
                ["running"] = _timer is not null,
                ["count"] = Events.Count,
                ["events"] = Events.ToArray(),
                ["sameProcessOnly"] = true,
                ["automaticDismissal"] = false
            });
        }
    }

    private static void Poll()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var main = process.MainWindowHandle;
            EnumWindows((hwnd, _) =>
            {
                if (hwnd == IntPtr.Zero || hwnd == main || !IsWindowVisible(hwnd) || !McpPopupWindowClassifier.BelongsToCurrentProcess(hwnd)) return true;
                var title = McpPopupWindowClassifier.WindowText(hwnd);
                var cls = McpPopupWindowClassifier.ClassName(hwnd);
                if (title.Length == 0 && cls.Length == 0) return true;
                var handle = hwnd.ToInt64().ToString("X", CultureInfo.InvariantCulture);
                var fingerprint = cls + "\n" + title;
                lock (Sync)
                {
                    if (LastFingerprints.TryGetValue(handle, out var prior) && string.Equals(prior, fingerprint, StringComparison.Ordinal)) return true;
                    LastFingerprints[handle] = fingerprint;
                    while (Events.Count >= MaxEvents) Events.Dequeue();
                    Events.Enqueue(new Dictionary<string, object?>
                    {
                        ["utc"] = DateTime.UtcNow.ToString("o"), ["windowHandle"] = handle,
                        ["class"] = Bound(cls, 256), ["title"] = Bound(title, 512),
                        ["classification"] = McpPopupWindowClassifier.Classify(hwnd), ["actionTaken"] = false
                    });
                }
                return true;
            }, IntPtr.Zero);
        }
        catch (Exception ex) { McpDiagnosticHub.Log("popup-observer", "poll failed: " + ex.Message); }
    }

    private static string Bound(string value, int max) => value.Length <= max ? value : value.Substring(0, max);
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
}
