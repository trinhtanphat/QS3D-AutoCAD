using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

/// <summary>Authenticated same-user local named-pipe bridge for QS3D Code clients.</summary>
internal static class Qs3dCodeHostLocalIpcServer
{
    internal const string ContractVersion = "1";
    private const int MaxMessageBytes = 1024 * 1024;
    private const int IoTimeoutMilliseconds = 5000;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly object Sync = new();
    private static readonly string HostInstanceId = Guid.NewGuid().ToString("N");
    private static Thread? _thread;
    private static NamedPipeServerStream? _currentPipe;
    private static volatile bool _stopping;
    private static string _sessionId = string.Empty;
    private static string _pipeName = string.Empty;
    private static string _capability = string.Empty;
    private static string _stateFilePath = string.Empty;
    private static string _hostVersion = string.Empty;
#if NET8_0_OR_GREATER
    private const bool CurrentUserOnlyPipeOption = true;
#else
    private const bool CurrentUserOnlyPipeOption = false;
#endif

    internal static void Start()
    {
        lock (Sync)
        {
            if (_thread is not null) return;
            using var process = Process.GetCurrentProcess();
            _sessionId = Guid.NewGuid().ToString("N");
            _capability = RandomToken(32);
            _pipeName = CreatePipeName(process.Id, _sessionId);
            _stateFilePath = CreateStateFilePath(process.Id);
            _hostVersion = CaptureHostVersion();
            _stopping = false;
            WriteStateFile(process.Id);
            _thread = new Thread(ServeLoop) { IsBackground = true, Name = "QS3D AutoCAD CodeHost IPC" };
            _thread.Start();
        }
        McpDiagnosticHub.Log("qs3d-code-host", "local named-pipe bridge started; contractVersion=" + ContractVersion);
    }
    internal static void Stop()
    {
        Thread? thread;
        string stateFile;
        lock (Sync)
        {
            if (_thread is null) return;
            _stopping = true;
            thread = _thread;
            _thread = null;
            stateFile = _stateFilePath;
            try { _currentPipe?.Dispose(); } catch { }
            _currentPipe = null;
            _stateFilePath = string.Empty;
        }
        if (thread is not null && thread != Thread.CurrentThread)
            try { thread.Join(IoTimeoutMilliseconds + 1000); } catch { }
        TryDelete(stateFile);
        lock (Sync)
        {
            _sessionId = string.Empty;
            _pipeName = string.Empty;
            _capability = string.Empty;
            _hostVersion = string.Empty;
        }
        McpDiagnosticHub.Log("qs3d-code-host", "local named-pipe bridge stopped");
    }

    internal static Qs3dCodeHostIdentity GetHostIdentity()
    {
        lock (Sync)
        {
            if (_thread is null || _stopping)
                throw new InvalidOperationException("host_unavailable: QS3D Code local IPC is not running.");
            return new Qs3dCodeHostIdentity(HostInstanceId, _sessionId,
                Process.GetCurrentProcess().Id, _hostVersion);
        }
    }

    internal static string StatusJson()
    {
        lock (Sync)
        {
            return McpJson.Serialize(new Dictionary<string, object?>
            {
                ["running"] = _thread is not null && !_stopping,
                ["contractVersion"] = ContractVersion,
                ["pipeName"] = _pipeName,
                ["stateFilePath"] = _stateFilePath,
                ["capabilityExposedInDiagnostics"] = false,
                ["localOnly"] = true,
                ["currentUserOnlyPipeOption"] = CurrentUserOnlyPipeOption,
                ["maxMessageBytes"] = MaxMessageBytes,
                ["ioTimeoutMilliseconds"] = IoTimeoutMilliseconds
            });
        }
    }
    private static PipeOptions PipeServerOptions()
    {
#if NET8_0_OR_GREATER
        return PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;
#else
        return PipeOptions.Asynchronous;
#endif
    }

    private static void ServeLoop()
    {
        while (!_stopping)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                string pipeName;
                lock (Sync) pipeName = _pipeName;
                if (pipeName.Length == 0) return;
                pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeServerOptions());
                lock (Sync)
                {
                    if (_stopping) { pipe.Dispose(); return; }
                    _currentPipe = pipe;
                }
                pipe.WaitForConnection();
                if (_stopping) return;
                HandleClient(pipe);
            }
            catch (ObjectDisposedException) when (_stopping) { return; }
            catch (IOException) when (_stopping) { return; }
            catch (Exception ex)
            {
                if (_stopping) return;
                McpDiagnosticHub.Log("qs3d-code-host", "local IPC client failure: " + ex.Message);
            }
            finally
            {
                lock (Sync) if (ReferenceEquals(_currentPipe, pipe)) _currentPipe = null;
                try { pipe?.Dispose(); } catch { }
            }
        }
    }

    private static void HandleClient(NamedPipeServerStream pipe)
    {
        var json = ReadMessage(pipe);
        var capability = McpTopLevelJson.ExtractString(json, "capability");
        string expected;
        lock (Sync) expected = _capability;
        if (!FixedEquals(capability, expected))
            throw new InvalidOperationException("unauthorized: invalid local IPC capability.");

        var request = new Qs3dCodeHostRequest
        {
            ContractVersion = McpTopLevelJson.ExtractString(json, "contractVersion"),
            RequestId = McpTopLevelJson.ExtractString(json, "requestId"),
            Operation = McpTopLevelJson.ExtractString(json, "operation"),
            PermissionClass = McpTopLevelJson.ExtractString(json, "permissionClass"),
            HostId = McpTopLevelJson.ExtractString(json, "hostId"),
            SessionId = McpTopLevelJson.ExtractString(json, "sessionId"),
            DrawingId = McpTopLevelJson.ExtractString(json, "drawingId"),
            ArgumentsJson = McpTopLevelJson.ExtractString(json, "argumentsJson"),
            ConfirmMutation = McpTopLevelJson.ExtractBoolean(json, "confirmMutation"),
            ActionId = McpTopLevelJson.ExtractString(json, "actionId"),
            WriterToken = McpTopLevelJson.ExtractString(json, "writerToken")
        };
        WriteMessage(pipe, SerializeResult(Qs3dCodeHostBridge.Execute(request)));
    }
    private static string SerializeResult(Qs3dCodeHostResult result)
    {
        var host = result.Host;
        var active = result.ActiveDrawing;
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["ok"] = result.Ok,
            ["contractVersion"] = ContractVersion,
            ["requestId"] = result.RequestId,
            ["operation"] = result.Operation,
            ["host"] = host is null ? null : new Dictionary<string, object?>
            {
                ["hostId"] = host.HostId, ["sessionId"] = host.SessionId,
                ["processId"] = host.ProcessId, ["hostVersion"] = host.HostVersion
            },
            ["activeDrawing"] = active is null ? null : new Dictionary<string, object?>
            {
                ["drawingId"] = active.DrawingId, ["displayName"] = active.DisplayName, ["isNamed"] = active.IsNamed
            },
            ["payloadJson"] = Bound(result.PayloadJson, 262144),
            ["errorCode"] = result.ErrorCode,
            ["message"] = Bound(result.Message, 2048)
        });
    }

    private static string ReadMessage(Stream stream)
    {
        var lengthBytes = new byte[4];
        ReadExact(stream, lengthBytes, 0, lengthBytes.Length);
        var length = BitConverter.ToInt32(lengthBytes, 0);
        if (length <= 0 || length > MaxMessageBytes)
            throw new InvalidOperationException("invalid_message: local IPC payload length exceeds bounds.");
        var payload = new byte[length];
        ReadExact(stream, payload, 0, payload.Length);
        try { return StrictUtf8.GetString(payload); }
        catch (DecoderFallbackException) { throw new InvalidOperationException("invalid_message: local IPC payload is not valid UTF-8."); }
    }

    private static void WriteMessage(Stream stream, string json)
    {
        var payload = Encoding.UTF8.GetBytes(json ?? string.Empty);
        if (payload.Length == 0 || payload.Length > MaxMessageBytes)
            throw new InvalidOperationException("response_too_large: local IPC response exceeds bounds.");
        var length = BitConverter.GetBytes(payload.Length);
        WriteAll(stream, length, 0, length.Length);
        WriteAll(stream, payload, 0, payload.Length);
        stream.Flush();
    }
    private static void ReadExact(Stream stream, byte[] buffer, int offset, int count)
    {
        var readTotal = 0;
        while (readTotal < count)
        {
            var async = stream.BeginRead(buffer, offset + readTotal, count - readTotal, null, null);
            using var wait = async.AsyncWaitHandle;
            if (!wait.WaitOne(IoTimeoutMilliseconds))
                throw new TimeoutException("Local IPC read timed out.");
            var read = stream.EndRead(async);
            if (read <= 0) throw new EndOfStreamException("Local IPC client disconnected before the frame completed.");
            readTotal += read;
        }
    }

    private static void WriteAll(Stream stream, byte[] buffer, int offset, int count)
    {
        var async = stream.BeginWrite(buffer, offset, count, null, null);
        using var wait = async.AsyncWaitHandle;
        if (!wait.WaitOne(IoTimeoutMilliseconds))
            throw new TimeoutException("Local IPC write timed out.");
        stream.EndWrite(async);
    }

    private static string CaptureHostVersion()
    {
        try
        {
            return McpDiagnosticHub.InvokeInCadContext(() =>
                Convert.ToString(AcApplication.GetSystemVariable("ACADVER"), CultureInfo.InvariantCulture) ?? string.Empty);
        }
        catch { return string.Empty; }
    }

    private static string CreatePipeName(int processId, string sessionId)
    {
        var seed = (Environment.UserName ?? string.Empty) + "\n" + processId.ToString(CultureInfo.InvariantCulture) + "\n" + sessionId;
        return "qs3d-autocad-code-" + Hash(seed).Substring(0, 24);
    }

    private static string CreateStateFilePath(int processId)
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("No user-local application-data directory is available.");
        return Path.Combine(root, "QS3D", "CodeHost", "AutoCAD", "host-" + processId.ToString(CultureInfo.InvariantCulture) + ".json");
    }
    private static void WriteStateFile(int processId)
    {
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("CodeHost state directory is unavailable.");
        Directory.CreateDirectory(directory);
        var temp = _stateFilePath + ".tmp-" + Guid.NewGuid().ToString("N");
        var json = McpJson.Serialize(new Dictionary<string, object?>
        {
            ["contractVersion"] = ContractVersion,
            ["hostId"] = HostInstanceId,
            ["sessionId"] = _sessionId,
            ["processId"] = processId,
            ["hostVersion"] = _hostVersion,
            ["pipeName"] = _pipeName,
            ["capability"] = _capability,
            ["localOnly"] = true
        });
        File.WriteAllText(temp, json, new UTF8Encoding(false));
        try
        {
            if (File.Exists(_stateFilePath)) File.Delete(_stateFilePath);
            File.Move(temp, _stateFilePath);
        }
        finally { TryDelete(temp); }
    }

    private static string RandomToken(int bytes)
    {
        var buffer = new byte[bytes];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(buffer);
        return Convert.ToBase64String(buffer).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Hash(string value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var item in bytes) builder.Append(item.ToString("x2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }
    private static bool FixedEquals(string? left, string? right)
    {
        var a = Encoding.UTF8.GetBytes(left ?? string.Empty);
        var b = Encoding.UTF8.GetBytes(right ?? string.Empty);
        var difference = a.Length ^ b.Length;
        var count = Math.Max(a.Length, b.Length);
        for (var index = 0; index < count; index++)
            difference |= (index < a.Length ? a[index] : (byte)0) ^ (index < b.Length ? b[index] : (byte)0);
        return difference == 0;
    }

    private static string Bound(string? value, int max)
    {
        var text = value ?? string.Empty;
        return text.Length <= max ? text : text.Substring(0, max);
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
