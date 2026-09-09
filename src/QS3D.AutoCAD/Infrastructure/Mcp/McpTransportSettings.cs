using System.Globalization;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal static class McpTransportSettings
{
    internal const bool ExternalTransportEnabledByDefault = false;
    internal const int DefaultPort = 8765;
    internal const string LoopbackHost = "127.0.0.1";

    internal static int Port
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("QS3D_AUTOCAD_MCP_PORT");
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) && port is >= 1024 and <= 65535
                ? port : DefaultPort;
        }
    }

    internal static bool ExternalTransportEnabled
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("QS3D_AUTOCAD_MCP_EXTERNAL_TRANSPORT");
            return string.Equals(value, "1", StringComparison.Ordinal) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static string LocalEndpoint => "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture) + "/mcp";
}