using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Passively observes bounded popup metadata owned by the current AutoCAD process.</summary>
internal static class McpPopupObserver
{
    private const uint EventSystemDialogStart = 0x0010;
    private const uint EventSystemDialogEnd = 0x0011;
    private const uint EventObjectShow = 0x8002;
    private const int ObjIdWindow = 0;
    private static readonly object Sync = new();
    private static readonly Dictionary<long, Seen> LastSeen = new();
    private static readonly WinEventDelegate Callback = OnWinEvent;
    private static IntPtr _dialogHook;
    private static IntPtr _showHook;
    private static uint _processId;
    private static bool _started;

    internal static void Start()
    {
        lock (Sync)
        {
            if (_started) return;
            using var process = Process.GetCurrentProcess();
            _processId = unchecked((uint)process.Id);
            _dialogHook = SetWinEventHook(EventSystemDialogStart, EventSystemDialogEnd, IntPtr.Zero, Callback, _processId, 0, 0);
            _showHook = SetWinEventHook(EventObjectShow, EventObjectShow, IntPtr.Zero, Callback, _processId, 0, 0);
            if (_dialogHook == IntPtr.Zero && _showHook == IntPtr.Zero)
                throw new InvalidOperationException("AutoCAD popup observer could not install a same-process Windows event hook.");
            _started = true;
        }
        McpDiagnosticHub.Log("popup-observer", "same-process passive popup observation started; automaticDismiss=false");
    }
    internal static void Stop()
    {
        IntPtr dialog;
        IntPtr show;
        lock (Sync)
        {
            if (!_started) return;
            _started = false;
            dialog = _dialogHook;
            show = _showHook;
            _dialogHook = IntPtr.Zero;
            _showHook = IntPtr.Zero;
            LastSeen.Clear();
        }
        if (dialog != IntPtr.Zero) try { UnhookWinEvent(dialog); } catch { }
        if (show != IntPtr.Zero) try { UnhookWinEvent(show); } catch { }
        McpDiagnosticHub.Log("popup-observer", "same-process passive popup observation stopped");
    }

    internal static string SnapshotJson()
    {
        lock (Sync)
        {
            return McpJson.Serialize(new Dictionary<string, object?>
            {
                ["running"] = _started,
                ["processId"] = _processId,
                ["trackedPopups"] = LastSeen.Count,
                ["sameProcessOnly"] = true,
                ["automaticDismiss"] = false
            });
        }
    }

    private static void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint eventThread, uint eventTime)
    {
        if (hwnd == IntPtr.Zero) return;
        if (eventType == EventObjectShow && (idObject != ObjIdWindow || idChild != 0)) return;
        if (!McpPopupWindowClassifier.BelongsToCurrentProcess(hwnd) || !IsWindowVisible(hwnd)) return;
        ThreadPool.QueueUserWorkItem(_ => Capture(hwnd));
    }
    private static void Capture(IntPtr hwnd)
    {
        try
        {
            Thread.Sleep(60);
            if (!McpPopupWindowClassifier.BelongsToCurrentProcess(hwnd) || !IsWindowVisible(hwnd)) return;
            var title = Bound(McpPopupWindowClassifier.WindowText(hwnd), 512);
            var cls = Bound(McpPopupWindowClassifier.ClassName(hwnd), 256);
            var classification = McpPopupWindowClassifier.Classify(hwnd);
            var signature = title + "\n" + cls + "\n" + classification;
            var now = DateTime.UtcNow;
            lock (Sync)
            {
                if (!_started) return;
                var key = hwnd.ToInt64();
                if (LastSeen.TryGetValue(key, out var seen)
                    && string.Equals(seen.Signature, signature, StringComparison.Ordinal)
                    && (now - seen.Utc).TotalSeconds < 10) return;
                LastSeen[key] = new Seen(signature, now);
                foreach (var stale in LastSeen.Where(item => (now - item.Value.Utc).TotalMinutes > 2).Select(item => item.Key).ToArray())
                    LastSeen.Remove(stale);
                while (LastSeen.Count > 64) LastSeen.Remove(LastSeen.Keys.First());
            }
            McpDiagnosticHub.Log("popup-notification",
                "handle=0x" + hwnd.ToInt64().ToString("X", CultureInfo.InvariantCulture)
                + "; class=" + cls + "; classification=" + classification + "; title=" + title);
        }
        catch { }
    }

    private static string Bound(string? value, int max)
    {
        var text = value ?? string.Empty;
        return text.Length <= max ? text : text.Substring(0, max);
    }

    private sealed record Seen(string Signature, DateTime Utc);
    private delegate void WinEventDelegate(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint eventThread, uint eventTime);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventDelegate callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr hwnd);
}
