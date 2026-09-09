namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Explicit-local-action tunnel intent. QS3D never auto-starts or purchases a tunnel provider.</summary>
internal static class McpSecureTunnelRuntime
{
    private static readonly object Sync = new();
    private static string _state = "disabled";
    private static string _endpoint = string.Empty;
    private const string ActivationMarker = "explicitLocalAction";

    internal static string RequestStartFromLocalUser(string endpointText)
    {
        if (!McpDesktopControlSession.IsEnabled)
            throw new InvalidOperationException("Secure tunnel activation requires an explicit local Agent Center consent action.");
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint)
            || !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(endpoint.UserInfo)
            || McpPublicEndpointResolver.IsLoopback(endpoint))
            throw new InvalidOperationException("Secure tunnel endpoint must be a credential-free public https URI.");
        lock (Sync)
        {
            _endpoint = endpoint.GetLeftPart(UriPartial.Path);
            _state = "manual-provider-start-required";
        }
        McpDiagnosticHub.Log("secure-tunnel", "explicit local start requested; provider credentials redacted; provider launch remains external");
        return SnapshotJson();
    }

    internal static string StopFromLocalUser()
    {
        lock (Sync) { _state = "disabled"; _endpoint = string.Empty; }
        McpDiagnosticHub.Log("secure-tunnel", "explicit local stop recorded");
        return SnapshotJson();
    }

    internal static string SnapshotJson()
    {
        lock (Sync)
        {
            return McpJson.Serialize(new Dictionary<string, object?>
            {
                ["state"] = _state,
                ["endpoint"] = _endpoint,
                ["activation"] = ActivationMarker,
                ["autoStart"] = false,
                ["providerProcessOwnedByPlugin"] = false,
                ["secretsRedacted"] = true
            });
        }
    }
}
