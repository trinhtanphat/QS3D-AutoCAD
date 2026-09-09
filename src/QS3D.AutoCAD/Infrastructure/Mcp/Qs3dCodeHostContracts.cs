namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal sealed class Qs3dCodeHostRequest
{
    internal const string CurrentContractVersion = "qs3d-codehost-ipc-v1";
    internal required string ContractVersion { get; init; }
    internal required string RequestId { get; init; }
    internal required string Operation { get; init; }
    internal required string Authorization { get; init; }
    internal required Dictionary<string, object?> Arguments { get; init; }

    internal static Qs3dCodeHostRequest Parse(string json)
    {
        var map = McpJson.ParseObject(json);
        var contractVersion = String(map, "contractVersion", 64);
        var requestId = String(map, "requestId", 128);
        var operation = String(map, "operation", 64);
        var authorization = String(map, "authorization", 256);
        if (!string.Equals(contractVersion, CurrentContractVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Unsupported QS3D code-host contractVersion.");
        if (requestId.Length == 0) throw new InvalidOperationException("requestId is required.");
        if (operation.Length == 0) throw new InvalidOperationException("operation is required.");
        var arguments = map.TryGetValue("arguments", out var raw) && raw is Dictionary<string, object?> objectMap
            ? objectMap : new Dictionary<string, object?>();
        return new Qs3dCodeHostRequest
        {
            ContractVersion = contractVersion,
            RequestId = requestId,
            Operation = operation,
            Authorization = authorization,
            Arguments = arguments
        };
    }

    internal static string Success(string requestId, object? result) => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["contractVersion"] = CurrentContractVersion,
        ["requestId"] = requestId,
        ["ok"] = true,
        ["result"] = result
    });

    internal static string Failure(string requestId, string error) => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["contractVersion"] = CurrentContractVersion,
        ["requestId"] = requestId,
        ["ok"] = false,
        ["error"] = Bound(error, 1024)
    });

    private static string String(Dictionary<string, object?> map, string name, int max)
    {
        var value = map.TryGetValue(name, out var raw) ? raw as string ?? string.Empty : string.Empty;
        if (value.Length > max) throw new InvalidOperationException(name + " exceeds the bounded IPC contract limit.");
        return value.Trim();
    }

    private static string Bound(string value, int max) => value.Length <= max ? value : value.Substring(0, max);
}
