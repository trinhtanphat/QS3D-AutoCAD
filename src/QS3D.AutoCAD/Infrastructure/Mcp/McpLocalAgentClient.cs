using System.Net;
using System.Text;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Bounded client for the exact embedded loopback MCP endpoint.</summary>
internal static class McpLocalAgentClient
{
    private const int MaxRequestBytes = 1024 * 1024;
    private const int MaxResponseBytes = 4 * 1024 * 1024;

    internal static bool IsLoopback(Uri? endpoint)
    {
        return endpoint is not null
               && endpoint.IsAbsoluteUri
               && endpoint.IsLoopback
               && string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               && string.IsNullOrEmpty(endpoint.UserInfo)
               && string.IsNullOrEmpty(endpoint.Query)
               && string.IsNullOrEmpty(endpoint.Fragment);
    }

    internal static string Request(string body, int timeoutMilliseconds = 5000)
    {
        var endpoint = new Uri(McpTransportSettings.LocalEndpoint, UriKind.Absolute);
        ValidateEndpoint(endpoint);
        if (timeoutMilliseconds < 100 || timeoutMilliseconds > 15000)
            throw new InvalidOperationException("Local MCP timeout must be between 100 and 15000 ms.");
        var payload = Encoding.UTF8.GetBytes(body ?? string.Empty);
        if (payload.Length == 0 || payload.Length > MaxRequestBytes)
            throw new InvalidOperationException("Local MCP request body is empty or exceeds the allowed size.");

#pragma warning disable SYSLIB0014
        var request = (HttpWebRequest)WebRequest.Create(endpoint);
#pragma warning restore SYSLIB0014
        request.AllowAutoRedirect = false;
        request.Method = "POST";
        request.ContentType = "application/json";
        request.Accept = "application/json";
        request.Timeout = timeoutMilliseconds;
        request.ReadWriteTimeout = timeoutMilliseconds;
        request.Headers[HttpRequestHeader.Authorization] = "Bearer " + McpEmbeddedServer.GetBearerToken();
        request.Headers["MCP-Protocol-Version"] = McpEmbeddedServer.ProtocolVersion;
        request.ContentLength = payload.Length;
        using (var stream = request.GetRequestStream()) stream.Write(payload, 0, payload.Length);

        try
        {
            using var response = (HttpWebResponse)request.GetResponse();
            if (response.ContentLength > MaxResponseBytes)
                throw new InvalidOperationException("Local MCP response exceeds the allowed size.");
            using var responseStream = response.GetResponseStream();
            return ReadBounded(responseStream, response.ContentLength);
        }
        catch (WebException ex) when (ex.Response is HttpWebResponse response)
        {
            using (response)
            using (var responseStream = response.GetResponseStream())
            {
                var detail = ReadBounded(responseStream, response.ContentLength);
                throw new InvalidOperationException("Local MCP HTTP " + (int)response.StatusCode + ": " + Bound(detail, 1024), ex);
            }
        }
    }

    internal static string RunReadOnlySelfTest()
    {
        var initialize = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}";
        var initialized = Request(initialize);
        if (initialized.IndexOf(McpEmbeddedServer.ServerName, StringComparison.Ordinal) < 0)
            throw new InvalidOperationException("Local MCP initialize response did not identify the canonical AutoCAD server.");
        var list = Request("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\",\"params\":{}}");
        if (list.IndexOf("autocad_status", StringComparison.Ordinal) < 0 || list.IndexOf("qs3d_status", StringComparison.Ordinal) < 0)
            throw new InvalidOperationException("Local MCP tools/list is missing required AutoCAD/QS3D tools.");
        return McpJson.Serialize(new Dictionary<string, object?> { ["ok"] = true, ["loopback"] = true, ["mutationExecuted"] = false });
    }

    private static void ValidateEndpoint(Uri endpoint)
    {
        var expected = new Uri(McpTransportSettings.LocalEndpoint, UriKind.Absolute);
        if (!IsLoopback(endpoint)
            || !string.Equals(endpoint.Host, expected.Host, StringComparison.OrdinalIgnoreCase)
            || endpoint.Port != expected.Port
            || !string.Equals(endpoint.AbsolutePath, "/mcp", StringComparison.Ordinal))
            throw new InvalidOperationException("Local MCP client accepts only the current loopback /mcp endpoint; no public fallback is allowed.");
    }

    private static string ReadBounded(Stream? stream, long advertisedLength)
    {
        if (stream is null) return string.Empty;
        if (advertisedLength > MaxResponseBytes)
            throw new InvalidOperationException("Local MCP response exceeds the allowed size.");
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0) break;
            if (memory.Length + read > MaxResponseBytes)
                throw new InvalidOperationException("Local MCP response exceeds the allowed size.");
            memory.Write(buffer, 0, read);
        }
        return new UTF8Encoding(false, true).GetString(memory.ToArray());
    }

    private static string Bound(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max);
}
