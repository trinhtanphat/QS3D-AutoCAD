using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Bounded loopback-only client for the embedded MCP endpoint. No public fallback exists.</summary>
internal static class McpLocalAgentClient
{
    private const int MaxRequestBytes = 1024 * 1024;
    private const int MaxResponseBytes = 1024 * 1024;

    internal static bool IsLoopback(Uri uri)
    {
        if (uri is null || !uri.IsAbsoluteUri) return false;
        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address);
    }

    internal static string PostJson(string json, int timeoutMs = 5000)
    {
        var endpoint = new Uri(McpTransportSettings.LocalEndpoint, UriKind.Absolute);
        if (!IsLoopback(endpoint)) throw new InvalidOperationException("Local-agent endpoint must be loopback; public fallback is forbidden.");
        var payload = Encoding.UTF8.GetBytes(json ?? "{}");
        if (payload.Length > MaxRequestBytes) throw new InvalidOperationException("Local-agent request exceeds 1 MiB.");
        timeoutMs = Math.Max(100, Math.Min(7000, timeoutMs));

        using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", McpEmbeddedServer.GetBearerToken());
        request.Content = new ByteArrayContent(payload);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        using var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (memory.Length + read > MaxResponseBytes) throw new InvalidOperationException("Local-agent response exceeds 1 MiB.");
            memory.Write(buffer, 0, read);
        }
        return Encoding.UTF8.GetString(memory.ToArray());
    }
}
