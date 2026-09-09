using QS3D.AutoCAD.UI;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal static class McpQs3dDomainRuntime
{
    private static readonly object Sync = new();
    private static bool _available = true;
    private static string _lastError = string.Empty;

    internal static bool IsTool(string? tool) => tool is "qs3d_status" or "qs3d_domain_status" or "qs3d_run_command";
    internal static bool RequiresMutation(string? tool) => tool is "qs3d_run_command";

    internal static void ResetForServerStart()
    {
        lock (Sync) { _available = true; _lastError = string.Empty; }
    }

    internal static string BuildStatusJson(bool compatibilityAlias)
    {
        return McpDiagnosticHub.InvokeInCadContext(() =>
        {
            var document = AcApplication.DocumentManager.MdiActiveDocument;
            bool available;
            string lastError;
            lock (Sync) { available = _available; lastError = _lastError; }
            return McpJson.Serialize(new Dictionary<string, object?>
            {
                ["lane"] = "qs3d_domain",
                ["available"] = available,
                ["activeDocument"] = document?.Name,
                ["commandCount"] = Qs3dCommandCatalog.All.Count,
                ["lastError"] = string.IsNullOrWhiteSpace(lastError) ? null : lastError,
                ["deprecatedAlias"] = compatibilityAlias
            });
        });
    }

    internal static string Call(string tool, string body)
    {
        if (tool != "qs3d_run_command") throw new InvalidOperationException("Unknown QS3D AutoCAD MCP mutation tool: " + tool);
        try
        {
            var result = McpDiagnosticHub.InvokeInCadContext(() => RunCommand(body));
            lock (Sync) { _available = true; _lastError = string.Empty; }
            return result;
        }
        catch (Exception ex)
        {
            lock (Sync) { _available = false; _lastError = ex.Message.Length <= 1024 ? ex.Message : ex.Message.Substring(0, 1024); }
            throw;
        }
    }

    private static string RunCommand(string body)
    {
        var command = McpTopLevelJson.ExtractString(body, "command").Trim().ToUpperInvariant();
        if (command.Length == 0 || command.Length > 80 || command.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_')))
            throw new InvalidOperationException("QS3D command name is invalid.");
        var descriptor = Qs3dCommandCatalog.All.FirstOrDefault(item => string.Equals(item.Command, command, StringComparison.OrdinalIgnoreCase));
        if (descriptor is null) throw new InvalidOperationException("QS3D command is not registered in the AutoCAD command catalog: " + command);
        var document = AcApplication.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active AutoCAD document is available.");
        document.SendStringToExecute(command + " ", true, false, false);
        McpCadAgentRuntime.AuditDomainMutation("qs3d_run_command", "command=" + command);
        return McpJson.Serialize(new Dictionary<string, object?> { ["accepted"] = true, ["command"] = command, ["queued"] = true });
    }
}