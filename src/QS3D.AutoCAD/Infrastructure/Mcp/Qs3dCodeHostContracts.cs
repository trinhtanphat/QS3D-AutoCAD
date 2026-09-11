namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal sealed class Qs3dCodeHostIdentity
{
    internal Qs3dCodeHostIdentity(string hostId, string sessionId, int processId, string hostVersion)
    {
        HostId = hostId ?? string.Empty;
        SessionId = sessionId ?? string.Empty;
        ProcessId = processId;
        HostVersion = hostVersion ?? string.Empty;
    }

    public string HostId { get; }
    public string SessionId { get; }
    public int ProcessId { get; }
    public string HostVersion { get; }
}

internal sealed class Qs3dCodeDocumentIdentity
{
    internal Qs3dCodeDocumentIdentity(string drawingId, string displayName, bool isNamed)
    {
        DrawingId = drawingId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        IsNamed = isNamed;
    }

    public string DrawingId { get; }
    public string DisplayName { get; }
    public bool IsNamed { get; }
}
internal sealed class Qs3dCodeHostRequest
{
    public string ContractVersion { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string PermissionClass { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string DrawingId { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = string.Empty;
    public bool ConfirmMutation { get; set; }
    public string ActionId { get; set; } = string.Empty;
    public string WriterToken { get; set; } = string.Empty;
}

internal sealed class Qs3dCodeHostResult
{
    internal Qs3dCodeHostResult(bool ok, string requestId, string operation,
        Qs3dCodeHostIdentity? host, Qs3dCodeDocumentIdentity? activeDrawing,
        string payloadJson, string errorCode, string message)
    {
        Ok = ok;
        RequestId = requestId ?? string.Empty;
        Operation = operation ?? string.Empty;
        Host = host;
        ActiveDrawing = activeDrawing;
        PayloadJson = payloadJson ?? string.Empty;
        ErrorCode = errorCode ?? string.Empty;
        Message = message ?? string.Empty;
    }
    public bool Ok { get; }
    public string RequestId { get; }
    public string Operation { get; }
    public Qs3dCodeHostIdentity? Host { get; }
    public Qs3dCodeDocumentIdentity? ActiveDrawing { get; }
    public string PayloadJson { get; }
    public string ErrorCode { get; }
    public string Message { get; }
}
