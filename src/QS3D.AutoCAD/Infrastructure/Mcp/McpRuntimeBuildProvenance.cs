using System.Reflection;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal static class McpRuntimeBuildProvenance
{
    internal const string ContractVersion = "mcp-ecosystem-v1";

    internal static string SnapshotJson()
    {
        var assembly = typeof(McpRuntimeBuildProvenance).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        var buildSha = Environment.GetEnvironmentVariable("QS3D_BUILD_SHA") ?? ExtractSha(informational);
        var buildId = Environment.GetEnvironmentVariable("QS3D_BUILD_ID") ?? string.Empty;
        var buildUtc = Environment.GetEnvironmentVariable("QS3D_BUILD_UTC") ?? string.Empty;
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["host"] = "AutoCAD",
            ["contractVersion"] = ContractVersion,
            ["assemblyVersion"] = assembly.GetName().Version?.ToString() ?? string.Empty,
            ["informationalVersion"] = Bound(informational, 160),
            ["buildSha"] = Bound(buildSha, 64),
            ["buildId"] = Bound(buildId, 96),
            ["buildUtc"] = Bound(buildUtc, 64),
            ["mcpProtocol"] = McpEmbeddedServer.ProtocolVersion
        });
    }

    private static string ExtractSha(string informational)
    {
        var plus = (informational ?? string.Empty).LastIndexOf('+');
        if (plus < 0 || plus + 1 >= informational.Length) return string.Empty;
        return informational.Substring(plus + 1);
    }

    private static string Bound(string value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Substring(0, Math.Min(value.Length, max));
}
