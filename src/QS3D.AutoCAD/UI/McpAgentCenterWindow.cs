using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using GroupBox = System.Windows.Controls.GroupBox;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using ScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility;
using QS3D.AutoCAD.Infrastructure.Mcp;

namespace QS3D.AutoCAD.UI;

/// <summary>Local-user control surface for MCP state. Sensitive actions require explicit local UI interaction.</summary>
internal sealed class McpAgentCenterWindow : Window
{
    private static McpAgentCenterWindow? _instance;
    private readonly TextBox _status = new();
    private readonly TextBlock _lastAction = new();
    private readonly ComboBox _profile = new();
    private readonly TextBox _provider = new() { Text = "external-provider" };
    private readonly TextBox _publicEndpoint = new();
    private readonly TextBox _cloudflareHostname = new();
    private readonly TextBox _oauthClientId = new();
    private readonly TextBox _oauthRedirect = new() { Text = "http://127.0.0.1:8766/callback" };

    internal static void ShowSingleton()
    {
        if (_instance is null)
        {
            _instance = new McpAgentCenterWindow();
            _instance.Closed += (_, _) => _instance = null;
        }
        _instance.Show();
        _instance.Activate();
        _instance.RefreshStatus();
    }

    private McpAgentCenterWindow()
    {
        Title = "QS3D AutoCAD — MCP Agent Center";
        Width = 760;
        Height = 820;
        MinWidth = 640;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = BuildContent();
    }
    private UIElement BuildContent()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root = new StackPanel { Margin = new Thickness(16) };
        scroll.Content = root;

        root.Children.Add(new TextBlock
        {
            Text = "MCP Agent Center",
            FontSize = 22,
            FontWeight = FontWeights.Bold
        });
        root.Children.Add(new TextBlock
        {
            Text = "AutoCAD local control surface. Public transport, foreground control and OAuth consent stay OFF until an explicit local action.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 12)
        });

        root.Children.Add(BuildConnectionGroup());
        root.Children.Add(BuildAgentControlGroup());
        root.Children.Add(BuildOAuthGroup());
        root.Children.Add(BuildRecoveryGroup());
        root.Children.Add(BuildDiagnosticsGroup());
        root.Children.Add(BuildFooter());
        return scroll;
    }
    private GroupBox BuildConnectionGroup()
    {
        foreach (var profile in McpTransportProfileRegistry.All) _profile.Items.Add(profile.Id);
        _profile.SelectedItem = McpEcosystemSettings.SelectedProfile;

        var panel = VerticalPanel();
        panel.Children.Add(Label("Transport profile (selection never starts a tunnel)"));
        panel.Children.Add(_profile);
        panel.Children.Add(Button("Select profile", () =>
        {
            var id = _profile.SelectedItem as string ?? "local-embedded";
            McpTransportProfileRegistry.SelectFromLocalUser(id);
            return "Selected profile: " + id;
        }));
        panel.Children.Add(Label("External provider name"));
        panel.Children.Add(_provider);
        panel.Children.Add(Label("Cloudflare hostname (optional, non-secret)"));
        panel.Children.Add(_cloudflareHostname);
        panel.Children.Add(Label("Provider-managed public HTTPS /mcp endpoint"));
        panel.Children.Add(_publicEndpoint);
        panel.Children.Add(HorizontalButtons(
            Button("Activate endpoint", ActivatePublicEndpoint),
            Button("Stop public endpoint", () => { McpSecureTunnelRuntime.StopFromLocalUser(); return "Public endpoint stopped."; })));
        return Group("Connection", panel);
    }
    private string ActivatePublicEndpoint()
    {
        var profileId = _profile.SelectedItem as string ?? McpEcosystemSettings.SelectedProfile;
        McpTransportProfileRegistry.SelectFromLocalUser(profileId);
        var endpoint = _publicEndpoint.Text.Trim();
        var provider = _provider.Text.Trim();
        if (string.Equals(profileId, "cloudflare", StringComparison.OrdinalIgnoreCase))
        {
            var hostname = _cloudflareHostname.Text.Trim();
            if (hostname.Length > 0)
            {
                McpCloudflareOnboarding.ConfigureFromLocalUser(string.Empty, "cloudflare", hostname);
                endpoint = McpCloudflareOnboarding.ConfiguredPublicMcpUrl;
                _publicEndpoint.Text = endpoint;
                provider = "cloudflare";
                _provider.Text = provider;
            }
        }
        McpSecureTunnelRuntime.StartFromLocalUser(provider, endpoint);
        return "Public endpoint activated from local Agent Center only.";
    }

    private GroupBox BuildAgentControlGroup()
    {
        var panel = VerticalPanel();
        panel.Children.Add(HorizontalButtons(
            Button("Enable foreground control", () =>
            {
                McpDesktopAutomationRuntime.EnableForegroundFromLocalUser("agent-center-local-user");
                return "Foreground desktop control enabled for this process session.";
            }),
            Button("Disable foreground control", () =>
            {
                McpDesktopAutomationRuntime.DisableForeground("agent-center-local-user");
                return "Foreground desktop control disabled.";
            })));
        return Group("Agent control", panel);
    }
    private GroupBox BuildOAuthGroup()
    {
        var panel = VerticalPanel();
        panel.Children.Add(Label("OAuth client id"));
        panel.Children.Add(_oauthClientId);
        panel.Children.Add(Label("Loopback redirect URI"));
        panel.Children.Add(_oauthRedirect);
        panel.Children.Add(HorizontalButtons(
            Button("Grant local OAuth consent", () =>
            {
                McpOAuthConsentStore.GrantFromLocalUser(
                    _oauthClientId.Text,
                    _oauthRedirect.Text,
                    McpOAuthAuthorizationServer.RequiredScope);
                return "OAuth consent granted for this process session.";
            }),
            Button("Revoke OAuth consent", () =>
            {
                McpOAuthConsentStore.RevokeFromLocalUser(
                    _oauthClientId.Text,
                    _oauthRedirect.Text,
                    McpOAuthAuthorizationServer.RequiredScope);
                return "OAuth consent revoked.";
            })));
        return Group("OAuth consent", panel);
    }
    private GroupBox BuildRecoveryGroup()
    {
        var panel = VerticalPanel();
        panel.Children.Add(HorizontalButtons(
            Button("Create recovery copy", McpProjectRecovery.CaptureNowFromLocalUser),
            Button("Recover latest to new copy", McpProjectRecovery.RecoverLatestToCopyFromLocalUser)));
        panel.Children.Add(new TextBlock
        {
            Text = "Recovery always writes a new copy and never overwrites the active DWG.",
            TextWrapping = TextWrapping.Wrap
        });
        return Group("Recovery", panel);
    }

    private GroupBox BuildDiagnosticsGroup()
    {
        _status.IsReadOnly = true;
        _status.AcceptsReturn = true;
        _status.TextWrapping = TextWrapping.NoWrap;
        _status.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _status.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        _status.MinHeight = 220;

        var panel = VerticalPanel();
        panel.Children.Add(HorizontalButtons(
            Button("Refresh status", () => { RefreshStatus(); return "Status refreshed."; }),
            Button("Safe self-repair", McpSelfHealingRepair.TryAutomaticSafeRepair)));
        panel.Children.Add(HorizontalButtons(
            Button("Restart local MCP listener", () => McpSelfHealingRepair.RunExplicitLocalRepair("restart-local-mcp-listener")),
            Button("Clear OAuth consent", () => McpSelfHealingRepair.RunExplicitLocalRepair("clear-process-oauth-consent"))));
        panel.Children.Add(_status);
        return Group("Advanced diagnostics", panel);
    }
    private UIElement BuildFooter()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(HorizontalButtons(
            Button("Finish first-run setup", () =>
            {
                McpFirstRunExperience.MarkCompletedFromLocalUser();
                return "First-run setup marked complete.";
            }),
            Button("Close", () => { Close(); return "Agent Center closed."; })));
        _lastAction.TextWrapping = TextWrapping.Wrap;
        _lastAction.Margin = new Thickness(0, 8, 0, 0);
        panel.Children.Add(_lastAction);
        return panel;
    }

    private void RefreshStatus()
    {
        var snapshot = new Dictionary<string, object?>
        {
            ["host"] = "AutoCAD",
            ["ecosystemSettings"] = McpJson.Parse(McpEcosystemSettings.SnapshotJson()),
            ["firstRun"] = McpJson.Parse(McpFirstRunExperience.SnapshotJson()),
            ["transportProfiles"] = McpJson.Parse(McpTransportProfileRegistry.SnapshotJson()),
            ["embeddedServer"] = McpJson.Parse(McpEmbeddedServerV2.StatusJson()),
            ["tunnel"] = McpJson.Parse(McpSecureTunnelRuntime.SnapshotJson()),
            ["desktopConsent"] = McpJson.Parse(McpDesktopControlSession.SnapshotJson()),
            ["oauthConsent"] = McpJson.Parse(McpOAuthConsentStore.SnapshotJson()),
            ["recovery"] = McpJson.Parse(McpProjectRecovery.SnapshotJson()),
            ["codeHost"] = McpJson.Parse(Qs3dCodeHostLocalIpcServer.StatusJson()),
            ["diagnostics"] = McpJson.Parse(McpDiagnosticHub.TailJson(20))
        };
        _status.Text = McpJson.Serialize(snapshot);
    }
    private Button Button(string text, Func<string> action)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(0, 4, 8, 4),
            Padding = new Thickness(10, 6, 10, 6),
            MinWidth = 140
        };
        button.Click += (_, _) => RunLocalAction(action);
        return button;
    }

    private void RunLocalAction(Func<string> action)
    {
        try
        {
            var message = action();
            _lastAction.Text = message;
            RefreshStatus();
        }
        catch (Exception ex)
        {
            _lastAction.Text = "Action failed: " + ex.Message;
            McpDiagnosticHub.Log("mcp-agent-center", "local action failed: " + ex.Message);
            RefreshStatus();
        }
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        Margin = new Thickness(0, 6, 0, 2),
        FontWeight = FontWeights.SemiBold
    };
    private static StackPanel VerticalPanel() => new() { Margin = new Thickness(6) };

    private static StackPanel HorizontalButtons(params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    private static GroupBox Group(string header, UIElement content) => new()
    {
        Header = header,
        Content = content,
        Margin = new Thickness(0, 0, 0, 10),
        Padding = new Thickness(6)
    };
}
