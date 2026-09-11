using System.Diagnostics;
using System.Globalization;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Same-process background host boundary; never injects global input or captures pixels.</summary>
internal static class McpBackgroundHostRuntime
{
    internal const string DefaultMode = "background_only";

    internal static string SnapshotJson()
    {
        using var process = Process.GetCurrentProcess();
        var main = process.MainWindowHandle;
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["mode"] = DefaultMode,
            ["processId"] = process.Id,
            ["mainWindowHandle"] = main.ToInt64().ToString("X", CultureInfo.InvariantCulture),
            ["mainWindowOwnedByCurrentProcess"] = main != IntPtr.Zero && McpPopupWindowClassifier.BelongsToCurrentProcess(main),
            ["pixelCapture"] = false,
            ["globalInputInjection"] = false,
            ["semanticUi"] = true
        });
    }

    internal static void EnsureSameProcessWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !McpPopupWindowClassifier.BelongsToCurrentProcess(hwnd))
            throw new InvalidOperationException("Background UI target must belong to the current AutoCAD process.");
    }
}
