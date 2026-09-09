namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Self-healing is intentionally limited to safe local runtime lifecycle state; it never edits a DWG or clicks dialogs.</summary>
internal static class McpSelfHealingRepair
{
    internal static string TrySafeAutomaticRepair()
    {
        if (McpCadAgentRuntime.AutomationStopped || McpCadMutationCoordinator.IsMutationActive)
            return Result(false, "blocked-by-cad-safety-gate");

        var actions = new List<string>();
        if (McpBackgroundHostRuntime.IsForegroundPolicyEnabled && !McpDesktopControlSession.IsEnabled)
        {
            McpBackgroundHostRuntime.DisableForegroundFromLocalUser();
            actions.Add("returned-interaction-policy-to-background_only");
        }
        if (!McpEmbeddedServer.IsRunning && !McpTransportSettings.ExternalTransportEnabled)
        {
            McpEmbeddedServerV2.EnsureStarted();
            actions.Add("restarted-loopback-listener");
        }

        var repaired = actions.Count > 0;
        McpDiagnosticHub.Log("self-healing", repaired ? string.Join(";", actions) : "no safe automatic repair required");
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["repaired"] = repaired,
            ["actions"] = actions,
            ["dwgMutation"] = false,
            ["dialogDismissal"] = false,
            ["publicTransportPublished"] = false,
            ["auditRecorded"] = true
        });
    }

    internal static string RepairFromExplicitLocalUser(string action)
    {
        McpDesktopControlSession.RequireLocalConsent("self-healing-explicit-repair");
        action = (action ?? string.Empty).Trim().ToLowerInvariant();
        if (action == "restart-local-listener")
        {
            if (McpCadAgentRuntime.AutomationStopped || McpCadMutationCoordinator.IsMutationActive)
                throw new InvalidOperationException("Local listener repair is blocked while CAD mutation/emergency-stop is active.");
            McpEmbeddedServerV2.EnsureStarted();
            McpDiagnosticHub.Log("self-healing", "explicit local restart-local-listener");
            return Result(true, "restart-local-listener");
        }
        if (action == "background-only")
        {
            McpBackgroundHostRuntime.DisableForegroundFromLocalUser();
            McpDiagnosticHub.Log("self-healing", "explicit local background-only");
            return Result(true, "background-only");
        }
        throw new InvalidOperationException("Unsupported safe repair action. Drawing mutation and arbitrary dialog dismissal are intentionally unavailable.");
    }

    private static string Result(bool repaired, string action) => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["repaired"] = repaired, ["action"] = action, ["dwgMutation"] = false,
        ["dialogDismissal"] = false, ["publicTransportPublished"] = false
    });
}
