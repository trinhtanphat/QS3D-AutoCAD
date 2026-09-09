using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Same-process AutoCAD UI observation/control. Background-only is the process-start default.</summary>
internal static class McpBackgroundHostRuntime
{
    private const int BackgroundOnly = 0;
    private const int ForegroundEnabled = 1;
    private const int MaxItems = 200;
    private const int MaxText = 32768;
    private const uint WmSetText = 0x000C;
    private const uint SmtoAbortIfHung = 0x0002;
    private static int _interactionPolicy = BackgroundOnly;

    internal static bool IsForegroundPolicyEnabled => Volatile.Read(ref _interactionPolicy) == ForegroundEnabled;
    internal static bool IsForegroundAvailable => IsForegroundPolicyEnabled && McpDesktopControlSession.IsEnabled;

    internal static void ResetForProcessStart()
    {
        Interlocked.Exchange(ref _interactionPolicy, BackgroundOnly);
        McpDesktopControlSession.Disable("process-start-default");
    }

    internal static void EnableForegroundFromLocalUser()
    {
        McpDesktopControlSession.RequireLocalConsent("foreground-local-enable");
        Interlocked.Exchange(ref _interactionPolicy, ForegroundEnabled);
        McpDiagnosticHub.Log("interaction-policy", "foreground enabled by explicit local user action");
    }

    internal static void DisableForegroundFromLocalUser()
    {
        Interlocked.Exchange(ref _interactionPolicy, BackgroundOnly);
        McpDiagnosticHub.Log("interaction-policy", "background_only");
    }

    internal static void EnsureGlobalInteractionAllowed(string toolName)
    {
        if (!UsesGlobalInteraction(toolName)) return;
        if (!IsForegroundPolicyEnabled)
            throw new InvalidOperationException("Global desktop input is disabled by the AutoCAD MCP background_only policy.");
        McpDesktopControlSession.RequireLocalConsent(toolName ?? "foreground-global-interaction");
    }

    internal static string Call(string tool, string body)
    {
        var args = string.IsNullOrWhiteSpace(body) ? "{}" : body;
        return tool switch
        {
            "autocad_interaction_policy_get" => PolicyJson(),
            "autocad_interaction_policy_set" => SetRemotePolicy(args),
            "autocad_ui_text_snapshot" => TextSnapshot(args),
            "autocad_ui_invoke" => McpBackgroundSemanticUiRuntime.Invoke(args),
            "autocad_ui_set_text" => SetText(args),
            _ => throw new InvalidOperationException("Unknown AutoCAD background-host MCP tool: " + tool)
        };
    }

    private static string SetRemotePolicy(string body)
    {
        var mode = McpTopLevelJson.ExtractString(body, "mode").Trim().ToLowerInvariant();
        if (mode.Length > 0 && mode != "background_only")
            throw new InvalidOperationException("Foreground Control can only be enabled by an explicit local Agent Center action.");
        if (McpTopLevelJson.TryGetBoolean(body, "enabled", out var enabled) && enabled)
            throw new InvalidOperationException("Remote MCP calls cannot enable Foreground Control; use the local Agent Center.");
        Interlocked.Exchange(ref _interactionPolicy, BackgroundOnly);
        return PolicyJson();
    }

    private static string PolicyJson() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["mode"] = IsForegroundPolicyEnabled ? "foreground_explicit" : "background_only",
        ["defaultMode"] = "background_only",
        ["processStartDefault"] = "background_only",
        ["requiresLocalConsent"] = true,
        ["localConsent"] = McpDesktopControlSession.IsEnabled,
        ["foregroundAvailable"] = IsForegroundAvailable,
        ["implicitForegroundFallback"] = false
    });

    private static string TextSnapshot(string body)
    {
        var mode = McpTopLevelJson.ExtractString(body, "mode").Trim().ToLowerInvariant();
        if (mode == "semantic") return McpBackgroundSemanticUiRuntime.Discover(body);
        if (mode.Length > 0 && mode != "text") throw new InvalidOperationException("mode must be text or semantic.");
        var scope = McpTopLevelJson.ExtractString(body, "scope").Trim().ToLowerInvariant();
        if (scope.Length == 0) scope = "all";
        if (scope != "all" && scope != "popup" && scope != "commandline")
            throw new InvalidOperationException("scope must be all, popup or commandline.");
        var limit = McpTopLevelJson.OptionalInt(body, "limit", 80, 1, MaxItems);
        var items = new List<Dictionary<string, object?>>();
        var budget = MaxText;
        EnumWindows((hwnd, _) =>
        {
            if (items.Count >= limit || budget <= 0) return false;
            if (!McpPopupWindowClassifier.BelongsToCurrentProcess(hwnd) || !IsWindowVisible(hwnd)) return true;
            AddItem(hwnd, true, scope, items, ref budget, limit);
            EnumChildWindows(hwnd, (child, __) =>
            {
                if (items.Count >= limit || budget <= 0) return false;
                if (McpPopupWindowClassifier.BelongsToCurrentProcess(child) && IsWindowVisible(child))
                    AddItem(child, false, scope, items, ref budget, limit);
                return items.Count < limit && budget > 0;
            }, IntPtr.Zero);
            return items.Count < limit && budget > 0;
        }, IntPtr.Zero);
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["mode"] = "text", ["scope"] = scope, ["count"] = items.Count,
            ["items"] = items, ["truncated"] = items.Count >= limit, ["background"] = true
        });
    }

    private static void AddItem(IntPtr hwnd, bool topLevel, string scope, List<Dictionary<string, object?>> items, ref int budget, int limit)
    {
        if (items.Count >= limit || budget <= 0) return;
        var cls = McpPopupWindowClassifier.ClassName(hwnd);
        if (scope == "popup" && topLevel == false && !cls.IndexOf("Dialog", StringComparison.OrdinalIgnoreCase) >= 0 && cls != "#32770") return;
        if (scope == "commandline" && !LooksLikeCommandLine(cls)) return;
        var text = McpPopupWindowClassifier.WindowText(hwnd);
        if (text.Length == 0) return;
        if (text.Length > budget) text = text.Substring(0, budget);
        budget -= text.Length;
        items.Add(new Dictionary<string, object?>
        {
            ["handle"] = Handle(hwnd), ["class"] = cls, ["text"] = text,
            ["topLevel"] = topLevel, ["classification"] = McpPopupWindowClassifier.Classify(hwnd)
        });
    }

    private static bool LooksLikeCommandLine(string cls)
    {
        var value = (cls ?? string.Empty).ToUpperInvariant();
        return value.Contains("EDIT") || value.Contains("RICH") || value.Contains("COMMAND") || value.Contains("PROMPT");
    }

    private static string SetText(string body)
    {
        var hwnd = RequiredHandle(body, "controlHandle");
        var cls = McpPopupWindowClassifier.ClassName(hwnd);
        if (!LooksLikeCommandLine(cls)) throw new InvalidOperationException("autocad_ui_set_text accepts only a same-process Edit/RichEdit/command control.");
        var text = McpTopLevelJson.ExtractString(body, "text");
        if (text.Length > 4000 || text.IndexOf('\0') >= 0) throw new InvalidOperationException("text exceeds the bounded background-control limit.");
        var result = SendMessageTimeout(hwnd, WmSetText, IntPtr.Zero, text, SmtoAbortIfHung, 2000, out _);
        if (result == IntPtr.Zero) throw new InvalidOperationException("Background WM_SETTEXT failed or timed out.");
        return McpJson.Serialize(new Dictionary<string, object?> { ["updated"] = true, ["background"] = true, ["controlHandle"] = Handle(hwnd) });
    }

    private static IntPtr RequiredHandle(string body, string property)
    {
        var token = McpTopLevelJson.ExtractString(body, property).Trim();
        if (!long.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var raw))
            throw new InvalidOperationException(property + " must be a hexadecimal HWND.");
        var hwnd = new IntPtr(raw);
        if (!McpPopupWindowClassifier.BelongsToCurrentProcess(hwnd)) throw new InvalidOperationException(property + " must belong to the current AutoCAD process.");
        return hwnd;
    }

    private static bool UsesGlobalInteraction(string toolName) => (toolName ?? string.Empty) switch
    {
        "desktop_screenshot" or "desktop_ui_tree" or "desktop_window_focus" or "desktop_window_set_state" or "desktop_window_move_resize" or
        "desktop_mouse_move" or "desktop_mouse_click" or "desktop_mouse_scroll" or "desktop_mouse_drag" or "desktop_type" or "desktop_key" or
        "desktop_clipboard_read" or "desktop_clipboard_write" or "desktop_sequence" => true,
        _ => false
    };

    private static string Handle(IntPtr hwnd) => hwnd.ToInt64().ToString("X", CultureInfo.InvariantCulture);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);
}
