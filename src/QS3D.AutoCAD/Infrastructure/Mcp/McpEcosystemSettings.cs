using System.Text;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Non-secret MCP ecosystem preferences. Provider credentials never live here.</summary>
internal static class McpEcosystemSettings
{
    private static readonly object Sync = new();
    private static string _selectedProfile = "local-embedded";
    private static bool _firstRunCompleted;

    internal static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QS3D", "MCP", "ecosystem-settings.txt");

    internal static string SelectedProfile
    {
        get { lock (Sync) return _selectedProfile; }
    }

    internal static bool FirstRunCompleted
    {
        get { lock (Sync) return _firstRunCompleted; }
    }

    internal static void Load()
    {
        lock (Sync)
        {
            _selectedProfile = "local-embedded";
            _firstRunCompleted = false;
            try
            {
                if (!File.Exists(SettingsPath) || new FileInfo(SettingsPath).Length > 4096) return;
                foreach (var line in File.ReadAllLines(SettingsPath))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2) continue;
                    if (string.Equals(parts[0], "selectedProfile", StringComparison.Ordinal))
                        _selectedProfile = McpTransportProfileRegistry.NormalizeProfileId(parts[1]);
                    else if (string.Equals(parts[0], "firstRunCompleted", StringComparison.Ordinal))
                        _firstRunCompleted = string.Equals(parts[1], "true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                McpDiagnosticHub.Log("mcp-settings", "load failed: " + ex.Message);
            }
        }
    }

    internal static void SelectProfileFromLocalUser(string profileId)
    {
        lock (Sync)
        {
            _selectedProfile = McpTransportProfileRegistry.NormalizeProfileId(profileId);
            SaveLocked();
        }
    }

    internal static void MarkFirstRunCompletedFromLocalUser()
    {
        lock (Sync)
        {
            _firstRunCompleted = true;
            SaveLocked();
        }
    }

    internal static string SnapshotJson()
    {
        lock (Sync)
        {
            return McpJson.Serialize(new Dictionary<string, object?>
            {
                ["selectedProfile"] = _selectedProfile,
                ["firstRunCompleted"] = _firstRunCompleted,
                ["containsSecrets"] = false,
                ["publicTransportAutoEnabled"] = false,
                ["foregroundControlAutoEnabled"] = false
            });
        }
    }

    private static void SaveLocked()
    {
        var directory = Path.GetDirectoryName(SettingsPath) ?? throw new InvalidOperationException("QS3D settings directory is unavailable.");
        Directory.CreateDirectory(directory);
        var text = new StringBuilder()
            .Append("selectedProfile=").Append(_selectedProfile).AppendLine()
            .Append("firstRunCompleted=").Append(_firstRunCompleted ? "true" : "false").AppendLine()
            .ToString();
        File.WriteAllText(SettingsPath, text, Encoding.UTF8);
    }
}
