using System.Security.Cryptography;
using System.Text;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Local IPC bridge into the canonical MCP runtime. It cannot execute shell commands.</summary>
internal static class Qs3dCodeHostBridge
{
    internal static string Dispatch(string requestJson)
    {
        Qs3dCodeHostRequest? request = null;
        try
        {
            request = Qs3dCodeHostRequest.Parse(requestJson);
            RequireAuthorization(request.Authorization);
            var result = request.Operation switch
            {
                "status" => McpJson.Parse(McpEmbeddedServerV2.StatusJson()),
                "tool.call" => ExecuteTool(request.Arguments),
                _ => throw new InvalidOperationException("Unsupported local IPC operation; arbitrary shell execution is not available.")
            };
            return Qs3dCodeHostRequest.Success(request.RequestId, result);
        }
        catch (Exception ex)
        {
            McpDiagnosticHub.Log("codehost-ipc", "request failed: " + ex.Message);
            return Qs3dCodeHostRequest.Failure(request?.RequestId ?? string.Empty, ex.Message);
        }
    }

    private static object? ExecuteTool(Dictionary<string, object?> arguments)
    {
        var tool = arguments.TryGetValue("tool", out var toolRaw) ? toolRaw as string ?? string.Empty : string.Empty;
        if (tool.Length == 0 || tool.Length > 128) throw new InvalidOperationException("tool is required and bounded.");
        if (McpToolRegistry.Find(tool) is null) throw new InvalidOperationException("IPC tool is not in the canonical MCP registry: " + tool);
        var body = arguments.TryGetValue("arguments", out var bodyRaw) && bodyRaw is Dictionary<string, object?> bodyMap
            ? McpJson.Serialize(bodyMap) : "{}";
        var resultJson = McpCadAgentRuntime.Call(tool, body);
        return McpJson.Parse(resultJson);
    }

    private static void RequireAuthorization(string authorization)
    {
        var expected = McpEmbeddedServer.GetBearerToken();
        if (!FixedTimeEquals(authorization ?? string.Empty, expected))
            throw new InvalidOperationException("Local IPC authorization failed.");
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        using var sha = SHA256.Create();
        var a = sha.ComputeHash(Encoding.UTF8.GetBytes(left ?? string.Empty));
        var b = sha.ComputeHash(Encoding.UTF8.GetBytes(right ?? string.Empty));
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
