using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using QS3D.AutoCAD.UI;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal static class McpDesktopAutomationRuntime
{
    private const int MaxWindows = 100;
    private const int MaxTypedCharacters = 8000;
    private const int MaxClipboardCharacters = 65536;
    private const int MaxSequenceSteps = 12;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint MouseRightDown = 0x0008;
    private const uint MouseRightUp = 0x0010;
    private const uint MouseMiddleDown = 0x0020;
    private const uint MouseMiddleUp = 0x0040;
    private const uint MouseWheel = 0x0800;
    private const int SwRestore = 9;
    private const int SwMinimize = 6;
    private const int SwMaximize = 3;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    private static readonly HashSet<string> Tools = new(StringComparer.Ordinal)
    {
        "diagnostics_log_tail", "diagnostics_since", "diagnostics_snapshot", "diagnostics_wait",
        "theme_get", "theme_set", "autocad_interaction_policy_get", "autocad_interaction_policy_set",
        "autocad_ui_text_snapshot", "autocad_ui_invoke", "autocad_ui_set_text",
        "desktop_cursor_position", "desktop_window_list", "desktop_foreground_window", "desktop_wait_for_window",
        "desktop_window_focus", "desktop_window_set_state", "desktop_window_move_resize", "desktop_ui_tree",
        "desktop_mouse_move", "desktop_mouse_click", "desktop_mouse_scroll", "desktop_mouse_drag",
        "desktop_type", "desktop_key", "desktop_clipboard_read", "desktop_clipboard_write", "desktop_screenshot", "desktop_sequence"
    };

    private static readonly HashSet<string> LocalConsentTools = new(StringComparer.Ordinal)
    {
        "autocad_ui_invoke", "autocad_ui_set_text", "desktop_window_focus", "desktop_window_set_state", "desktop_window_move_resize",
        "desktop_mouse_move", "desktop_mouse_click", "desktop_mouse_scroll", "desktop_mouse_drag", "desktop_type", "desktop_key",
        "desktop_clipboard_write", "desktop_sequence"
    };

    private static readonly HashSet<string> SensitiveReadTools = new(StringComparer.Ordinal)
    {
        "desktop_clipboard_read", "desktop_screenshot", "desktop_ui_tree"
    };

    private static readonly string ConsentPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QS3D", "MCP", "desktop-consent.txt");
    private static volatile bool _interactionEnabled;

    internal static bool IsTool(string? tool) => Tools.Contains(tool ?? string.Empty);
    internal static bool RequiresMutation(string? tool) => McpToolRegistry.RequiresMutation(tool);
    internal static IEnumerable<McpToolDescriptor> ToolDescriptors() => McpToolRegistry.ToolDescriptors().Where(item => IsTool(item.Name));

    internal static string Call(string tool, string body)
    {
        if (!IsTool(tool)) throw new InvalidOperationException("Unknown AutoCAD desktop MCP tool: " + tool);
        if (LocalConsentTools.Contains(tool) || SensitiveReadTools.Contains(tool)) RequireLocalConsent(tool);
        return tool switch
        {
            "diagnostics_log_tail" => McpDiagnosticHub.TailJson(McpTopLevelJson.OptionalInt(body, "limit", 25, 1, 100)),
            "diagnostics_since" => DiagnosticsSince(body),
            "diagnostics_snapshot" => DiagnosticsSnapshot(),
            "diagnostics_wait" => McpDiagnosticHub.WaitJson(McpTopLevelJson.OptionalInt(body, "timeoutMs", 1000, 0, 7000)),
            "theme_get" => ThemeGet(),
            "theme_set" => ThemeSet(body),
            "autocad_interaction_policy_get" => InteractionPolicyGet(),
            "autocad_interaction_policy_set" => InteractionPolicySet(body),
            "autocad_ui_text_snapshot" => AutoCadUiTextSnapshot(),
            "autocad_ui_invoke" => AutoCadUiInvoke(body),
            "autocad_ui_set_text" => AutoCadUiSetText(body),
            "desktop_cursor_position" => CursorPosition(),
            "desktop_window_list" => WindowList(McpTopLevelJson.OptionalInt(body, "limit", 50, 1, MaxWindows)),
            "desktop_foreground_window" => ForegroundWindow(),
            "desktop_wait_for_window" => WaitForWindow(body),
            "desktop_window_focus" => WindowFocus(body),
            "desktop_window_set_state" => WindowSetState(body),
            "desktop_window_move_resize" => WindowMoveResize(body),
            "desktop_ui_tree" => WindowList(MaxWindows),
            "desktop_mouse_move" => MouseMove(body),
            "desktop_mouse_click" => MouseClick(body),
            "desktop_mouse_scroll" => MouseScroll(body),
            "desktop_mouse_drag" => MouseDrag(body),
            "desktop_type" => TypeText(body),
            "desktop_key" => PressKey(body),
            "desktop_clipboard_read" => ClipboardRead(),
            "desktop_clipboard_write" => ClipboardWrite(body),
            "desktop_screenshot" => Screenshot(),
            "desktop_sequence" => Sequence(body),
            _ => throw new InvalidOperationException("Unknown AutoCAD desktop MCP tool: " + tool)
        };
    }

    private static string DiagnosticsSince(string body)
    {
        var token = McpTopLevelJson.ExtractString(body, "sinceUtc");
        if (!DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var since))
            since = DateTime.UtcNow.AddMinutes(-5);
        return McpDiagnosticHub.SinceJson(since, McpTopLevelJson.OptionalInt(body, "limit", 50, 1, 100));
    }

    private static string DiagnosticsSnapshot() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["mcp"] = McpJson.Parse(McpCadAgentRuntime.Call("mcp_status", "{}")),
        ["policy"] = McpJson.Parse(InteractionPolicyGet()),
        ["diagnostics"] = McpJson.Parse(McpDiagnosticHub.TailJson(20))
    });

    private static string ThemeGet() => McpDiagnosticHub.InvokeInCadContext(() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["mode"] = Qs3dThemeManager.Mode.ToString(),
        ["resolved"] = Qs3dThemeManager.Current.Resolved.ToString(),
        ["persistenceError"] = Qs3dThemeManager.LastPersistenceError
    }));

    private static string ThemeSet(string body)
    {
        var value = McpTopLevelJson.ExtractString(body, "mode");
        if (!Enum.TryParse(value, true, out Qs3dThemeMode mode) || !Enum.IsDefined(typeof(Qs3dThemeMode), mode))
            throw new InvalidOperationException("mode must be System, Light or Dark.");
        return McpDiagnosticHub.InvokeInCadContext(() =>
        {
            Qs3dThemeManager.SetMode(mode);
            return ThemeGetInContext();
        });
    }

    private static string ThemeGetInContext() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["mode"] = Qs3dThemeManager.Mode.ToString(), ["resolved"] = Qs3dThemeManager.Current.Resolved.ToString(), ["updated"] = true
    });

    private static string InteractionPolicyGet() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["enabled"] = _interactionEnabled,
        ["localConsentPresent"] = LocalConsentPresent(),
        ["consentPath"] = ConsentPath,
        ["rule"] = "Enabling remote desktop/UI automation requires a local file whose exact text is ALLOW_DESKTOP_AUTOMATION."
    });

    private static string InteractionPolicySet(string body)
    {
        if (!McpTopLevelJson.TryGetBoolean(body, "enabled", out var enabled)) throw new InvalidOperationException("enabled boolean is required.");
        if (enabled && !LocalConsentPresent()) throw new InvalidOperationException("Local desktop consent is not present; policy cannot be enabled remotely.");
        _interactionEnabled = enabled;
        McpDiagnosticHub.Log("autocad_interaction_policy_set", "enabled=" + enabled.ToString(CultureInfo.InvariantCulture));
        return InteractionPolicyGet();
    }

    private static bool LocalConsentPresent()
    {
        try
        {
            if (!File.Exists(ConsentPath) || new FileInfo(ConsentPath).Length > 128) return false;
            return string.Equals(File.ReadAllText(ConsentPath).Trim(), "ALLOW_DESKTOP_AUTOMATION", StringComparison.Ordinal);
        }
        catch { return false; }
    }

    private static void RequireLocalConsent(string tool)
    {
        if (!_interactionEnabled || !LocalConsentPresent())
            throw new InvalidOperationException(tool + " requires local QS3D desktop consent and an enabled interaction policy.");
    }

    private static string AutoCadUiTextSnapshot() => McpDiagnosticHub.InvokeInCadContext(() =>
    {
        var document = AcApplication.DocumentManager.MdiActiveDocument;
        var commands = Qs3dCommandCatalog.All.Take(100).Select(item => new Dictionary<string, object?>
        {
            ["command"] = item.Command, ["label"] = UiText.Get(item.LabelKey), ["section"] = item.Section
        }).ToList();
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["host"] = "AutoCAD", ["document"] = document?.Name, ["qs3dCommands"] = commands, ["count"] = commands.Count
        });
    });

    private static string AutoCadUiInvoke(string body) => McpQs3dDomainRuntime.Call("qs3d_run_command", body);

    private static string AutoCadUiSetText(string body)
    {
        var target = McpTopLevelJson.ExtractString(body, "target");
        var text = McpTopLevelJson.ExtractString(body, "text");
        if (!string.Equals(target, "commandLine", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only target=commandLine is supported; arbitrary UI control text mutation fails closed.");
        if (text.Length == 0 || text.Length > MaxTypedCharacters || text.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
            throw new InvalidOperationException("commandLine text must be one bounded literal line.");
        return McpDiagnosticHub.InvokeInCadContext(() =>
        {
            var document = AcApplication.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active AutoCAD document.");
            document.SendStringToExecute(text, true, false, false);
            return McpJson.Serialize(new Dictionary<string, object?> { ["accepted"] = true, ["target"] = "commandLine", ["characterCount"] = text.Length });
        });
    }

    private static string CursorPosition()
    {
        if (!GetCursorPos(out var point)) throw new InvalidOperationException("GetCursorPos failed.");
        return McpJson.Serialize(new Dictionary<string, object?> { ["x"] = point.X, ["y"] = point.Y });
    }

    private static string WindowList(int limit)
    {
        var windows = EnumerateWindows(limit);
        return McpJson.Serialize(new Dictionary<string, object?> { ["count"] = windows.Count, ["windows"] = windows });
    }

    private static string ForegroundWindow()
    {
        var handle = GetForegroundWindow();
        return McpJson.Serialize(new Dictionary<string, object?> { ["window"] = handle == IntPtr.Zero ? null : DescribeWindow(handle) });
    }

    private static string WaitForWindow(string body)
    {
        var title = McpTopLevelJson.ExtractString(body, "titleContains");
        if (title.Length > 160) throw new InvalidOperationException("titleContains exceeds 160 characters.");
        var timeout = McpTopLevelJson.OptionalInt(body, "timeoutMs", 3000, 0, 7000);
        var started = Environment.TickCount;
        while (unchecked(Environment.TickCount - started) <= timeout)
        {
            var found = EnumerateWindows(MaxWindows).FirstOrDefault(window =>
                string.IsNullOrWhiteSpace(title) || Convert.ToString(window["title"], CultureInfo.InvariantCulture)?.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0);
            if (found is not null) return McpJson.Serialize(new Dictionary<string, object?> { ["found"] = true, ["window"] = found });
            Thread.Sleep(100);
        }
        return McpJson.Serialize(new Dictionary<string, object?> { ["found"] = false, ["timedOut"] = true });
    }

    private static string WindowFocus(string body)
    {
        var handle = RequireWindow(body);
        ShowWindow(handle, SwRestore);
        if (!SetForegroundWindow(handle)) throw new InvalidOperationException("SetForegroundWindow failed.");
        return McpJson.Serialize(new Dictionary<string, object?> { ["focused"] = true, ["window"] = DescribeWindow(handle) });
    }

    private static string WindowSetState(string body)
    {
        var handle = RequireWindow(body);
        var state = McpTopLevelJson.ExtractString(body, "state");
        var command = state.ToLowerInvariant() switch { "restore" => SwRestore, "minimize" => SwMinimize, "maximize" => SwMaximize, _ => throw new InvalidOperationException("state must be restore, minimize or maximize.") };
        ShowWindow(handle, command);
        return McpJson.Serialize(new Dictionary<string, object?> { ["updated"] = true, ["state"] = state, ["windowHandle"] = HandleText(handle) });
    }

    private static string WindowMoveResize(string body)
    {
        var handle = RequireWindow(body);
        var map = McpTopLevelJson.ParseObject(body);
        var x = Int(map, "x", -100000, 100000);
        var y = Int(map, "y", -100000, 100000);
        var width = Int(map, "width", 1, 20000);
        var height = Int(map, "height", 1, 20000);
        if (!MoveWindow(handle, x, y, width, height, true)) throw new InvalidOperationException("MoveWindow failed.");
        return McpJson.Serialize(new Dictionary<string, object?> { ["updated"] = true, ["window"] = DescribeWindow(handle) });
    }

    private static string MouseMove(string body)
    {
        var map = McpTopLevelJson.ParseObject(body);
        var x = Int(map, "x", -100000, 100000);
        var y = Int(map, "y", -100000, 100000);
        if (!SetCursorPos(x, y)) throw new InvalidOperationException("SetCursorPos failed.");
        return CursorPosition();
    }

    private static string MouseClick(string body)
    {
        var button = McpTopLevelJson.ExtractString(body, "button");
        if (string.IsNullOrWhiteSpace(button)) button = "left";
        var count = McpTopLevelJson.OptionalInt(body, "count", 1, 1, 3);
        var flags = button.ToLowerInvariant() switch
        {
            "left" => (MouseLeftDown, MouseLeftUp), "right" => (MouseRightDown, MouseRightUp), "middle" => (MouseMiddleDown, MouseMiddleUp),
            _ => throw new InvalidOperationException("button must be left, right or middle.")
        };
        for (var index = 0; index < count; index++) { mouse_event(flags.Item1, 0, 0, 0, UIntPtr.Zero); mouse_event(flags.Item2, 0, 0, 0, UIntPtr.Zero); }
        return McpJson.Serialize(new Dictionary<string, object?> { ["clicked"] = true, ["button"] = button, ["count"] = count });
    }

    private static string MouseScroll(string body)
    {
        var map = McpTopLevelJson.ParseObject(body);
        var delta = Int(map, "delta", -2400, 2400);
        mouse_event(MouseWheel, 0, 0, unchecked((uint)delta), UIntPtr.Zero);
        return McpJson.Serialize(new Dictionary<string, object?> { ["scrolled"] = true, ["delta"] = delta });
    }

    private static string MouseDrag(string body)
    {
        var map = McpTopLevelJson.ParseObject(body);
        var x1 = Int(map, "x1", -100000, 100000); var y1 = Int(map, "y1", -100000, 100000);
        var x2 = Int(map, "x2", -100000, 100000); var y2 = Int(map, "y2", -100000, 100000);
        if (!SetCursorPos(x1, y1)) throw new InvalidOperationException("SetCursorPos failed.");
        mouse_event(MouseLeftDown, 0, 0, 0, UIntPtr.Zero);
        try
        {
            for (var step = 1; step <= 10; step++) { SetCursorPos(x1 + (x2 - x1) * step / 10, y1 + (y2 - y1) * step / 10); Thread.Sleep(15); }
        }
        finally { mouse_event(MouseLeftUp, 0, 0, 0, UIntPtr.Zero); }
        return McpJson.Serialize(new Dictionary<string, object?> { ["dragged"] = true, ["x"] = x2, ["y"] = y2 });
    }

    private static string TypeText(string body)
    {
        var text = McpTopLevelJson.ExtractString(body, "text");
        if (text.Length > MaxTypedCharacters) throw new InvalidOperationException("text exceeds 8000 characters.");
        foreach (var ch in text) SendUnicode(ch);
        return McpJson.Serialize(new Dictionary<string, object?> { ["typed"] = true, ["characterCount"] = text.Length });
    }

    private static string PressKey(string body)
    {
        var key = McpTopLevelJson.ExtractString(body, "key").Trim().ToUpperInvariant();
        var vk = key switch
        {
            "ENTER" => (ushort)0x0D, "ESC" or "ESCAPE" => (ushort)0x1B, "TAB" => (ushort)0x09, "BACKSPACE" => (ushort)0x08,
            "DELETE" => (ushort)0x2E, "SPACE" => (ushort)0x20, "LEFT" => (ushort)0x25, "UP" => (ushort)0x26, "RIGHT" => (ushort)0x27, "DOWN" => (ushort)0x28,
            "F1" => (ushort)0x70, "F2" => (ushort)0x71, "F3" => (ushort)0x72, "F4" => (ushort)0x73, "F5" => (ushort)0x74,
            _ when key.Length == 1 && char.IsLetterOrDigit(key[0]) => (ushort)key[0],
            _ => throw new InvalidOperationException("Unsupported bounded desktop key: " + key)
        };
        SendVirtualKey(vk);
        return McpJson.Serialize(new Dictionary<string, object?> { ["pressed"] = true, ["key"] = key });
    }

    private static string ClipboardRead()
    {
        var text = RunSta(() => System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : string.Empty);
        if (text.Length > MaxClipboardCharacters) text = text.Substring(0, MaxClipboardCharacters);
        return McpJson.Serialize(new Dictionary<string, object?> { ["text"] = text, ["characterCount"] = text.Length, ["truncated"] = text.Length >= MaxClipboardCharacters });
    }

    private static string ClipboardWrite(string body)
    {
        var text = McpTopLevelJson.ExtractString(body, "text");
        if (text.Length > MaxClipboardCharacters) throw new InvalidOperationException("clipboard text exceeds 65536 characters.");
        RunSta(() => { System.Windows.Clipboard.SetText(text); return true; });
        return McpJson.Serialize(new Dictionary<string, object?> { ["written"] = true, ["characterCount"] = text.Length });
    }

    private static string Screenshot()
    {
        var x = GetSystemMetrics(SmXVirtualScreen); var y = GetSystemMetrics(SmYVirtualScreen);
        var width = GetSystemMetrics(SmCxVirtualScreen); var height = GetSystemMetrics(SmCyVirtualScreen);
        if (width <= 0 || height <= 0) throw new InvalidOperationException("Virtual desktop dimensions are unavailable.");
        var scale = Math.Min(1.0, Math.Min(1280.0 / width, 900.0 / height));
        using var source = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(source)) graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        using var output = scale < 1 ? new Bitmap(source, new Size(Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)))) : new Bitmap(source);
        using var memory = new MemoryStream();
        output.Save(memory, ImageFormat.Png);
        if (memory.Length > 3L * 1024 * 1024) throw new InvalidOperationException("Screenshot exceeds the bounded 3 MiB payload limit.");
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["mimeType"] = "image/png", ["width"] = output.Width, ["height"] = output.Height, ["base64"] = Convert.ToBase64String(memory.ToArray())
        });
    }

    private static string Sequence(string body)
    {
        var steps = McpTopLevelJson.ExtractArray(body, "steps") ?? throw new InvalidOperationException("steps array is required.");
        if (steps.Count == 0 || steps.Count > MaxSequenceSteps) throw new InvalidOperationException("desktop_sequence requires 1-12 steps.");
        var results = new List<object?>();
        var started = Environment.TickCount;
        foreach (var raw in steps)
        {
            if (unchecked(Environment.TickCount - started) > 7000) throw new TimeoutException("desktop_sequence exceeded 7 seconds.");
            var step = raw as Dictionary<string, object?> ?? throw new InvalidOperationException("Each desktop_sequence step must be an object.");
            var tool = step.TryGetValue("tool", out var toolValue) ? toolValue as string : null;
            var arguments = step.TryGetValue("arguments", out var argsValue) && argsValue is Dictionary<string, object?> args ? args : new Dictionary<string, object?>();
            if (tool is null || tool is "desktop_sequence" or "desktop_clipboard_read" or "desktop_ui_tree") throw new InvalidOperationException("Sequence step tool is not allowed.");
            if (!LocalConsentTools.Contains(tool) && tool != "desktop_wait_for_window") throw new InvalidOperationException("Sequence step tool is not in the bounded allow-list: " + tool);
            var result = Call(tool, McpJson.Serialize(arguments));
            results.Add(McpJson.Parse(result));
        }
        return McpJson.Serialize(new Dictionary<string, object?> { ["completed"] = true, ["stepCount"] = results.Count, ["results"] = results });
    }

    private static List<Dictionary<string, object?>> EnumerateWindows(int limit)
    {
        var result = new List<Dictionary<string, object?>>();
        EnumWindows((handle, _) =>
        {
            if (result.Count >= limit) return false;
            if (IsWindowVisible(handle) && IsCurrentSessionWindow(handle))
            {
                var item = DescribeWindow(handle);
                if (!string.IsNullOrWhiteSpace(Convert.ToString(item["title"], CultureInfo.InvariantCulture))) result.Add(item);
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static Dictionary<string, object?> DescribeWindow(IntPtr handle)
    {
        var length = Math.Min(512, GetWindowTextLength(handle));
        var title = new StringBuilder(length + 1);
        GetWindowText(handle, title, title.Capacity);
        GetWindowThreadProcessId(handle, out var pid);
        GetWindowRect(handle, out var rect);
        return new Dictionary<string, object?>
        {
            ["windowHandle"] = HandleText(handle), ["title"] = title.ToString(), ["processId"] = (long)pid,
            ["bounds"] = new Dictionary<string, object?> { ["x"] = rect.Left, ["y"] = rect.Top, ["width"] = rect.Right - rect.Left, ["height"] = rect.Bottom - rect.Top }
        };
    }

    private static bool IsCurrentSessionWindow(IntPtr handle)
    {
        GetWindowThreadProcessId(handle, out var pid);
        if (pid == 0) return false;
        try { using var process = Process.GetProcessById(checked((int)pid)); return process.SessionId == Process.GetCurrentProcess().SessionId; }
        catch { return false; }
    }

    private static IntPtr RequireWindow(string body)
    {
        var token = McpTopLevelJson.ExtractString(body, "windowHandle").Trim();
        if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) token = token.Substring(2);
        if (!long.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)) throw new InvalidOperationException("windowHandle must be hexadecimal.");
        var handle = new IntPtr(value);
        if (handle == IntPtr.Zero || !IsWindow(handle) || !IsCurrentSessionWindow(handle)) throw new InvalidOperationException("windowHandle is not a live current-session window.");
        return handle;
    }

    private static int Int(Dictionary<string, object?> map, string name, int minimum, int maximum)
    {
        if (!map.TryGetValue(name, out var raw)) throw new InvalidOperationException(name + " is required.");
        var number = raw switch { long value => value, double value when Math.Truncate(value) == value => checked((long)value), int value => value, _ => throw new InvalidOperationException(name + " must be an integer.") };
        if (number < minimum || number > maximum) throw new InvalidOperationException(name + " is outside the bounded range.");
        return checked((int)number);
    }

    private static string HandleText(IntPtr handle) => "0x" + handle.ToInt64().ToString("X", CultureInfo.InvariantCulture);

    private static T RunSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? error = null;
        using var completed = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try { result = action(); } catch (Exception ex) { error = ex; } finally { completed.Set(); }
        }) { IsBackground = true, Name = "QS3D-MCP-STA" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!completed.Wait(5000)) throw new TimeoutException("STA desktop operation timed out.");
        if (error is not null) throw new InvalidOperationException("Desktop STA operation failed: " + error.Message, error);
        return result!;
    }

    private static void SendUnicode(char ch)
    {
        var inputs = new[]
        {
            new Input { Type = InputKeyboard, Union = new InputUnion { Keyboard = new KeyboardInput { Scan = ch, Flags = KeyEventUnicode } } },
            new Input { Type = InputKeyboard, Union = new InputUnion { Keyboard = new KeyboardInput { Scan = ch, Flags = KeyEventUnicode | KeyEventKeyUp } } }
        };
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length) throw new InvalidOperationException("SendInput Unicode typing failed.");
    }

    private static void SendVirtualKey(ushort virtualKey)
    {
        var inputs = new[]
        {
            new Input { Type = InputKeyboard, Union = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = virtualKey } } },
            new Input { Type = InputKeyboard, Union = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = virtualKey, Flags = KeyEventKeyUp } } }
        };
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length) throw new InvalidOperationException("SendInput key press failed.");
    }

    [StructLayout(LayoutKind.Sequential)] private struct PointNative { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RectNative { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Union; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey; public ushort Scan; public uint Flags; public uint Time; public IntPtr ExtraInfo; }
    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle, StringBuilder text, int maxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr handle);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out RectNative rect);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr handle, int command);
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool repaint);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out PointNative point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
}