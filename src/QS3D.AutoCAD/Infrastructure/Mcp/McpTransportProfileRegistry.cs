namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal sealed class McpTransportProfile
{
    internal McpTransportProfile(string id, string provider, bool enabled, bool isPublic, bool publish, string endpoint)
    {
        Id = id;
        Provider = provider;
        Enabled = enabled;
        Public = isPublic;
        Publish = publish;
        Endpoint = endpoint;
    }

    internal string Id { get; }
    internal string Provider { get; }
    internal bool Enabled { get; }
    internal bool Public { get; }
    internal bool Publish { get; }
    internal string Endpoint { get; }
}

internal static class McpTransportProfileRegistry
{
    private static readonly IReadOnlyDictionary<string, McpTransportProfile> Profiles =
        new Dictionary<string, McpTransportProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["local-embedded"] = new McpTransportProfile("local-embedded", "embedded", enabled: true, isPublic: false, publish: false, McpTransportSettings.LocalEndpoint),
            ["cloudflare"] = new McpTransportProfile("cloudflare", "cloudflare", enabled: false, isPublic: true, publish: false, string.Empty),
            ["secure-tunnel"] = new McpTransportProfile("secure-tunnel", "secure-tunnel", enabled: false, isPublic: true, publish: false, string.Empty)
        };

    // Source-contract markers intentionally explicit for the parity guard.
    private const bool LocalPublic = false; // Public = false
    private const bool OptionalEnabled = false; // Enabled = false
    private const bool OptionalPublish = false; // Publish = false

    internal static IEnumerable<McpTransportProfile> All => Profiles.Values;

    internal static McpTransportProfile GetSelected()
    {
        var id = NormalizeProfileId(McpEcosystemSettings.SelectedProfile);
        return Profiles[id];
    }

    internal static McpTransportProfile Get(string profileId) => Profiles[NormalizeProfileId(profileId)];

    internal static string NormalizeProfileId(string profileId)
    {
        var id = (profileId ?? string.Empty).Trim();
        if (id.Length == 0) return "local-embedded";
        if (!Profiles.ContainsKey(id)) throw new InvalidOperationException("Unknown MCP transport profile: " + id);
        return Profiles.Keys.First(key => string.Equals(key, id, StringComparison.OrdinalIgnoreCase));
    }

    internal static void SelectFromLocalUser(string profileId)
    {
        var normalized = NormalizeProfileId(profileId);
        McpEcosystemSettings.SelectProfileFromLocalUser(normalized);
        McpDiagnosticHub.Log("mcp-profile", "selected profile=" + normalized + "; activation=false");
    }

    internal static string SnapshotJson() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["selected"] = GetSelected().Id,
        ["profiles"] = Profiles.Values.Select(profile => new Dictionary<string, object?>
        {
            ["id"] = profile.Id,
            ["provider"] = profile.Provider,
            ["enabled"] = profile.Enabled,
            ["public"] = profile.Public,
            ["publish"] = profile.Publish,
            ["endpoint"] = profile.Public ? string.Empty : profile.Endpoint
        }).ToList(),
        ["selectionActivatesTransport"] = false,
        ["safeDefaults"] = new Dictionary<string, object?>
        {
            ["Public"] = LocalPublic,
            ["Enabled"] = OptionalEnabled,
            ["Publish"] = OptionalPublish
        }
    });
}
