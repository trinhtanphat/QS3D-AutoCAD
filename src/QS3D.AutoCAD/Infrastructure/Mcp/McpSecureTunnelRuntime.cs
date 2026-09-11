namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>
/// Explicit-local-action tunnel capability state. The provider process is managed outside
/// the AutoCAD plugin so remote MCP calls can never obtain a generic process/shell primitive.
/// </summary>
internal static class McpSecureTunnelRuntime
{
    private static readonly object Sync = new();
    private static bool _active;
    private static string _provider = string.Empty;
    private static string _publicMcpUrl = string.Empty;
    private const string ActivationContract = "explicitLocalAction";

    internal static string PublicMcpUrl
    {
        get { lock (Sync) return _active ? _publicMcpUrl : string.Empty; }
    }

    internal static void StartFromLocalUser(string provider, string publicMcpUrl)
    {
        var selected = McpTransportProfileRegistry.GetSelected();
        if (!selected.Public) throw new InvalidOperationException("Select an explicit public/tunnel profile before activation.");
        var normalized = McpPublicEndpointResolver.NormalizeCandidate(publicMcpUrl);
        if (normalized.Length == 0) throw new InvalidOperationException("Secure tunnel public endpoint must be non-loopback HTTPS /mcp without credentials.");
        var providerName = (provider ?? string.Empty).Trim();
        if (providerName.Length == 0 || providerName.Length > 64) throw new InvalidOperationException("Tunnel provider name is invalid.");
        lock (Sync)
        {
            _provider = providerName;
            _publicMcpUrl = normalized;
            _active = true;
        }
        McpDiagnosticHub.Log("mcp-tunnel", "activated from explicit local action; provider-managed process; endpoint=" + normalized);
    }

    internal static void StopFromLocalUser()
    {
        lock (Sync)
        {
            _active = false;
            _publicMcpUrl = string.Empty;
        }
        McpDiagnosticHub.Log("mcp-tunnel", "deactivated from explicit local action");
    }

    internal static void StopForHostShutdown()
    {
        lock (Sync)
        {
            _active = false;
            _publicMcpUrl = string.Empty;
        }
    }

    internal static string SnapshotJson()
    {
        lock (Sync)
        {
            return McpJson.Serialize(new Dictionary<string, object?>
            {
                ["active"] = _active,
                ["provider"] = _provider,
                ["publicMcpUrl"] = _active ? _publicMcpUrl : string.Empty,
                ["explicitLocalAction"] = ActivationContract,
                ["providerProcessManagedOutsidePlugin"] = true,
                ["remoteActivationAllowed"] = false,
                ["credentialsLogged"] = false
            });
        }
    }
}
