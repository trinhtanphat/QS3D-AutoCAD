namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal enum McpExecutionMode
{
    Normal,
    ReadOnly
}

internal static class McpToolCapabilityContract
{
    internal static McpExecutionMode ResolveExecutionMode(string? primary, string? alternate)
    {
        var value = string.IsNullOrWhiteSpace(primary) ? alternate : primary;
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "normal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "default", StringComparison.OrdinalIgnoreCase))
            return McpExecutionMode.Normal;
        if (string.Equals(value, "readOnly", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "read_only", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "readonly", StringComparison.OrdinalIgnoreCase))
            return McpExecutionMode.ReadOnly;
        throw new InvalidOperationException("Unsupported execution mode: " + value + ".");
    }

    internal static void EnsureAllowed(string tool, McpExecutionMode executionMode, bool requiresMutation)
    {
        if (string.IsNullOrWhiteSpace(tool)) throw new InvalidOperationException("MCP tool name is required.");
        if (requiresMutation && executionMode == McpExecutionMode.ReadOnly)
            throw new InvalidOperationException("Tool '" + tool + "' is a mutation and is blocked in read-only execution mode.");
    }
}