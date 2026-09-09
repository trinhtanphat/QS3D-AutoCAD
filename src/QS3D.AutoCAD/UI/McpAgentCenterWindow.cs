using System.Windows;
using System.Windows.Controls;
using QS3D.AutoCAD.Infrastructure.Mcp;

namespace QS3D.AutoCAD.UI;

/// <summary>Thin local-user MCP operations surface. Service/runtime logic stays in Infrastructure/Mcp.</summary>
internal sealed class McpAgentCenterWindow : Window
{
    private readonly TextBox _status = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        MinHeight = 360,
        FontFamily = new System.Windows.Media.FontFamily("Consolas")
    };

    internal McpAgentCenterWindow()
    {
        Title = "QS3D · AutoCAD MCP Agent Center";
        Width = 880;
        Height = 720;
        MinWidth = 680;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = BuildContent();
        Loaded += (_, __) => RefreshStatus();
    }

    private UIElement BuildContent()
    {
        var root = new DockPanel { Margin = new Thickness(18) };
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new TextBlock { Text = "QS3D · AutoCAD MCP Agent Center", FontSize = 22, FontWeight = FontWeights.Bold });
        header.Children.Add(new TextBlock
        {
            Text = "Local MCP is preferred. Public/tunnel transports and foreground desktop control are never enabled automatically.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        actions.Children.Add(Button("Refresh", (_, __) => RefreshStatus()));
        actions.Children.Add(Button("Complete local first-run", (_, __) => RunLocal(() => McpFirstRunExperience.CompleteFromLocalUser())));
        actions.Children.Add(Button("Enable Foreground Control", (_, __) => RunLocal(() =>
        {
            McpDesktopControlSession.EnableFromLocalUser("agent-center-foreground");
            McpBackgroundHostRuntime.EnableForegroundFromLocalUser();
        })));
        actions.Children.Add(Button("Background only", (_, __) => RunLocal(() =>
        {
            McpBackgroundHostRuntime.DisableForegroundFromLocalUser();
            McpDesktopControlSession.Disable("agent-center-background-only");
        })));
        actions.Children.Add(Button("Select local transport", (_, __) => RunLocal(() => McpTransportProfileRegistry.SelectFromLocalUser("local-embedded"))));
        actions.Children.Add(Button("Select Cloudflare profile", (_, __) => RunLocal(() => McpTransportProfileRegistry.SelectFromLocalUser("cloudflare"))));
        actions.Children.Add(Button("Safe runtime repair", (_, __) => RunLocal(() => McpSelfHealingRepair.TrySafeAutomaticRepair())));
        DockPanel.SetDock(actions, Dock.Top);
        root.Children.Add(actions);

        root.Children.Add(_status);
        return root;
    }

    private static Button Button(string text, RoutedEventHandler handler)
    {
        var button = new Button { Content = text, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 8, 8), MinWidth = 130 };
        button.Click += handler;
        return button;
    }

    private void RunLocal(Action action)
    {
        try { action(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "QS3D MCP", MessageBoxButton.OK, MessageBoxImage.Warning); }
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        var sections = new[]
        {
            "FIRST RUN\r\n" + McpFirstRunExperience.SnapshotJson(),
            "MCP SERVER\r\n" + McpEmbeddedServerV2.StatusJson(),
            "TRANSPORT PROFILES\r\n" + McpTransportProfileRegistry.SnapshotJson(),
            "INTERACTION POLICY\r\n" + McpBackgroundHostRuntime.Call("autocad_interaction_policy_get", "{}"),
            "DESKTOP CONSENT\r\n" + McpDesktopControlSession.SnapshotJson(),
            "WATCHDOG\r\n" + McpRuntimeWatchdog.SnapshotJson(),
            "CLOUDFLARE READINESS\r\n" + McpCloudflareOnboarding.SnapshotJson(),
            "SECURE TUNNEL\r\n" + McpSecureTunnelRuntime.SnapshotJson(),
            "OAUTH\r\n" + McpOAuthAuthorizationServer.StatusJson(),
            "POPUPS\r\n" + McpPopupObserver.SnapshotJson(),
            "RECOVERY PLAN\r\n" + McpProjectRecovery.BuildPlanJson(),
            "CODE HOST IPC\r\n" + Qs3dCodeHostLocalIpcServer.StatusJson(),
            "PROVENANCE\r\n" + McpRuntimeBuildProvenance.SnapshotJson()
        };
        _status.Text = string.Join("\r\n\r\n", sections);
    }
}
