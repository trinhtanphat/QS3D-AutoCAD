using System.Net;
using System.Net.Sockets;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Validates the single public MCP URL without activating any provider.</summary>
internal static class McpPublicEndpointResolver
{
    private const string PublicUrlEnvironment = "QS3D_MCP_PUBLIC_URL";

    internal static string Resolve()
    {
        if (!McpTransportSettings.ExternalTransportEnabled) return string.Empty;

        var selected = McpTransportProfileRegistry.GetSelected();
        if (!selected.Public) return string.Empty;

        var candidate = string.Equals(selected.Provider, "cloudflare", StringComparison.OrdinalIgnoreCase)
            ? McpCloudflareOnboarding.ConfiguredPublicMcpUrl
            : McpSecureTunnelRuntime.PublicMcpUrl;
        var normalized = NormalizeCandidate(candidate);
        if (normalized.Length > 0) return normalized;

        return NormalizeCandidate(Environment.GetEnvironmentVariable(PublicUrlEnvironment) ?? string.Empty);
    }

    internal static string NormalizeCandidate(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (raw.Length == 0 || !Uri.TryCreate(raw, UriKind.Absolute, out var uri)) return string.Empty;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return string.Empty;
        if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return string.Empty;
        if (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address) && IsPrivateOrLocalAddress(address)) return string.Empty;

        var path = uri.AbsolutePath;
        if (string.IsNullOrEmpty(path) || path == "/") path = "/mcp";
        else if (!string.Equals(path.TrimEnd('/'), "/mcp", StringComparison.OrdinalIgnoreCase)) return string.Empty;

        var builder = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Path = "/mcp", Query = string.Empty, Fragment = string.Empty };
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private static bool IsPrivateOrLocalAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork && bytes.Length == 4)
        {
            var first = bytes[0];
            var second = bytes[1];
            return first == 0 || first == 10 || first == 127 || first >= 224
                || (first == 100 && second >= 64 && second <= 127)
                || (first == 169 && second == 254)
                || (first == 172 && second >= 16 && second <= 31)
                || (first == 192 && second == 168);
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6 && bytes.Length == 16)
            return address.Equals(IPAddress.IPv6Any) || address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal || (bytes[0] & 0xFE) == 0xFC;
        return address.AddressFamily != AddressFamily.InterNetwork && address.AddressFamily != AddressFamily.InterNetworkV6;
    }
}
