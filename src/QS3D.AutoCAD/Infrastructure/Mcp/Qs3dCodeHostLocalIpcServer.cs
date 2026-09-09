using System.IO.Pipes;
using System.Text;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Machine-local named-pipe server for the bounded QS3D code-host contract.</summary>
internal static class Qs3dCodeHostLocalIpcServer
{
    internal const string PipeName = "QS3D.AutoCAD.MCP.CodeHost.v1";
    private const int MaxMessageCharacters = 1024 * 1024;
    private static readonly object Sync = new();
    private static Thread? _thread;
    private static volatile bool _stopping;

    internal static bool IsRunning
    {
        get { lock (Sync) return _thread is not null && !_stopping; }
    }

    internal static void Start()
    {
        lock (Sync)
        {
            if (_thread is not null && !_stopping) return;
            _stopping = false;
            _thread = new Thread(ServeLoop) { IsBackground = true, Name = "QS3D AutoCAD local code-host IPC" };
            _thread.Start();
        }
        McpDiagnosticHub.Log("codehost-ipc", "local named-pipe server started; contractVersion=" + Qs3dCodeHostRequest.CurrentContractVersion);
    }

    internal static void Stop()
    {
        Thread? thread;
        lock (Sync) { _stopping = true; thread = _thread; _thread = null; }
        TryWakeServer();
        if (thread is not null && thread != Thread.CurrentThread)
        {
            try { thread.Join(1000); } catch { }
        }
        McpDiagnosticHub.Log("codehost-ipc", "local named-pipe server stopped");
    }

    internal static string StatusJson() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["running"] = IsRunning,
        ["transport"] = "named-pipe-local-only",
        ["pipeName"] = PipeName,
        ["contractVersion"] = Qs3dCodeHostRequest.CurrentContractVersion,
        ["requiresAuthorization"] = true,
        ["arbitraryShellExecution"] = false
    });

    private static void ServeLoop()
    {
        while (!_stopping)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None, 8192, 8192);
                pipe.WaitForConnection();
                if (_stopping) break;
                using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, leaveOpen: true);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
                var request = ReadBoundedLine(reader);
                var response = Qs3dCodeHostBridge.Dispatch(request);
                writer.WriteLine(response);
            }
            catch (Exception ex)
            {
                if (!_stopping) McpDiagnosticHub.Log("codehost-ipc", "serve error: " + ex.Message);
            }
        }
    }

    private static string ReadBoundedLine(StreamReader reader)
    {
        var builder = new StringBuilder();
        while (true)
        {
            var value = reader.Read();
            if (value < 0 || value == '\n') break;
            if (value == '\r') continue;
            if (builder.Length >= MaxMessageCharacters) throw new InvalidOperationException("Local IPC message exceeds 1 MiB character limit.");
            builder.Append((char)value);
        }
        if (builder.Length == 0) throw new InvalidOperationException("Local IPC request is empty.");
        return builder.ToString();
    }

    private static void TryWakeServer()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(100);
            using var writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true };
            writer.WriteLine("{}");
        }
        catch { }
    }
}
