namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Produces recovery plans before any repair action. Plans are observational and never mutate a DWG.</summary>
internal static class McpProjectRecovery
{
    internal static string BuildPlanJson()
    {
        var steps = new List<Dictionary<string, object?>>();
        if (!McpEmbeddedServer.IsRunning)
            steps.Add(Step("restart-local-listener", automaticSafe: true, "Restart only the loopback MCP listener when no CAD mutation or emergency-stop is active."));
        if (McpCadAgentRuntime.AutomationStopped)
            steps.Add(Step("require-local-resume", automaticSafe: false, "Automation is emergency-stopped; only an explicit canonical resume mutation may continue."));
        if (!McpDesktopControlSession.IsEnabled && McpBackgroundHostRuntime.IsForegroundPolicyEnabled)
            steps.Add(Step("disable-stale-foreground-policy", automaticSafe: true, "Foreground policy has no active local consent and should be returned to background_only."));
        if (steps.Count == 0) steps.Add(Step("no-repair-required", automaticSafe: true, "Local MCP runtime is healthy enough for continued diagnostics."));

        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["generatedUtc"] = DateTime.UtcNow.ToString("o"),
            ["planBeforeMutation"] = true,
            ["drawingMutationPlanned"] = false,
            ["arbitraryDialogDismissalPlanned"] = false,
            ["steps"] = steps,
            ["provenance"] = McpJson.Parse(McpRuntimeBuildProvenance.SnapshotJson())
        });
    }

    private static Dictionary<string, object?> Step(string action, bool automaticSafe, string reason) => new()
    {
        ["action"] = action, ["automaticSafe"] = automaticSafe, ["reason"] = reason
    };
}
