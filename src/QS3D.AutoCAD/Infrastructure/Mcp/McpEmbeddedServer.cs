using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>
/// Loopback-only embedded Streamable-HTTP MCP endpoint. Network work stays off the
/// AutoCAD application thread; CAD work is marshalled by the MCP runtime.
/// </summary>
internal static class McpEmbeddedServer
{
    private const int MaxHeaderBytes = 64 * 1024;
    private const int MaxBodyBytes = 1024 * 1024;
    private const int MaxConcurrentClients = 16;
    private const string BearerEnvironment = "QS3D_AUTOCAD_MCP_BEARER_TOKEN";
    private const string TokenFileName = "mcp-bearer-token.txt";
    private static readonly object Sync = new();
    private static readonly SemaphoreSlim ClientSlots = new(MaxConcurrentClients, MaxConcurrentClients);
    private static TcpListener? _listener;
    private static Thread? _listenerThread;
    private static volatile bool _stopping;
    private static string _lastError = string.Empty;
    private static string _bearerToken = string.Empty;
    private static string _tokenSource = string.Empty;

    internal const string ProtocolVersion = "2025-06-18";
    internal const string ServerName = "qs3d-autocad-embedded-1";

    internal static bool IsRunning
    {
        get { lock (Sync) return _listener is not null && !_stopping; }
    }

    internal static string LastError
    {
        get { lock (Sync) return _lastError; }
    }

    internal static string TokenFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QS3D", "MCP", TokenFileName);

    internal static string TokenSource
    {
        get { EnsureBearerToken(); lock (Sync) return _tokenSource; }
    }

    internal static void Start()
    {
        lock (Sync)
        {
            if (_listener is not null && !_stopping) return;
            EnsureBearerToken();
            McpCadAgentRuntime.ResetForServerStart();
            _stopping = false;
            _lastError = string.Empty;
            var listener = new TcpListener(IPAddress.Loopback, McpTransportSettings.Port);
            listener.Server.NoDelay = true;
            listener.Start(32);
            _listener = listener;
            _listenerThread = new Thread(ServeLoop)
            {
                IsBackground = true,
                Name = "QS3D AutoCAD MCP loopback server"
            };
            _listenerThread.Start();
            McpDiagnosticHub.Log("mcp", "embedded server started at " + McpTransportSettings.LocalEndpoint);
        }
    }

    internal static void Stop()
    {
        Thread? thread;
        lock (Sync)
        {
            if (_listener is null && _listenerThread is null) return;
            _stopping = true;
            thread = _listenerThread;
            try { _listener?.Stop(); } catch { }
            _listener = null;
            _listenerThread = null;
        }

        if (thread is not null && thread != Thread.CurrentThread)
        {
            try { thread.Join(1000); } catch { }
        }
        McpDiagnosticHub.Log("mcp", "embedded server stopped");
    }

    internal static string GetBearerToken()
    {
        EnsureBearerToken();
        lock (Sync) return _bearerToken;
    }

    private static void ServeLoop()
    {
        while (!_stopping)
        {
            TcpClient? client = null;
            try
            {
                var listener = _listener;
                if (listener is null) return;
                client = listener.AcceptTcpClient();
                client.NoDelay = true;
                if (!ClientSlots.Wait(0))
                {
                    client.Dispose();
                    client = null;
                    continue;
                }

                ThreadPool.QueueUserWorkItem(HandleClient, client);
                client = null;
            }
            catch (SocketException ex)
            {
                if (_stopping) return;
                SetLastError("socket: " + ex.Message);
                Thread.Sleep(100);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                if (_stopping) return;
                SetLastError("listener: " + ex.Message);
                Thread.Sleep(100);
            }
            finally
            {
                try { client?.Dispose(); } catch { }
            }
        }
    }

    private static void HandleClient(object? state)
    {
        try
        {
            using var client = state as TcpClient;
            if (client is null) return;
            using var stream = client.GetStream();
            stream.ReadTimeout = 10000;
            stream.WriteTimeout = 10000;
            try
            {
                var request = ReadRequest(stream);
                if (request is not null) HandleRequest(stream, request);
            }
            catch (HttpProtocolException ex)
            {
                WriteResponse(stream, ex.StatusCode, ex.Reason, McpJson.Serialize(new Dictionary<string, object?>
                {
                    ["error"] = ex.Message
                }), null);
            }
        }
        catch (Exception ex)
        {
            SetLastError("request: " + ex.Message);
        }
        finally
        {
            ClientSlots.Release();
        }
    }

    private static HttpRequest? ReadRequest(NetworkStream stream)
    {
        var buffer = new byte[4096];
        using var accumulated = new MemoryStream();
        var headerEnd = -1;
        while (headerEnd < 0)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0) return null;
            accumulated.Write(buffer, 0, read);
            if (accumulated.Length > MaxHeaderBytes + MaxBodyBytes)
                throw new HttpProtocolException(413, "Payload Too Large", "MCP HTTP request exceeds configured bounds.");
            headerEnd = FindHeaderEnd(accumulated.GetBuffer(), checked((int)accumulated.Length));
            if (headerEnd < 0 && accumulated.Length > MaxHeaderBytes)
                throw new HttpProtocolException(431, "Request Header Fields Too Large", "MCP HTTP headers exceed 64 KiB.");
        }

        var all = accumulated.ToArray();
        var headerText = Encoding.ASCII.GetString(all, 0, headerEnd);
        var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
        var requestParts = lines.Length == 0
            ? new string[0]
            : lines[0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (requestParts.Length != 3
            || (!string.Equals(requestParts[2], "HTTP/1.1", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(requestParts[2], "HTTP/1.0", StringComparison.OrdinalIgnoreCase)))
            throw new HttpProtocolException(400, "Bad Request", "Invalid MCP HTTP request line.");

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            if (lines[index].Length == 0) continue;
            var separator = lines[index].IndexOf(':');
            if (separator <= 0) throw new HttpProtocolException(400, "Bad Request", "Malformed HTTP header.");
            var name = lines[index].Substring(0, separator).Trim();
            var value = lines[index].Substring(separator + 1).Trim();
            if (name.Length == 0 || value.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                throw new HttpProtocolException(400, "Bad Request", "Malformed HTTP header.");
            if (headers.ContainsKey(name) && IsCriticalSingletonHeader(name))
                throw new HttpProtocolException(400, "Bad Request", "Duplicate security-sensitive HTTP header.");
            headers[name] = value;
        }

        if (headers.TryGetValue("Transfer-Encoding", out var transferEncoding) && !string.IsNullOrWhiteSpace(transferEncoding))
            throw new HttpProtocolException(400, "Bad Request", "Transfer-Encoding is not supported; use Content-Length.");

        var contentLength = 0;
        if (headers.TryGetValue("Content-Length", out var lengthText)
            && (!int.TryParse(lengthText, NumberStyles.None, CultureInfo.InvariantCulture, out contentLength)
                || contentLength < 0 || contentLength > MaxBodyBytes))
            throw new HttpProtocolException(400, "Bad Request", "Invalid MCP HTTP Content-Length.");

        var bodyOffset = headerEnd + 4;
        var body = new byte[contentLength];
        var available = Math.Max(0, Math.Min(contentLength, all.Length - bodyOffset));
        if (available > 0) Buffer.BlockCopy(all, bodyOffset, body, 0, available);
        var written = available;
        while (written < contentLength)
        {
            var read = stream.Read(body, written, contentLength - written);
            if (read <= 0) throw new HttpProtocolException(400, "Bad Request", "Unexpected end of MCP HTTP body.");
            written += read;
        }

        return new HttpRequest(requestParts[0], requestParts[1], headers, Encoding.UTF8.GetString(body));
    }

    private static void HandleRequest(NetworkStream stream, HttpRequest request)
    {
        var path = request.Path.Split('?')[0];
        if (string.Equals(path, "/healthz", StringComparison.Ordinal))
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                WriteResponse(stream, 405, "Method Not Allowed", "{\"error\":\"GET required\"}", null);
                return;
            }

            WriteResponse(stream, 200, "OK", McpJson.Serialize(new Dictionary<string, object?>
            {
                ["ok"] = true,
                ["server"] = ServerName,
                ["protocol"] = ProtocolVersion,
                ["endpoint"] = McpTransportSettings.LocalEndpoint
            }), null);
            return;
        }

        if (!string.Equals(path, "/mcp", StringComparison.Ordinal))
        {
            WriteResponse(stream, 404, "Not Found", "{\"error\":\"not found\"}", null);
            return;
        }

        if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            WriteResponse(stream, 405, "Method Not Allowed", "{\"error\":\"POST required\"}", null);
            return;
        }

        if (!Authorized(request.Headers))
        {
            WriteResponse(stream, 401, "Unauthorized", "{\"error\":\"invalid bearer token\"}", "Bearer");
            return;
        }

        if (request.Body.Length == 0 || Encoding.UTF8.GetByteCount(request.Body) > MaxBodyBytes)
        {
            WriteResponse(stream, 400, "Bad Request", "{\"error\":\"JSON-RPC body is required\"}", null);
            return;
        }

        Dictionary<string, object?> message;
        try
        {
            message = McpJson.ParseObject(request.Body);
        }
        catch (Exception ex)
        {
            WriteResponse(stream, 200, "OK", JsonRpcError(null, -32700, "Parse error", ex.Message), null);
            return;
        }

        var id = message.TryGetValue("id", out var idValue) ? idValue : null;
        var hasId = message.ContainsKey("id");
        if (!message.TryGetValue("jsonrpc", out var jsonRpc) || !string.Equals(jsonRpc as string, "2.0", StringComparison.Ordinal)
            || !message.TryGetValue("method", out var methodValue) || methodValue is not string method || method.Length == 0)
        {
            WriteResponse(stream, 200, "OK", JsonRpcError(id, -32600, "Invalid Request", "jsonrpc=2.0 and method are required."), null);
            return;
        }

        if (!hasId)
        {
            if (string.Equals(method, "notifications/initialized", StringComparison.Ordinal)
                || method.StartsWith("notifications/", StringComparison.Ordinal))
            {
                WriteEmptyResponse(stream, 202, "Accepted");
                return;
            }
        }

        var parameters = message.TryGetValue("params", out var paramsValue) && paramsValue is Dictionary<string, object?> map
            ? map
            : new Dictionary<string, object?>();

        try
        {
            var result = DispatchMethod(method, parameters);
            WriteResponse(stream, 200, "OK", JsonRpcResult(id, result), null);
        }
        catch (UnknownMethodException ex)
        {
            WriteResponse(stream, 200, "OK", JsonRpcError(id, -32601, "Method not found", ex.Message), null);
        }
        catch (Exception ex)
        {
            SetLastError("rpc " + method + ": " + ex.Message);
            WriteResponse(stream, 200, "OK", JsonRpcError(id, -32603, "Internal error", Bounded(ex.Message, 2048)), null);
        }
    }

    private static object DispatchMethod(string method, Dictionary<string, object?> parameters)
    {
        switch (method)
        {
            case "initialize":
                return new Dictionary<string, object?>
                {
                    ["protocolVersion"] = ProtocolVersion,
                    ["capabilities"] = new Dictionary<string, object?>
                    {
                        ["tools"] = new Dictionary<string, object?> { ["listChanged"] = false }
                    },
                    ["serverInfo"] = new Dictionary<string, object?>
                    {
                        ["name"] = ServerName,
                        ["version"] = "1.0"
                    }
                };
            case "ping":
                return new Dictionary<string, object?>();
            case "tools/list":
                return new Dictionary<string, object?>
                {
                    ["tools"] = McpToolRegistry.ToolDescriptors().Select(descriptor => new Dictionary<string, object?>
                    {
                        ["name"] = descriptor.Name,
                        ["description"] = descriptor.Description,
                        ["inputSchema"] = McpJson.Parse(descriptor.InputSchemaJson)
                    }).ToList()
                };
            case "tools/call":
                return CallTool(parameters);
            default:
                throw new UnknownMethodException("Unsupported MCP method: " + method);
        }
    }

    private static object CallTool(Dictionary<string, object?> parameters)
    {
        var name = parameters.TryGetValue("name", out var nameValue) ? nameValue as string : null;
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("tools/call requires params.name.");
        var arguments = parameters.TryGetValue("arguments", out var argumentsValue) && argumentsValue is Dictionary<string, object?> args
            ? args
            : new Dictionary<string, object?>();
        try
        {
            var result = McpCadAgentRuntime.Call(name!, McpJson.Serialize(arguments));
            return new Dictionary<string, object?>
            {
                ["content"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["type"] = "text", ["text"] = result }
                },
                ["isError"] = false
            };
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object?>
            {
                ["content"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["type"] = "text", ["text"] = Bounded(ex.Message, 4096) }
                },
                ["isError"] = true
            };
        }
    }

    private static string JsonRpcResult(object? id, object result) => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id,
        ["result"] = result
    });

    private static string JsonRpcError(object? id, int code, string message, string detail) => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id,
        ["error"] = new Dictionary<string, object?>
        {
            ["code"] = code,
            ["message"] = message,
            ["data"] = Bounded(detail, 2048)
        }
    });

    private static bool Authorized(Dictionary<string, string> headers)
    {
        if (!headers.TryGetValue("Authorization", out var authorization)) return false;
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var supplied = authorization.Substring(prefix.Length).Trim();
        return FixedTimeEquals(supplied, GetBearerToken());
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
        var rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
        var difference = leftBytes.Length ^ rightBytes.Length;
        var length = Math.Max(leftBytes.Length, rightBytes.Length);
        for (var index = 0; index < length; index++)
        {
            var a = index < leftBytes.Length ? leftBytes[index] : (byte)0;
            var b = index < rightBytes.Length ? rightBytes[index] : (byte)0;
            difference |= a ^ b;
        }
        return difference == 0;
    }

    private static void EnsureBearerToken()
    {
        lock (Sync)
        {
            if (_bearerToken.Length > 0) return;
            var fromEnvironment = (Environment.GetEnvironmentVariable(BearerEnvironment) ?? string.Empty).Trim();
            if (fromEnvironment.Length >= 24 && fromEnvironment.Length <= 512)
            {
                _bearerToken = fromEnvironment;
                _tokenSource = "environment";
                return;
            }

            try
            {
                var path = TokenFilePath;
                var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Token directory is unavailable.");
                Directory.CreateDirectory(directory);
                if (File.Exists(path) && new FileInfo(path).Length is > 0 and <= 1024)
                {
                    var existing = File.ReadAllText(path).Trim();
                    if (existing.Length >= 24 && existing.Length <= 512)
                    {
                        _bearerToken = existing;
                        _tokenSource = "file";
                        return;
                    }
                }

                var bytes = new byte[32];
                using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
                var generated = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                File.WriteAllText(path, generated, new UTF8Encoding(false));
                _bearerToken = generated;
                _tokenSource = "generated-file";
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Unable to initialize MCP bearer token: " + ex.Message, ex);
            }
        }
    }

    private static int FindHeaderEnd(byte[] buffer, int length)
    {
        for (var index = 0; index <= length - 4; index++)
            if (buffer[index] == 13 && buffer[index + 1] == 10 && buffer[index + 2] == 13 && buffer[index + 3] == 10)
                return index;
        return -1;
    }

    private static bool IsCriticalSingletonHeader(string name) =>
        string.Equals(name, "Authorization", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "Host", StringComparison.OrdinalIgnoreCase);

    private static void WriteResponse(NetworkStream stream, int statusCode, string reason, string body, string? authenticateScheme)
    {
        var payload = Encoding.UTF8.GetBytes(body ?? string.Empty);
        var header = new StringBuilder()
            .Append("HTTP/1.1 ").Append(statusCode.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(reason).Append("\r\n")
            .Append("Content-Type: application/json; charset=utf-8\r\n")
            .Append("Content-Length: ").Append(payload.Length.ToString(CultureInfo.InvariantCulture)).Append("\r\n")
            .Append("Cache-Control: no-store\r\n")
            .Append("Connection: close\r\n");
        if (!string.IsNullOrWhiteSpace(authenticateScheme)) header.Append("WWW-Authenticate: ").Append(authenticateScheme).Append("\r\n");
        header.Append("\r\n");
        var headerBytes = Encoding.ASCII.GetBytes(header.ToString());
        stream.Write(headerBytes, 0, headerBytes.Length);
        if (payload.Length > 0) stream.Write(payload, 0, payload.Length);
        stream.Flush();
    }

    private static void WriteEmptyResponse(NetworkStream stream, int statusCode, string reason)
    {
        var header = "HTTP/1.1 " + statusCode.ToString(CultureInfo.InvariantCulture) + " " + reason
                     + "\r\nContent-Length: 0\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
        var bytes = Encoding.ASCII.GetBytes(header);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private static void SetLastError(string error)
    {
        var bounded = Bounded(error, 2048);
        lock (Sync) _lastError = bounded;
        McpDiagnosticHub.Log("mcp-error", bounded);
    }

    private static string Bounded(string? value, int max)
    {
        var text = value ?? string.Empty;
        return text.Length <= max ? text : text.Substring(0, max);
    }

    private sealed class HttpRequest
    {
        internal HttpRequest(string method, string path, Dictionary<string, string> headers, string body)
        {
            Method = method;
            Path = path;
            Headers = headers;
            Body = body;
        }

        internal string Method { get; }
        internal string Path { get; }
        internal Dictionary<string, string> Headers { get; }
        internal string Body { get; }
    }

    private sealed class HttpProtocolException : Exception
    {
        internal HttpProtocolException(int statusCode, string reason, string message) : base(message)
        {
            StatusCode = statusCode;
            Reason = reason;
        }

        internal int StatusCode { get; }
        internal string Reason { get; }
    }

    private sealed class UnknownMethodException : Exception
    {
        internal UnknownMethodException(string message) : base(message)
        {
        }
    }
}
