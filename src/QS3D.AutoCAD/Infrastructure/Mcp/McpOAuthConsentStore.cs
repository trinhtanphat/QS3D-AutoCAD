using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal sealed class McpOAuthGrant
{
    internal required string Hash { get; init; }
    internal required string ClientId { get; init; }
    internal required string RedirectUri { get; init; }
    internal required string[] Scopes { get; init; }
    internal required DateTime ExpiresUtc { get; init; }
}

/// <summary>Process-local deny-by-default consent store using short-lived one-time authorization codes.</summary>
internal static class McpOAuthConsentStore
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, McpOAuthGrant> Grants = new(StringComparer.Ordinal);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    internal static string IssueOneTimeCodeFromLocalUser(string clientId, string redirectUri, IEnumerable<string> scopes)
    {
        McpDesktopControlSession.RequireLocalConsent("oauth-consent");
        clientId = BoundRequired(clientId, "clientId", 128);
        var redirect = ValidateLoopbackRedirect(redirectUri);
        var scopeList = NormalizeScopes(scopes);
        var code = RandomToken(32);
        var hash = Hash(code);
        lock (Sync)
        {
            PruneLocked();
            Grants[hash] = new McpOAuthGrant
            {
                Hash = hash,
                ClientId = clientId,
                RedirectUri = redirect.AbsoluteUri,
                Scopes = scopeList,
                ExpiresUtc = DateTime.UtcNow.Add(Lifetime)
            };
        }
        McpDiagnosticHub.Log("oauth", "one-time local consent code issued; client=" + clientId + "; scopeCount=" + scopeList.Length);
        return code;
    }

    internal static string ConsumeOneTimeCode(string code, string clientId, string redirectUri)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 256) throw new InvalidOperationException("authorization code is invalid");
        clientId = BoundRequired(clientId, "clientId", 128);
        var redirect = ValidateLoopbackRedirect(redirectUri);
        var hash = Hash(code);
        McpOAuthGrant? grant;
        lock (Sync)
        {
            PruneLocked();
            if (!Grants.TryGetValue(hash, out grant)) throw new InvalidOperationException("authorization denied-by-default: code is absent, expired or already consumed");
            Grants.Remove(hash); // one-time consumption occurs before any token/result is returned.
        }
        if (!string.Equals(grant.ClientId, clientId, StringComparison.Ordinal)
            || !string.Equals(grant.RedirectUri, redirect.AbsoluteUri, StringComparison.Ordinal))
            throw new InvalidOperationException("authorization denied-by-default: client or loopback redirect mismatch");
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["authorized"] = true,
            ["clientId"] = clientId,
            ["scopes"] = grant.Scopes,
            ["consumed"] = true,
            ["oneTime"] = true
        });
    }

    internal static string SnapshotJson()
    {
        lock (Sync)
        {
            PruneLocked();
            return McpJson.Serialize(new Dictionary<string, object?>
            {
                ["authorization"] = "deny-by-default",
                ["pendingOneTimeCodes"] = Grants.Count,
                ["loopbackRedirectOnly"] = true,
                ["plaintextCodesStored"] = false,
                ["browserCookieAccess"] = false
            });
        }
    }

    internal static Uri ValidateLoopbackRedirect(string redirectUri)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)) throw new InvalidOperationException("redirect_uri must be absolute");
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("redirect_uri must use http/https loopback");
        var isLoopback = string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address));
        if (!isLoopback || !string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidOperationException("redirect_uri must be credential-free loopback");
        return uri;
    }

    private static string[] NormalizeScopes(IEnumerable<string> scopes)
    {
        var result = (scopes ?? Array.Empty<string>()).Select(item => (item ?? string.Empty).Trim())
            .Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).Take(16).ToArray();
        if (result.Length == 0) throw new InvalidOperationException("At least one bounded scope is required.");
        if (result.Any(item => item.Length > 80)) throw new InvalidOperationException("OAuth scope exceeds 80 characters.");
        return result;
    }

    private static string BoundRequired(string value, string name, int max)
    {
        value = (value ?? string.Empty).Trim();
        if (value.Length == 0 || value.Length > max) throw new InvalidOperationException(name + " is required and bounded.");
        return value;
    }

    private static string RandomToken(int bytes)
    {
        var buffer = new byte[bytes];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(buffer);
        return Convert.ToBase64String(buffer).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Hash(string value)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
    }

    private static void PruneLocked()
    {
        var now = DateTime.UtcNow;
        foreach (var key in Grants.Where(pair => pair.Value.ExpiresUtc <= now).Select(pair => pair.Key).ToArray()) Grants.Remove(key);
    }
}
