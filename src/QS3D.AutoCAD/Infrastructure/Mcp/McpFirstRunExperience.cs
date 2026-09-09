namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Idempotent first-run state. Initialization never enables public transport or foreground control.</summary>
internal static class McpFirstRunExperience
{
    private static int _initialized;

    internal static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1) return;
        McpDiagnosticHub.Log("mcp-first-run", McpEcosystemSettings.FirstRunCompleted ? "previously completed" : "local onboarding pending");
    }

    internal static void CompleteFromLocalUser()
    {
        EnsureInitialized();
        McpEcosystemSettings.MarkFirstRunCompletedFromLocalUser();
        McpDiagnosticHub.Log("mcp-first-run", "completed by explicit local user action");
    }

    internal static string SnapshotJson() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["initialized"] = Volatile.Read(ref _initialized) == 1,
        ["completed"] = McpEcosystemSettings.FirstRunCompleted,
        ["idempotent"] = true,
        ["publicTransportAutoEnabled"] = false,
        ["publicEndpointAutoPublished"] = false,
        ["foregroundControlAutoEnabled"] = false,
        ["providerCredentialsStored"] = false
    });
}
