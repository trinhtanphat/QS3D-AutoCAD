using System.Security.Cryptography;
using System.Text;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal sealed class McpOAuthHttpResponse
{
    internal int StatusCode;
    internal string Reason = string.Empty;
    internal string Body = string.Empty;
    internal string ContentType = "application/json; charset=utf-8";
    internal readonly Dictionary<string, string> Headers = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Small deny-by-default OAuth/PKCE server with one-time codes and hashed in-memory tokens.</summary>
internal static class McpOAuthAuthorizationServer
{
    internal const string RequiredScope = "qs3d:mcp";
    internal static readonly TimeSpan AuthorizationCodeLifetime = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);
    private const int MaxEntries = 256;
    private static readonly object Sync = new();
    private static readonly Dictionary<string, AuthorizationCodeEntry> Codes = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, AccessTokenEntry> Tokens = new(StringComparer.Ordinal);

    private sealed class AuthorizationCodeEntry
    {
        internal string ClientId = string.Empty;
        internal string RedirectUri = string.Empty;
        internal string CodeChallenge = string.Empty;
        internal string Resource = string.Empty;
        internal long ExpiresUtc;
    }

    private sealed class AccessTokenEntry
    {
        internal string ClientId = string.Empty;
        internal string Resource = string.Empty;
        internal long ExpiresUtc;
    }

    internal static bool TryHandle(
        string method,
        string path,
        string query,
        IDictionary<string, string> headers,
        string body,
        out McpOAuthHttpResponse response)
    {
        response = null!;
        var publicMcpUrl = McpPublicEndpointResolver.Resolve();
        if (publicMcpUrl.Length == 0 || !Uri.TryCreate(publicMcpUrl, UriKind.Absolute, out var resourceUri)) return false;
        var issuer = resourceUri.GetLeftPart(UriPartial.Authority);

        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(path, "/.well-known/oauth-protected-resource", StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, "/.well-known/oauth-protected-resource/mcp", StringComparison.OrdinalIgnoreCase)))
        {
            response = Json(200, "OK", new Dictionary<string, object?>
            {
                ["resource"] = publicMcpUrl,
                ["authorization_servers"] = new[] { issuer },
                ["scopes_supported"] = new[] { RequiredScope }
            });
            return true;
        }

        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            && string.Equals(path, "/.well-known/oauth-authorization-server", StringComparison.OrdinalIgnoreCase))
        {
            response = Json(200, "OK", new Dictionary<string, object?>
            {
                ["issuer"] = issuer,
                ["authorization_endpoint"] = issuer + "/oauth/authorize",
                ["token_endpoint"] = issuer + "/oauth/token",
                ["response_types_supported"] = new[] { "code" },
                ["grant_types_supported"] = new[] { "authorization_code" },
                ["token_endpoint_auth_methods_supported"] = new[] { "none" },
                ["code_challenge_methods_supported"] = new[] { "S256" },
                ["scopes_supported"] = new[] { RequiredScope }
            });
            return true;
        }

        if (string.Equals(path, "/oauth/authorize", StringComparison.OrdinalIgnoreCase))
        {
            response = !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
                ? OAuthError(405, "Method Not Allowed", "invalid_request", "authorization requires GET")
                : Authorize(query, publicMcpUrl);
            return true;
        }

        if (string.Equals(path, "/oauth/token", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                response = OAuthError(405, "Method Not Allowed", "invalid_request", "token endpoint requires POST");
                return true;
            }
            if (!headers.TryGetValue("Content-Type", out var contentType)
                || !contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                response = OAuthError(415, "Unsupported Media Type", "invalid_request", "token endpoint requires form encoding");
                return true;
            }
            response = ExchangeToken(body, publicMcpUrl);
            return true;
        }

        return false;
    }

    internal static bool TryValidateAccessToken(IDictionary<string, string> headers, string publicMcpUrl)
    {
        if (string.IsNullOrWhiteSpace(publicMcpUrl) || headers is null) return false;
        if (!headers.TryGetValue("Authorization", out var authorization)) return false;
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var raw = authorization.Substring(prefix.Length).Trim();
        if (raw.Length < 24 || raw.Length > 512) return false;
        var hash = HashText(raw);
        lock (Sync)
        {
            CleanupLocked();
            if (!Tokens.TryGetValue(hash, out var entry)) return false;
            return entry.ExpiresUtc > Now() && string.Equals(entry.Resource, publicMcpUrl, StringComparison.Ordinal);
        }
    }

    internal static string BuildBearerChallenge(string publicMcpUrl)
    {
        if (!Uri.TryCreate(publicMcpUrl, UriKind.Absolute, out var uri)) return "Bearer scope=\"" + RequiredScope + "\"";
        return "Bearer resource_metadata=\"" + uri.GetLeftPart(UriPartial.Authority)
               + "/.well-known/oauth-protected-resource/mcp\", scope=\"" + RequiredScope + "\"";
    }

    internal static bool IsAllowedLoopbackRedirect(string? value)
    {
        if (!Uri.TryCreate((value ?? string.Empty).Trim(), UriKind.Absolute, out var uri)) return false;
        if (!uri.IsLoopback || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment)) return false;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)) return false;
        return uri.AbsolutePath.Length > 0 && uri.AbsolutePath.Length <= 1024;
    }

    private static McpOAuthHttpResponse Authorize(string query, string resource)
    {
        var form = ParseForm(query);
        if (!EqualsValue(form, "response_type", "code")) return OAuthError(400, "Bad Request", "unsupported_response_type", "only code is supported");
        var clientId = Value(form, "client_id", 128);
        var redirectUri = Value(form, "redirect_uri", 2048);
        var scope = Value(form, "scope", 128);
        var state = Value(form, "state", 1024, required: false);
        var challenge = Value(form, "code_challenge", 256);
        if (!EqualsValue(form, "code_challenge_method", "S256")) return OAuthError(400, "Bad Request", "invalid_request", "PKCE S256 is required");
        if (!IsAllowedLoopbackRedirect(redirectUri)) return OAuthError(400, "Bad Request", "invalid_redirect_uri", "redirect URI must be loopback http");
        if (!string.Equals(scope, RequiredScope, StringComparison.Ordinal)) return OAuthError(400, "Bad Request", "invalid_scope", "unsupported scope");
        if (!McpOAuthConsentStore.HasConsent(clientId, redirectUri, scope))
            return OAuthError(403, "Forbidden", "interaction_required", "deny-by-default: grant access from the local QS3D Agent Center first");

        var code = RandomToken(32);
        lock (Sync)
        {
            CleanupLocked();
            if (Codes.Count >= MaxEntries) return OAuthError(503, "Service Unavailable", "temporarily_unavailable", "authorization-code capacity reached");
            Codes[HashText(code)] = new AuthorizationCodeEntry
            {
                ClientId = clientId,
                RedirectUri = redirectUri,
                CodeChallenge = challenge,
                Resource = resource,
                ExpiresUtc = Now() + (long)AuthorizationCodeLifetime.TotalSeconds
            };
        }
        var separator = redirectUri.IndexOf('?') >= 0 ? "&" : "?";
        var location = redirectUri + separator + "code=" + Uri.EscapeDataString(code);
        if (state.Length > 0) location += "&state=" + Uri.EscapeDataString(state);
        var response = new McpOAuthHttpResponse { StatusCode = 302, Reason = "Found", Body = string.Empty, ContentType = "text/plain; charset=utf-8" };
        response.Headers["Location"] = location;
        response.Headers["Cache-Control"] = "no-store";
        return response;
    }

    private static McpOAuthHttpResponse ExchangeToken(string body, string resource)
    {
        var form = ParseForm(body);
        if (!EqualsValue(form, "grant_type", "authorization_code")) return OAuthError(400, "Bad Request", "unsupported_grant_type", "only authorization_code is supported");
        var code = Value(form, "code", 512);
        var clientId = Value(form, "client_id", 128);
        var redirectUri = Value(form, "redirect_uri", 2048);
        var verifier = Value(form, "code_verifier", 256);

        AuthorizationCodeEntry? entry;
        lock (Sync)
        {
            CleanupLocked();
            var key = HashText(code);
            if (!Codes.TryGetValue(key, out entry)) return OAuthError(400, "Bad Request", "invalid_grant", "authorization code is invalid or already consumed");
            Codes.Remove(key); // one-time credential: consume before verification/reply
        }
        if (entry.ExpiresUtc <= Now()
            || !string.Equals(entry.ClientId, clientId, StringComparison.Ordinal)
            || !string.Equals(entry.RedirectUri, redirectUri, StringComparison.Ordinal)
            || !string.Equals(entry.Resource, resource, StringComparison.Ordinal)
            || !FixedTimeEquals(entry.CodeChallenge, PkceChallenge(verifier)))
            return OAuthError(400, "Bad Request", "invalid_grant", "authorization code binding or PKCE verification failed");

        var accessToken = RandomToken(32);
        lock (Sync)
        {
            CleanupLocked();
            if (Tokens.Count >= MaxEntries) return OAuthError(503, "Service Unavailable", "temporarily_unavailable", "token capacity reached");
            Tokens[HashText(accessToken)] = new AccessTokenEntry
            {
                ClientId = clientId,
                Resource = resource,
                ExpiresUtc = Now() + (long)AccessTokenLifetime.TotalSeconds
            };
        }
        return Json(200, "OK", new Dictionary<string, object?>
        {
            ["access_token"] = accessToken,
            ["token_type"] = "Bearer",
            ["expires_in"] = (long)AccessTokenLifetime.TotalSeconds,
            ["scope"] = RequiredScope
        });
    }

    private static Dictionary<string, string> ParseForm(string? text)
    {
        var raw = (text ?? string.Empty).TrimStart('?');
        if (raw.Length > 32768) throw new InvalidOperationException("OAuth form exceeds bounds.");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (raw.Length == 0) return result;
        var pairs = raw.Split('&');
        if (pairs.Length > 32) throw new InvalidOperationException("OAuth form has too many parameters.");
        foreach (var pair in pairs)
        {
            var equals = pair.IndexOf('=');
            var name = Decode(equals < 0 ? pair : pair.Substring(0, equals));
            var value = Decode(equals < 0 ? string.Empty : pair.Substring(equals + 1));
            if (name.Length == 0 || name.Length > 128 || value.Length > 8192 || result.ContainsKey(name))
                throw new InvalidOperationException("OAuth form parameter is invalid or duplicated.");
            result[name] = value;
        }
        return result;
    }

    private static string Decode(string value) => Uri.UnescapeDataString((value ?? string.Empty).Replace('+', ' '));

    private static string Value(Dictionary<string, string> form, string name, int max, bool required = true)
    {
        if (!form.TryGetValue(name, out var value) || (required && string.IsNullOrWhiteSpace(value)) || value.Length > max)
            throw new InvalidOperationException("OAuth parameter is missing or invalid: " + name);
        return value ?? string.Empty;
    }

    private static bool EqualsValue(Dictionary<string, string> form, string name, string expected) =>
        form.TryGetValue(name, out var value) && string.Equals(value, expected, StringComparison.Ordinal);

    private static string PkceChallenge(string verifier)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string RandomToken(int bytes)
    {
        var buffer = new byte[bytes];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(buffer);
        return Convert.ToBase64String(buffer).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashText(string value)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left ?? string.Empty);
        var b = Encoding.UTF8.GetBytes(right ?? string.Empty);
        var diff = a.Length ^ b.Length;
        var length = Math.Max(a.Length, b.Length);
        for (var i = 0; i < length; i++) diff |= (i < a.Length ? a[i] : (byte)0) ^ (i < b.Length ? b[i] : (byte)0);
        return diff == 0;
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static void CleanupLocked()
    {
        var now = Now();
        foreach (var key in Codes.Where(pair => pair.Value.ExpiresUtc <= now).Select(pair => pair.Key).ToArray()) Codes.Remove(key);
        foreach (var key in Tokens.Where(pair => pair.Value.ExpiresUtc <= now).Select(pair => pair.Key).ToArray()) Tokens.Remove(key);
    }

    private static McpOAuthHttpResponse Json(int status, string reason, Dictionary<string, object?> body) =>
        new() { StatusCode = status, Reason = reason, Body = McpJson.Serialize(body) };

    private static McpOAuthHttpResponse OAuthError(int status, string reason, string error, string description) =>
        Json(status, reason, new Dictionary<string, object?> { ["error"] = error, ["error_description"] = description });
}
