namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Non-secret Cloudflare onboarding state. It never creates resources or stores provider credentials.</summary>
internal static class McpCloudflareOnboarding
{
    internal static string SnapshotJson()
    {
        var accountId = Bound(Environment.GetEnvironmentVariable("QS3D_CLOUDFLARE_ACCOUNT_ID") ?? string.Empty, 64);
        var hostname = Bound(Environment.GetEnvironmentVariable("QS3D_CLOUDFLARE_MCP_HOSTNAME") ?? string.Empty, 253);
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["provider"] = "cloudflare",
            ["configured"] = accountId.Length > 0 && hostname.Length > 0,
            ["accountId"] = accountId,
            ["hostname"] = hostname,
            ["credentialsStoredByQs3d"] = false,
            ["resourceCreationSupported"] = false,
            ["paidServiceAutoActivation"] = false,
            ["requiredManualSteps"] = new[] { "Create/configure provider resources outside QS3D", "Configure provider-owned credentials", "Use an explicit local Agent Center action to request tunnel activation" }
        });
    }

    private static string Bound(string value, int max) => value.Length <= max ? value : value.Substring(0, max);
}
