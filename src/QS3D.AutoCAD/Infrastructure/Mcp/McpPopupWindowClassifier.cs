using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal static class McpPopupWindowClassifier
{
    internal static bool BelongsToCurrentProcess(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        GetWindowThreadProcessId(hwnd, out var processId);
        using var process = Process.GetCurrentProcess();
        return processId == unchecked((uint)process.Id);
    }

    internal static string Classify(IntPtr hwnd)
    {
        if (!BelongsToCurrentProcess(hwnd)) return "foreign";
        var title = WindowText(hwnd).ToLowerInvariant();
        var cls = ClassName(hwnd).ToLowerInvariant();
        if (title.Contains("error") || title.Contains("fatal") || title.Contains("lỗi")) return "error";
        if (title.Contains("warning") || title.Contains("cảnh báo")) return "warning";
        if (cls.Contains("dialog") || cls.Contains("#32770")) return "modal";
        if (cls.Contains("command") || cls.Contains("prompt")) return "command";
        return "popup";
    }

    internal static string ClassName(IntPtr hwnd)
    {
        var buffer = new StringBuilder(256);
        return GetClassName(hwnd, buffer, buffer.Capacity) > 0 ? buffer.ToString() : string.Empty;
    }

    internal static string WindowText(IntPtr hwnd)
    {
        var length = Math.Min(GetWindowTextLength(hwnd), 1024);
        if (length <= 0) return string.Empty;
        var buffer = new StringBuilder(length + 1);
        GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr hWnd);
}
