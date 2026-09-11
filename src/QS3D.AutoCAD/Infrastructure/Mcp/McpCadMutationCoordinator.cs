namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal static class McpCadMutationCoordinator
{
    private static readonly object MutationGate = new();
    private static string? _activeWriterToken;
    private static string? _activeTool;

    internal static bool IsMutationActive => Volatile.Read(ref _activeTool) is not null;

    internal static IDisposable EnterMutation(string? writerToken, string tool, Action<string>? audit = null)
    {
        var token = string.IsNullOrWhiteSpace(writerToken) ? "anonymous" : writerToken!.Trim();
        if (token.Length > 128) throw new InvalidOperationException("writerToken exceeds 128 characters.");
        if (!Monitor.TryEnter(MutationGate, 7000))
            throw new InvalidOperationException("Another MCP mutation writer is still active; retry later.");
        _activeWriterToken = token;
        _activeTool = tool;
        audit?.Invoke("writer-enter token=" + token + "; tool=" + tool);
        return new Releaser(token, tool, audit);
    }

    internal static void Reset()
    {
        _activeWriterToken = null;
        _activeTool = null;
    }

    internal static string StatusJson() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["active"] = _activeTool is not null,
        ["writerToken"] = _activeWriterToken,
        ["tool"] = _activeTool
    });

    private sealed class Releaser : IDisposable
    {
        private readonly string _token;
        private readonly string _tool;
        private readonly Action<string>? _audit;
        private bool _disposed;

        internal Releaser(string token, string tool, Action<string>? audit)
        {
            _token = token;
            _tool = tool;
            _audit = audit;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _audit?.Invoke("writer-exit token=" + _token + "; tool=" + _tool);
            _activeWriterToken = null;
            _activeTool = null;
            Monitor.Exit(MutationGate);
        }
    }
}
