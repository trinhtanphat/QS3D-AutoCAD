namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Process-scoped local consent. Remote MCP calls cannot enable this state.</summary>
internal static class McpDesktopControlSession
{
    private static int _enabled;
    private static string _lastReason = "process-start-default";
    private const string ConsentContract = "requiresLocalConsent";

    internal static bool IsEnabled => Volatile.Read(ref _enabled) == 1;
    internal static string ConsentState => IsEnabled ? "local-consent-active" : "local-consent-required";

    internal static void EnableFromLocalUser(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Local consent reason is required.");
        _lastReason = Bound(reason, 96);
        Interlocked.Exchange(ref _enabled, 1);
        McpDiagnosticHub.Log("desktop-consent", "enabled locally; reason=" + _lastReason);
    }

    internal static void Disable(string reason)
    {
        _lastReason = Bound(reason, 96);
        Interlocked.Exchange(ref _enabled, 0);
        McpDiagnosticHub.Log("desktop-consent", "disabled; reason=" + _lastReason);
    }

    internal static void RequireLocalConsent(string operation)
    {
        if (!IsEnabled)
            throw new InvalidOperationException((operation ?? "desktop operation") + " requires local process-scoped desktop consent.");
    }

    internal static string SnapshotJson() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["enabled"] = IsEnabled,
        ["state"] = ConsentState,
        ["requiresLocalConsent"] = true,
        ["processScoped"] = true,
        ["resetsOnRestart"] = true,
        ["lastReason"] = _lastReason
    });

    private static string Bound(string value, int max)
    {
        value ??= string.Empty;
        return value.Substring(0, Math.Min(value.Length, max));
    }
}
