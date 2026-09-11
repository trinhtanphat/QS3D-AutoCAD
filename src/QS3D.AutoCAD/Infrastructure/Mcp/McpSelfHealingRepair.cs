namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Fail-closed repair planner for safe MCP runtime state; never mutates a DWG.</summary>
internal static class McpSelfHealingRepair
{
    internal static string BuildPlanJson()
    {
        var actions = new List<string>();
        if (!McpEmbeddedServer.IsRunning && !McpCadAgentRuntime.AutomationStopped && !McpCadMutationCoordinator.IsMutationActive)
            actions.Add("restart-local-mcp-listener");
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["planBeforeMutation"] = true,
            ["drawingMutation"] = false,
            ["automaticDialogDismiss"] = false,
            ["publicTransportActivation"] = false,
            ["actions"] = actions,
            ["recovery"] = McpJson.Parse(McpProjectRecovery.BuildPlanJson())
        });
    }

    internal static string TryAutomaticSafeRepair()
    {
        var plan = McpJson.Parse(BuildPlanJson());
        var repaired = false;
        if (!McpEmbeddedServer.IsRunning
            && !McpCadAgentRuntime.AutomationStopped
            && !McpCadMutationCoordinator.IsMutationActive)
        {
            repaired = McpEmbeddedServerV2.EnsureStarted();
            if (repaired) McpDiagnosticHub.Log("self-healing", "safe local MCP listener restarted; drawingMutation=false");
        }
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["planned"] = plan,
            ["repaired"] = repaired,
            ["drawingMutation"] = false,
            ["requiresConfirmationForDrawingMutation"] = true
        });
    }

    internal static string RunExplicitLocalRepair(string action)
    {
        var normalized = (action ?? string.Empty).Trim().ToLowerInvariant();
        string result;
        switch (normalized)
        {
            case "restart-local-mcp-listener":
                if (McpCadAgentRuntime.AutomationStopped || McpCadMutationCoordinator.IsMutationActive)
                    throw new InvalidOperationException("Local MCP listener repair is blocked by emergency stop or active mutation.");
                McpEmbeddedServer.Stop();
                McpEmbeddedServerV2.EnsureStarted();
                result = "local MCP listener restarted";
                break;
            case "clear-process-oauth-consent":
                McpOAuthConsentStore.RevokeAll("explicit-local-repair");
                result = "process OAuth consent cleared";
                break;
            default:
                throw new InvalidOperationException("Unsupported explicit local repair action.");
        }
        McpDiagnosticHub.Log("self-healing-repair", "explicit local repair action=" + normalized + "; result=" + result);
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["action"] = normalized,
            ["result"] = result,
            ["drawingMutation"] = false,
            ["audited"] = true
        });
    }
}
