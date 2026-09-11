namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Process-scoped OAuth grants created only by explicit local-user action.</summary>
internal static class McpOAuthConsentStore
{
    private static readonly object Sync = new();
    private static readonly HashSet<string> Grants = new(StringComparer.Ordinal);
    private const int MaxGrants = 64;
    private const string DefaultPolicy = "deny-by-default";

    internal static void GrantFromLocalUser(string clientId, string redirectUri, string scope)
    {
        var key = BuildKey(clientId, redirectUri, scope);
        lock (Sync)
        {
            if (Grants.Count >= MaxGrants && !Grants.Contains(key))
                throw new InvalidOperationException("OAuth local-consent capacity reached; revoke an older grant first.");
            Grants.Add(key);
        }
        McpDiagnosticHub.Log("mcp-oauth-consent", "local consent granted; client=" + Bound(clientId, 48) + "; scope=" + scope);
    }

    internal static void RevokeFromLocalUser(string clientId, string redirectUri, string scope)
    {
        var key = BuildKey(clientId, redirectUri, scope);
        lock (Sync) Grants.Remove(key);
        McpDiagnosticHub.Log("mcp-oauth-consent", "local consent revoked; client=" + Bound(clientId, 48));
    }

    internal static void RevokeAll(string reason)
    {
        lock (Sync) Grants.Clear();
        McpDiagnosticHub.Log("mcp-oauth-consent", "all process-scoped consent revoked; reason=" + Bound(reason, 96));
    }

    internal static bool HasConsent(string clientId, string redirectUri, string scope)
    {
        string key;
        try { key = BuildKey(clientId, redirectUri, scope); }
        catch { return false; }
        lock (Sync) return Grants.Contains(key);
    }

    internal static string SnapshotJson()
    {
        lock (Sync)
        {
            return McpJson.Serialize(new Dictionary<string, object?>
            {
                ["policy"] = DefaultPolicy,
                ["grantCount"] = Grants.Count,
                ["processScoped"] = true,
                ["persistentBearerTokens"] = false,
                ["browserCookieAccess"] = false,
                ["requiresExplicitLocalConsent"] = true
            });
        }
    }

    private static string BuildKey(string clientId, string redirectUri, string scope)
    {
        var client = (clientId ?? string.Empty).Trim();
        if (client.Length == 0 || client.Length > 128) throw new InvalidOperationException("OAuth client id is invalid.");
        if (!McpOAuthAuthorizationServer.IsAllowedLoopbackRedirect(redirectUri))
            throw new InvalidOperationException("OAuth redirect must be an allow-listed loopback URI.");
        if (!string.Equals((scope ?? string.Empty).Trim(), McpOAuthAuthorizationServer.RequiredScope, StringComparison.Ordinal))
            throw new InvalidOperationException("OAuth scope is not supported.");
        return client + "\n" + redirectUri.Trim() + "\n" + scope.Trim();
    }

    private static string Bound(string? value, int max)
    {
        var text = value ?? string.Empty;
        return text.Length <= max ? text : text.Substring(0, max);
    }
}
