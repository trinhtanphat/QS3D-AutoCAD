using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Typed allow-listed bridge from local QS3D Code IPC to canonical MCP execution.</summary>
internal static class Qs3dCodeHostBridge
{
    private const int MaxArgumentsCharacters = 65536;
    private static readonly IReadOnlyDictionary<string, string> ReadOperations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["host.status"] = "autocad_status",
            ["mcp.status"] = "mcp_status",
            ["drawing.active"] = "cad_active_document",
            ["drawing.selection"] = "cad_selection",
            ["drawing.view"] = "cad_view_state",
            ["drawing.layers"] = "cad_layer_snapshot",
            ["diagnostics.tail"] = "cad_audit_tail"
        };

    private static readonly HashSet<string> MutationOperations = new(StringComparer.Ordinal)
    {
        "cad_create_line", "cad_create_circle", "cad_create_arc", "cad_create_polyline",
        "cad_create_text", "cad_create_mtext", "cad_entity_transform", "cad_entity_delete",
        "cad_entity_set_layer", "cad_layer", "cad_create_box", "cad_extrude",
        "cad_boolean_union", "cad_boolean_subtract", "cad_boolean_intersect",
        "cad_save", "cad_save_as", "cad_layer_set_state", "cad_layer_restore",
        "cad_view_zoom_extents", "cad_view_fit_entities", "cad_view_set", "qs3d_run_command"
    };
    internal static Qs3dCodeHostResult Execute(Qs3dCodeHostRequest request)
    {
        Qs3dCodeHostIdentity? host = null;
        Qs3dCodeDocumentIdentity? active = null;
        var requestId = string.Empty;
        var operation = string.Empty;
        try
        {
            if (request is null) throw new InvalidOperationException("request_required: request is required.");
            RequireContract(request.ContractVersion);
            requestId = NormalizeToken(request.RequestId, 128, "requestId");
            operation = NormalizeToken(request.Operation, 96, "operation");
            host = Qs3dCodeHostLocalIpcServer.GetHostIdentity();
            active = CaptureActiveDocumentIdentity(host.SessionId);

            if (string.Equals(operation, "host.identity", StringComparison.Ordinal))
            {
                RequirePermission(request.PermissionClass, "read");
                RejectStale(request, host, active, requireDrawing: false);
                return Success(requestId, operation, host, active, "{}");
            }

            if (string.Equals(operation, "cad_agent_stop", StringComparison.Ordinal))
            {
                RequirePermission(request.PermissionClass, "emergency-stop");
                if (!request.ConfirmMutation)
                    throw new InvalidOperationException("confirmation_required: confirmMutation=true is required.");
                RejectStale(request, host, active, requireDrawing: false);
                var body = BuildMutationArguments(request);
                return Success(requestId, operation, host, active, McpCadAgentRuntime.Call("cad_agent_stop", body));
            }

            if (ReadOperations.TryGetValue(operation, out var readTool))
            {
                RequirePermission(request.PermissionClass, "read");
                RejectStale(request, host, active, requireDrawing: operation.StartsWith("drawing.", StringComparison.Ordinal));
                return Success(requestId, operation, host, active, McpCadAgentRuntime.Call(readTool, BoundArguments(request.ArgumentsJson)));
            }
            RejectStale(request, host, active, requireDrawing: true);
            RequirePermission(request.PermissionClass, "cad-mutation");
            if (!MutationOperations.Contains(operation))
                throw new InvalidOperationException("operation_not_allowed: mutation operation is not allow-listed.");
            if (!request.ConfirmMutation)
                throw new InvalidOperationException("confirmation_required: confirmMutation=true is required.");
            return Success(requestId, operation, host, active,
                McpCadAgentRuntime.Call(operation, BuildMutationArguments(request)));
        }
        catch (StaleIdentityException ex)
        {
            return Failure(requestId, operation, host, active, "stale_identity", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Failure(requestId, operation, host, active, Classify(ex.Message), ex.Message);
        }
        catch (Exception ex)
        {
            McpDiagnosticHub.Log("qs3d-code-host", "bridge failure: " + ex.GetType().Name);
            return Failure(requestId, operation, host, active, "host_error",
                "QS3D Code host operation failed. Inspect bounded diagnostics for details.");
        }
    }

    private static string BuildMutationArguments(Qs3dCodeHostRequest request)
    {
        var map = McpJson.ParseObject(BoundArguments(request.ArgumentsJson));
        map.Remove("confirmMutation");
        map.Remove("actionId");
        map.Remove("writerToken");
        map["confirmMutation"] = request.ConfirmMutation;
        if (!string.IsNullOrWhiteSpace(request.ActionId)) map["actionId"] = Bound(request.ActionId, 128);
        if (!string.IsNullOrWhiteSpace(request.WriterToken)) map["writerToken"] = Bound(request.WriterToken, 128);
        return McpJson.Serialize(map);
    }

    private static string BoundArguments(string? value)
    {
        var arguments = (value ?? string.Empty).Trim();
        if (arguments.Length == 0) arguments = "{}";
        if (arguments.Length > MaxArgumentsCharacters || arguments.IndexOf('\0') >= 0)
            throw new InvalidOperationException("invalid_arguments: arguments exceed the local IPC boundary.");
        _ = McpJson.ParseObject(arguments);
        return arguments;
    }
    private static Qs3dCodeDocumentIdentity CaptureActiveDocumentIdentity(string sessionId)
    {
        return McpDiagnosticHub.InvokeInCadContext(() =>
        {
            var document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document is null) return new Qs3dCodeDocumentIdentity(string.Empty, string.Empty, false);
            var name = document.Name ?? string.Empty;
            var runtimeId = RuntimeHelpers.GetHashCode(document).ToString(CultureInfo.InvariantCulture);
            return new Qs3dCodeDocumentIdentity(
                HashIdentity(sessionId + "\n" + name + "\n" + runtimeId),
                SafeLeaf(name),
                Path.IsPathRooted(name));
        });
    }

    private static void RejectStale(Qs3dCodeHostRequest request, Qs3dCodeHostIdentity host,
        Qs3dCodeDocumentIdentity active, bool requireDrawing)
    {
        if (!FixedEquals(request.HostId, host.HostId))
            throw new StaleIdentityException("Host identity changed; refresh local QS3D Code state.");
        if (!FixedEquals(request.SessionId, host.SessionId))
            throw new StaleIdentityException("Host session changed; refresh local QS3D Code state.");
        if (!requireDrawing) return;
        if (active.DrawingId.Length == 0)
            throw new StaleIdentityException("No active drawing is available for this operation.");
        if (!FixedEquals(request.DrawingId, active.DrawingId))
            throw new StaleIdentityException("Active drawing changed; refresh identity before dispatch.");
    }

    private static void RequireContract(string? version)
    {
        if (!string.Equals((version ?? string.Empty).Trim(), Qs3dCodeHostLocalIpcServer.ContractVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("contract_mismatch: unsupported contractVersion.");
    }

    private static void RequirePermission(string? actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidOperationException("permission_denied: permissionClass is not admitted for this operation.");
    }
    private static string NormalizeToken(string? value, int maxLength, string name)
    {
        var token = (value ?? string.Empty).Trim();
        if (token.Length == 0 || token.Length > maxLength)
            throw new InvalidOperationException("invalid_arguments: " + name + " is missing or exceeds bounds.");
        foreach (var ch in token)
            if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.'))
                throw new InvalidOperationException("invalid_arguments: " + name + " contains unsupported characters.");
        return token;
    }

    private static string HashIdentity(string value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var valueByte in bytes) builder.Append(valueByte.ToString("x2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static string SafeLeaf(string value)
    {
        try
        {
            var leaf = Path.GetFileName(value ?? string.Empty);
            return leaf.Length <= 260 ? leaf : leaf.Substring(0, 260);
        }
        catch { return string.Empty; }
    }

    private static bool FixedEquals(string? left, string? right)
    {
        var a = Encoding.UTF8.GetBytes(left ?? string.Empty);
        var b = Encoding.UTF8.GetBytes(right ?? string.Empty);
        var difference = a.Length ^ b.Length;
        var count = Math.Max(a.Length, b.Length);
        for (var index = 0; index < count; index++)
            difference |= (index < a.Length ? a[index] : (byte)0) ^ (index < b.Length ? b[index] : (byte)0);
        return difference == 0;
    }
    private static Qs3dCodeHostResult Success(string requestId, string operation,
        Qs3dCodeHostIdentity host, Qs3dCodeDocumentIdentity active, string payload) =>
        new(true, requestId, operation, host, active, payload, string.Empty, string.Empty);

    private static Qs3dCodeHostResult Failure(string requestId, string operation,
        Qs3dCodeHostIdentity? host, Qs3dCodeDocumentIdentity? active, string code, string message) =>
        new(false, requestId, operation, host, active, string.Empty, code, Bound(message, 1200));

    private static string Classify(string? message)
    {
        var value = message ?? string.Empty;
        if (value.StartsWith("permission_denied:", StringComparison.Ordinal)) return "permission_denied";
        if (value.StartsWith("contract_mismatch:", StringComparison.Ordinal)) return "contract_mismatch";
        if (value.StartsWith("confirmation_required:", StringComparison.Ordinal)) return "confirmation_required";
        if (value.StartsWith("operation_not_allowed:", StringComparison.Ordinal)) return "operation_not_allowed";
        if (value.StartsWith("invalid_arguments:", StringComparison.Ordinal)) return "invalid_arguments";
        return "operation_failed";
    }

    private static string Bound(string? value, int max)
    {
        var text = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
        return text.Length <= max ? text : text.Substring(0, max);
    }

    private sealed class StaleIdentityException : InvalidOperationException
    {
        internal StaleIdentityException(string message) : base(message) { }
    }
}
