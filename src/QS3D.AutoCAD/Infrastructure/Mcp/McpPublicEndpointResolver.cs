using System.Net;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal static class McpPublicEndpointResolver
{
    internal static string Resolve(string profileId, string endpointText)
    {
        var profile = McpTransportProfileRegistry.Get(profileId);
        if (!profile.Public)
            return McpJson.Serialize(new Dictionary<string, object?> { ["ready"] = true, ["endpoint"] = profile.Endpoint, ["public"] = false });
        if (profile.Enabled == false || profile.Publish == false)
            return NotReady(profile.Id, "public profile is disabled/publish=false by default");
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint)) return NotReady(profile.Id, "endpoint is not an absolute URI");
        if (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return NotReady(profile.Id, "remote endpoint must use https");
        if (!string.IsNullOrEmpty(endpoint.UserInfo)) return NotReady(profile.Id, "credentials in endpoint URLs are forbidden");
        if (IsLoopback(endpoint)) return NotReady(profile.Id, "public endpoint cannot resolve to loopback");
        return McpJson.Serialize(new Dictionary<string, object?> { ["ready"] = true, ["profile"] = profile.Id, ["endpoint"] = endpoint.GetLeftPart(UriPartial.Path), ["public"] = true });
    }

    internal static bool IsLoopback(Uri endpoint)
    {
        if (string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(endpoint.Host, out var address) && IPAddress.IsLoopback(address);
    }

    private static string NotReady(string profile, string reason) => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["ready"] = false, ["profile"] = profile, ["reason"] = reason, ["fallbackToPublic"] = false
    });
}
