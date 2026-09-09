namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal sealed record McpToolDescriptor(string Name, string Description, string InputSchemaJson, bool RequiresMutation);

internal static class McpToolRegistry
{
    private const string EmptySchema = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}";
    private const string Confirm = "\"confirmMutation\":{\"type\":\"boolean\",\"const\":true},\"actionId\":{\"type\":\"string\",\"maxLength\":128},\"writerToken\":{\"type\":\"string\",\"maxLength\":128},\"executionMode\":{\"type\":\"string\",\"enum\":[\"normal\",\"readOnly\"]}";

    private static readonly IReadOnlyList<McpToolDescriptor> Tools = Build();
    private static readonly Dictionary<string, McpToolDescriptor> ByName = Tools.ToDictionary(item => item.Name, StringComparer.Ordinal);

    internal static IEnumerable<McpToolDescriptor> ToolDescriptors() => Tools;

    internal static McpToolDescriptor? Find(string? name) =>
        name is not null && ByName.TryGetValue(name, out var descriptor) ? descriptor : null;

    internal static bool RequiresMutation(string? name) => Find(name)?.RequiresMutation == true;

    private static IReadOnlyList<McpToolDescriptor> Build()
    {
        var list = new List<McpToolDescriptor>
        {
            Read("mcp_status", "Read embedded MCP server, capability and transport status."),
            Read("autocad_status", "Read bounded AutoCAD host/document status."),
            Read("qs3d_status", "Read QS3D domain/runtime status (compatibility alias)."),
            Read("qs3d_domain_status", "Read QS3D command/domain availability."),
            Read("cad_active_document", "Read active DWG identity and saved/modified state."),
            Read("cad_mutation_status", "Read mutation acknowledgement/replay status by actionId.", Object("\"actionId\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":128}" , "\"actionId\"")),
            Read("cad_selection", "Read exact handles/types for the implied AutoCAD selection."),
            Read("cad_database_snapshot", "Read a bounded current-space entity snapshot.", Object("\"limit\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":1000}")),
            Read("cad_entity_inspect", "Inspect one live entity by hexadecimal handle.", Object("\"handle\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":32}", "\"handle\"")),
            Read("cad_view_state", "Read the active AutoCAD view state."),
            Read("cad_wait_idle", "Wait for CMDACTIVE=0 within a bounded timeout.", Object("\"timeoutMs\":{\"type\":\"integer\",\"minimum\":100,\"maximum\":7000}")),
            Read("cad_sysvar", "Read one allow-listed non-sensitive AutoCAD system variable.", Object("\"name\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":64}", "\"name\"")),

            Mutate("cad_create_line", "Create a native AutoCAD Line.", Coordinates("x1","y1","z1","x2","y2","z2"), "\"x1\",\"y1\",\"x2\",\"y2\",\"confirmMutation\""),
            Mutate("cad_create_circle", "Create a native AutoCAD Circle.", Coordinates("x","y","z","radius"), "\"x\",\"y\",\"radius\",\"confirmMutation\""),
            Mutate("cad_create_arc", "Create a native AutoCAD Arc from center/radius/angles in radians.", Coordinates("x","y","z","radius","startAngle","endAngle"), "\"x\",\"y\",\"radius\",\"startAngle\",\"endAngle\",\"confirmMutation\""),
            Mutate("cad_create_polyline", "Create a native 2D AutoCAD Polyline from bounded points.", "\"points\":{\"type\":\"array\",\"minItems\":2,\"maxItems\":256,\"items\":{\"type\":\"object\",\"properties\":{\"x\":{\"type\":\"number\"},\"y\":{\"type\":\"number\"}},\"required\":[\"x\",\"y\"],\"additionalProperties\":false}},\"closed\":{\"type\":\"boolean\"}", "\"points\",\"confirmMutation\""),
            Mutate("cad_create_text", "Create native DBText.", Coordinates("x","y","z","height") + ",\"text\":{\"type\":\"string\",\"maxLength\":8000}", "\"x\",\"y\",\"text\",\"confirmMutation\""),
            Mutate("cad_create_mtext", "Create native MText.", Coordinates("x","y","z","height","width") + ",\"text\":{\"type\":\"string\",\"maxLength\":32000}", "\"x\",\"y\",\"text\",\"confirmMutation\""),
            Mutate("cad_entity_transform", "Translate/rotate/scale one entity by handle.", "\"handle\":{\"type\":\"string\",\"maxLength\":32},\"dx\":{\"type\":\"number\"},\"dy\":{\"type\":\"number\"},\"dz\":{\"type\":\"number\"},\"rotationRadians\":{\"type\":\"number\"},\"scale\":{\"type\":\"number\",\"exclusiveMinimum\":0}", "\"handle\",\"confirmMutation\""),
            Mutate("cad_entity_delete", "Erase one live entity by exact handle.", "\"handle\":{\"type\":\"string\",\"maxLength\":32}", "\"handle\",\"confirmMutation\""),
            Mutate("cad_entity_set_layer", "Move one entity to an existing or newly-created layer.", "\"handle\":{\"type\":\"string\",\"maxLength\":32},\"layer\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":255}", "\"handle\",\"layer\",\"confirmMutation\""),
            Mutate("cad_layer", "Create a layer or set the current layer; action=create|setCurrent.", "\"action\":{\"type\":\"string\",\"enum\":[\"create\",\"setCurrent\"]},\"name\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":255},\"colorIndex\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":255}", "\"action\",\"name\",\"confirmMutation\""),
            Read("cad_command_catalog", "Read the bounded AutoCAD command allow-list usable by cad_command_sequence."),
            Mutate("cad_command_sequence", "Queue one allow-listed AutoCAD command with bounded literal input text.", "\"command\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":64},\"inputs\":{\"type\":\"string\",\"maxLength\":4000}", "\"command\",\"confirmMutation\""),
            Mutate("qs3d_run_command", "Queue one command from the existing QS3D AutoCAD command catalog.", "\"command\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":80}", "\"command\",\"confirmMutation\""),

            Mutate("cad_create_box", "Create a native AutoCAD Solid3d box centered at x,y,z.", Coordinates("x","y","z","length","width","height"), "\"x\",\"y\",\"z\",\"length\",\"width\",\"height\",\"confirmMutation\""),
            Mutate("cad_extrude", "Extrude one closed planar Curve into a Solid3d.", "\"handle\":{\"type\":\"string\",\"maxLength\":32},\"height\":{\"type\":\"number\"}", "\"handle\",\"height\",\"confirmMutation\""),
            Mutate("cad_boolean_union", "Boolean-union two Solid3d entities; tool solid is consumed.", BooleanProperties, BooleanRequired),
            Mutate("cad_boolean_subtract", "Boolean-subtract tool Solid3d from target; tool solid is consumed.", BooleanProperties, BooleanRequired),
            Mutate("cad_boolean_intersect", "Boolean-intersect two Solid3d entities; tool solid is consumed.", BooleanProperties, BooleanRequired),
            Mutate("cad_save", "Synchronously save the active rooted DWG using native Database.SaveAs.", string.Empty, "\"confirmMutation\""),
            Mutate("cad_save_as", "Save active drawing to an absolute .dwg path with overwrite checks.", "\"path\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":1024},\"overwrite\":{\"type\":\"boolean\"}", "\"path\",\"confirmMutation\""),

            Read("cad_layer_state", "Read ON/OFF, frozen and locked state for one layer.", Object("\"name\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":255}", "\"name\"")),
            Mutate("cad_layer_set_state", "Atomically set ON/OFF, frozen and/or locked state for one layer.", "\"name\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":255},\"on\":{\"type\":\"boolean\"},\"frozen\":{\"type\":\"boolean\"},\"locked\":{\"type\":\"boolean\"}", "\"name\",\"confirmMutation\""),
            Read("cad_layer_snapshot", "Capture an opaque bounded native layer-state snapshot."),
            Mutate("cad_layer_restore", "Restore an opaque snapshot from cad_layer_snapshot atomically.", "\"snapshot\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":524288}", "\"snapshot\",\"confirmMutation\""),
            Mutate("cad_view_zoom_extents", "Fit active AutoCAD view to drawing extents.", "\"padding\":{\"type\":\"number\",\"minimum\":1,\"maximum\":2}", "\"confirmMutation\""),
            Mutate("cad_view_fit_entities", "Fit active view to exact entity handles.", "\"handlesCsv\":{\"type\":\"string\",\"maxLength\":1800},\"padding\":{\"type\":\"number\",\"minimum\":1,\"maximum\":2}", "\"handlesCsv\",\"confirmMutation\""),
            Mutate("cad_view_set", "Set bounded active view center/size without modal UI.", Coordinates("centerX","centerY","width","height","twistRadians"), "\"centerX\",\"centerY\",\"width\",\"height\",\"confirmMutation\""),
            Read("agent_status", "Read bounded MCP agent stop/error/action state."),
            Read("cad_command_state", "Read CMDACTIVE/CMDNAMES without command-line history."),

            Read("diagnostics_log_tail", "Read bounded MCP diagnostics.", Object("\"limit\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":100}")),
            Read("diagnostics_since", "Read bounded diagnostics since an ISO UTC timestamp.", Object("\"sinceUtc\":{\"type\":\"string\",\"maxLength\":64},\"limit\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":100}")),
            Read("diagnostics_snapshot", "Read a bounded MCP/AutoCAD diagnostics snapshot."),
            Read("diagnostics_wait", "Wait briefly for a new diagnostic event.", Object("\"timeoutMs\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":7000}")),
            Read("theme_get", "Read QS3D UI theme mode and resolved AutoCAD theme."),
            Mutate("theme_set", "Set QS3D UI theme mode: System, Light or Dark.", "\"mode\":{\"type\":\"string\",\"enum\":[\"System\",\"Light\",\"Dark\"]}", "\"mode\",\"confirmMutation\""),
            Read("autocad_interaction_policy_get", "Read local desktop/UI consent policy."),
            Mutate("autocad_interaction_policy_set", "Set desktop/UI policy within the locally-authorized consent ceiling.", "\"enabled\":{\"type\":\"boolean\"}", "\"enabled\",\"confirmMutation\""),
            Read("autocad_ui_text_snapshot", "Read bounded QS3D/AutoCAD UI text metadata without screen OCR."),
            Mutate("autocad_ui_invoke", "Invoke one existing QS3D command by catalog name.", "\"command\":{\"type\":\"string\",\"maxLength\":80}", "\"command\",\"confirmMutation\""),
            Mutate("autocad_ui_set_text", "Set text only on a supported QS3D UI target; unsupported targets fail closed.", "\"target\":{\"type\":\"string\",\"maxLength\":80},\"text\":{\"type\":\"string\",\"maxLength\":8000}", "\"target\",\"text\",\"confirmMutation\""),

            Read("desktop_cursor_position", "Read current Windows desktop cursor position."),
            Read("desktop_window_list", "List visible top-level windows in the current interactive session.", Object("\"limit\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":100}")),
            Read("desktop_foreground_window", "Read current foreground-window metadata."),
            Read("desktop_wait_for_window", "Wait for a visible window matching title text.", Object("\"titleContains\":{\"type\":\"string\",\"maxLength\":160},\"timeoutMs\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":7000}")),
            Mutate("desktop_window_focus", "Restore/focus one current-session window; requires local consent.", "\"windowHandle\":{\"type\":\"string\",\"maxLength\":32}", "\"windowHandle\",\"confirmMutation\""),
            Mutate("desktop_window_set_state", "Set one current-session window to restore/minimize/maximize; requires local consent.", "\"windowHandle\":{\"type\":\"string\",\"maxLength\":32},\"state\":{\"type\":\"string\",\"enum\":[\"restore\",\"minimize\",\"maximize\"]}", "\"windowHandle\",\"state\",\"confirmMutation\""),
            Mutate("desktop_window_move_resize", "Move/resize one current-session top-level window; requires local consent.", "\"windowHandle\":{\"type\":\"string\",\"maxLength\":32},\"x\":{\"type\":\"integer\"},\"y\":{\"type\":\"integer\"},\"width\":{\"type\":\"integer\",\"minimum\":1},\"height\":{\"type\":\"integer\",\"minimum\":1}", "\"windowHandle\",\"x\",\"y\",\"width\",\"height\",\"confirmMutation\""),
            Read("desktop_ui_tree", "Read a privacy-bounded top-level desktop UI tree; requires local sensitive-read consent."),
            Mutate("desktop_mouse_move", "Move cursor to absolute virtual-desktop coordinates; requires local consent.", Coordinates("x","y"), "\"x\",\"y\",\"confirmMutation\""),
            Mutate("desktop_mouse_click", "Click a mouse button at the current cursor; requires local consent.", "\"button\":{\"type\":\"string\",\"enum\":[\"left\",\"right\",\"middle\"]},\"count\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":3}", "\"confirmMutation\""),
            Mutate("desktop_mouse_scroll", "Scroll mouse wheel by bounded delta; requires local consent.", "\"delta\":{\"type\":\"integer\",\"minimum\":-2400,\"maximum\":2400}", "\"delta\",\"confirmMutation\""),
            Mutate("desktop_mouse_drag", "Drag cursor from one absolute point to another; requires local consent.", Coordinates("x1","y1","x2","y2"), "\"x1\",\"y1\",\"x2\",\"y2\",\"confirmMutation\""),
            Mutate("desktop_type", "Type bounded Unicode text into foreground window; requires local consent.", "\"text\":{\"type\":\"string\",\"maxLength\":8000}", "\"text\",\"confirmMutation\""),
            Mutate("desktop_key", "Press one bounded named key; requires local consent.", "\"key\":{\"type\":\"string\",\"maxLength\":32}", "\"key\",\"confirmMutation\""),
            Read("desktop_clipboard_read", "Read bounded text clipboard; requires local sensitive-read consent."),
            Mutate("desktop_clipboard_write", "Write bounded text clipboard; requires local consent.", "\"text\":{\"type\":\"string\",\"maxLength\":65536}", "\"text\",\"confirmMutation\""),
            Read("desktop_screenshot", "Capture one bounded virtual-desktop PNG; requires local sensitive-read consent."),
            Mutate("desktop_sequence", "Run up to 12 bounded desktop steps against the current session; requires local consent.", "\"steps\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":12,\"items\":{\"type\":\"object\"}}", "\"steps\",\"confirmMutation\""),

            Mutate("cad_ui_click", "Compatibility alias for bounded desktop_mouse_click.", "\"button\":{\"type\":\"string\"}", "\"confirmMutation\""),
            Mutate("cad_ui_type", "Compatibility alias for bounded desktop_type.", "\"text\":{\"type\":\"string\",\"maxLength\":8000}", "\"text\",\"confirmMutation\""),
            Mutate("cad_ui_key", "Compatibility alias for bounded desktop_key.", "\"key\":{\"type\":\"string\",\"maxLength\":32}", "\"key\",\"confirmMutation\""),
            Mutate("cad_agent_stop", "Emergency-stop further MCP automation mutations.", string.Empty, "\"confirmMutation\""),
            Mutate("cad_agent_resume", "Resume MCP mutations after emergency stop.", string.Empty, "\"confirmMutation\""),
            Read("cad_audit_tail", "Read bounded MCP mutation audit evidence.", Object("\"limit\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":100}")),
            Mutate("cad_cancel_command", "Queue bounded ESC cancellation for the active AutoCAD command.", string.Empty, "\"confirmMutation\"")
        };
        return list;
    }

    private const string BooleanProperties = "\"targetHandle\":{\"type\":\"string\",\"maxLength\":32},\"toolHandle\":{\"type\":\"string\",\"maxLength\":32}";
    private const string BooleanRequired = "\"targetHandle\",\"toolHandle\",\"confirmMutation\"";

    private static McpToolDescriptor Read(string name, string description, string? schema = null) =>
        new(name, description, schema ?? EmptySchema, false);

    private static McpToolDescriptor Mutate(string name, string description, string properties, string required) =>
        new(name, description, MutationObject(properties, required), true);

    private static string Object(string properties, string required = "") =>
        "{\"type\":\"object\",\"properties\":{" + properties + "},"
        + (string.IsNullOrWhiteSpace(required) ? string.Empty : "\"required\":[" + required + "],")
        + "\"additionalProperties\":false}";

    private static string MutationObject(string properties, string required) =>
        Object((string.IsNullOrWhiteSpace(properties) ? string.Empty : properties + ",") + Confirm, required);

    private static string Coordinates(params string[] names) => string.Join(",", names.Select(name => "\"" + name + "\":{\"type\":\"number\"}"));
}