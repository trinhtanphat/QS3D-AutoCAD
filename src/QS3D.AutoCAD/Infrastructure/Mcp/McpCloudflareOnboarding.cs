using System.Text;
using System.Text.RegularExpressions;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Non-secret Cloudflare readiness/onboarding state. It never creates or purchases resources.</summary>
internal static class McpCloudflareOnboarding
{
    private static readonly object Sync = new();
    private static string _accountId = string.Empty;
    private static string _profileId = string.Empty;
    private static string _publicHostname = string.Empty;
    private static bool _approvedByLocalUser;
    private const string ExplicitLocalAction = "explicitLocalAction";

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QS3D", "MCP", "cloudflare-onboarding.txt");

    internal static string ConfiguredPublicMcpUrl
    {
        get
        {
            lock (Sync)
            {
                if (!_approvedByLocalUser || _publicHostname.Length == 0) return string.Empty;
                return "https://" + _publicHostname + "/mcp";
            }
        }
    }

    internal static void Load()
    {
        lock (Sync)
        {
            _accountId = string.Empty;
            _profileId = string.Empty;
            _publicHostname = string.Empty;
            _approvedByLocalUser = false;
            try
            {
                if (!File.Exists(SettingsPath) || new FileInfo(SettingsPath).Length > 8192) return;
                foreach (var line in File.ReadAllLines(SettingsPath))
                {
                    var pair = line.Split(new[] { '=' }, 2);
                    if (pair.Length != 2) continue;
                    if (pair[0] == "accountId") _accountId = NormalizeIdentifier(pair[1]);
                    else if (pair[0] == "profileId") _profileId = NormalizeIdentifier(pair[1]);
                    else if (pair[0] == "publicHostname") _publicHostname = NormalizeHostname(pair[1]);
                    else if (pair[0] == "approvedByLocalUser") _approvedByLocalUser = string.Equals(pair[1], "true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                McpDiagnosticHub.Log("mcp-cloudflare", "non-secret onboarding load failed: " + ex.Message);
            }
        }
    }

    internal static void ConfigureFromLocalUser(string accountId, string profileId, string publicHostname)
    {
        var account = NormalizeIdentifier(accountId);
        var profile = NormalizeIdentifier(profileId);
        var hostname = NormalizeHostname(publicHostname);
        if (profile.Length == 0) throw new InvalidOperationException("Cloudflare profile id is required.");
        if (hostname.Length == 0) throw new InvalidOperationException("Cloudflare public hostname is invalid.");
        lock (Sync)
        {
            _accountId = account;
            _profileId = profile;
            _publicHostname = hostname;
            _approvedByLocalUser = true;
            SaveLocked();
        }
        McpDiagnosticHub.Log("mcp-cloudflare", "onboarding configured by explicit local action; no resource creation or credential storage");
    }

    internal static void ClearFromLocalUser()
    {
        lock (Sync)
        {
            _accountId = string.Empty;
            _profileId = string.Empty;
            _publicHostname = string.Empty;
            _approvedByLocalUser = false;
            SaveLocked();
        }
    }

    internal static string SnapshotJson()
    {
        lock (Sync)
        {
            return McpJson.Serialize(new Dictionary<string, object?>
            {
                ["provider"] = "cloudflare",
                ["accountConfigured"] = _accountId.Length > 0,
                ["profileId"] = _profileId,
                ["publicHostname"] = _publicHostname,
                ["approvedByLocalUser"] = _approvedByLocalUser,
                ["explicitLocalAction"] = ExplicitLocalAction,
                ["credentialsStoredByQs3d"] = false,
                ["resourceCreationSupported"] = false,
                ["paidServiceAutoActivation"] = false,
                ["publicMcpUrl"] = ConfiguredPublicMcpUrl
            });
        }
    }

    private static string NormalizeIdentifier(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length > 128 || !Regex.IsMatch(text, "^[A-Za-z0-9_.:-]*$", RegexOptions.CultureInvariant)) return string.Empty;
        return text;
    }

    private static string NormalizeHostname(string? value)
    {
        var text = (value ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
        if (text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) text = text.Substring(8);
        if (text.Contains('/') || text.Contains('?') || text.Contains('#') || text.Contains('@')) return string.Empty;
        return text.Length <= 253 && Uri.CheckHostName(text) == UriHostNameType.Dns ? text : string.Empty;
    }

    private static void SaveLocked()
    {
        var directory = Path.GetDirectoryName(SettingsPath) ?? throw new InvalidOperationException("Cloudflare settings directory unavailable.");
        Directory.CreateDirectory(directory);
        var text = new StringBuilder()
            .Append("accountId=").Append(_accountId).AppendLine()
            .Append("profileId=").Append(_profileId).AppendLine()
            .Append("publicHostname=").Append(_publicHostname).AppendLine()
            .Append("approvedByLocalUser=").Append(_approvedByLocalUser ? "true" : "false").AppendLine()
            .ToString();
        File.WriteAllText(SettingsPath, text, new UTF8Encoding(false));
    }
}
