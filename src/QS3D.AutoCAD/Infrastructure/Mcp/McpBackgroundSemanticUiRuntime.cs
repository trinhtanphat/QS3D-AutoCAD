using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Bounded same-process semantic discovery without OCR or global input.</summary>
internal static class McpBackgroundSemanticUiRuntime
{
    private const int MaxDepth = 8;
    private const int MaxNodes = 200;
    private static readonly object Sync = new();
    private static long _generation;
    private static Snapshot? _last;

    internal static string Discover(string body)
    {
        var maxDepth = McpTopLevelJson.OptionalInt(body, "maxDepth", 4, 1, MaxDepth);
        var maxNodes = McpTopLevelJson.OptionalInt(body, "maxNodes", 80, 1, MaxNodes);
        var root = ResolveRoot(body);
        var nodes = new List<Dictionary<string, object?>>();
        Walk(root, "root", 0, maxDepth, maxNodes, nodes);
        var generation = Interlocked.Increment(ref _generation);
        lock (Sync) _last = new Snapshot(generation, root);
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["mode"] = "semantic",
            ["windowHandle"] = Handle(root),
            ["expectedDiscoveryGeneration"] = generation,
            ["discoveryGeneration"] = generation,
            ["maxDepth"] = maxDepth,
            ["maxNodes"] = maxNodes,
            ["count"] = nodes.Count,
            ["nodes"] = nodes,
            ["background"] = true
        });
    }

    internal static string Invoke(string body)
    {
        var expected = McpTopLevelJson.OptionalInt(body, "expectedDiscoveryGeneration", 0, 0, int.MaxValue);
        var handleText = McpTopLevelJson.ExtractString(body, "controlHandle");
        if (!long.TryParse(handleText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var raw))
            throw new InvalidOperationException("controlHandle must be a hexadecimal same-process HWND.");
        var hwnd = new IntPtr(raw);
        if (!McpPopupWindowClassifier.BelongsToCurrentProcess(hwnd)) throw new InvalidOperationException("Semantic target must belong to the current AutoCAD process.");
        lock (Sync)
        {
            if (_last is null || _last.Generation != expected)
                throw new InvalidOperationException("expectedDiscoveryGeneration is stale; request fresh semantic discovery.");
            _last = null; // single-use discovery prevents replay after provider boundary
        }
        var cls = McpPopupWindowClassifier.ClassName(hwnd);
        if (!string.Equals(cls, "Button", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only an exact standard Button semantic target is currently invokable in background mode.");
        McpDesktopControlSession.RequireLocalConsent("autocad_ui_invoke");
        var result = SendMessageTimeout(hwnd, 0x00F5, IntPtr.Zero, IntPtr.Zero, 0x0002, 2000, out _);
        if (result == IntPtr.Zero)
            throw new InvalidOperationException("Semantic provider outcome is uncertain; no automatic retry. Inspect the actionId acknowledgement before recovery.");
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["invoked"] = true,
            ["background"] = true,
            ["controlHandle"] = Handle(hwnd),
            ["requiresRediscovery"] = true,
            ["retryAllowed"] = false
        });
    }

    private static IntPtr ResolveRoot(string body)
    {
        var requested = McpTopLevelJson.ExtractString(body, "windowHandle");
        if (requested.Length > 0 && long.TryParse(requested, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var raw))
        {
            var candidate = new IntPtr(raw);
            if (McpPopupWindowClassifier.BelongsToCurrentProcess(candidate)) return candidate;
            throw new InvalidOperationException("windowHandle must belong to the current AutoCAD process.");
        }
        using var process = Process.GetCurrentProcess();
        if (process.MainWindowHandle == IntPtr.Zero) throw new InvalidOperationException("AutoCAD main window is unavailable.");
        return process.MainWindowHandle;
    }

    private static void Walk(IntPtr hwnd, string path, int depth, int maxDepth, int maxNodes, List<Dictionary<string, object?>> nodes)
    {
        if (nodes.Count >= maxNodes || !McpPopupWindowClassifier.BelongsToCurrentProcess(hwnd)) return;
        nodes.Add(new Dictionary<string, object?>
        {
            ["elementPath"] = path,
            ["depth"] = depth,
            ["controlHandle"] = Handle(hwnd),
            ["controlType"] = McpPopupWindowClassifier.ClassName(hwnd),
            ["classification"] = McpPopupWindowClassifier.Classify(hwnd)
        });
        if (depth >= maxDepth) return;
        var index = 0;
        EnumChildWindows(hwnd, (child, _) =>
        {
            if (nodes.Count >= maxNodes) return false;
            Walk(child, path + "/" + index.ToString(CultureInfo.InvariantCulture), depth + 1, maxDepth, maxNodes, nodes);
            index++;
            return nodes.Count < maxNodes;
        }, IntPtr.Zero);
    }

    private static string Handle(IntPtr hwnd) => hwnd.ToInt64().ToString("X", CultureInfo.InvariantCulture);

    private sealed class Snapshot
    {
        internal Snapshot(long generation, IntPtr root) { Generation = generation; Root = root; }
        internal long Generation { get; }
        internal IntPtr Root { get; }
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
}
