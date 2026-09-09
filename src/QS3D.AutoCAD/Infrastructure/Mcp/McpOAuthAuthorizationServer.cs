namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Local authorization facade; remote requests are denied until a local-user consent code exists.</summary>
internal static class McpOAuthAuthorizationServer
{
    internal const string Contract = "oauth-local-consent-v1";

    internal static string AuthorizeFromLocalUser(string clientId, string redirectUri, IEnumerable<string> scopes)
    {
        var code = McpOAuthConsentStore.IssueOneTimeCodeFromLocalUser(clientId, redirectUri, scopes);
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["authorized"] = true,
            ["delivery"] = "loopback-authorization-code",
            ["code"] = code,
            ["expiresSeconds"] = 120,
            ["contractVersion"] = Contract
        });
    }

    internal static string Exchange(string code, string clientId, string redirectUri) =>
        McpOAuthConsentStore.ConsumeOneTimeCode(code, clientId, redirectUri);

    internal static string StatusJson() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["running"] = false,
        ["networkListener"] = false,
        ["authorization"] = "deny-by-default",
        ["consent"] = McpJson.Parse(McpOAuthConsentStore.SnapshotJson()),
        ["diagnosticsReturnTokens"] = false
    });
}
