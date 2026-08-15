using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;

const string ResourceName = "QS3D.BundleZip";

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    SetupOptions options;
    try
    {
        options = ParseOptions(args);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        PrintHelp();
        return 2;
    }

    if (options.Help)
    {
        PrintHelp();
        return 0;
    }

    var logPath = CreateLogPath();
    WriteLog(logPath, $"QS3D AutoCAD Setup started. Arguments: {string.Join(' ', args.Select(QuoteForLog))}");

    try
    {
        if (RequiresElevation(options) && !IsAdministrator())
        {
            if (options.ElevatedChild)
            {
                throw new InvalidOperationException("Administrator elevation was requested but the elevated process still does not have administrator rights.");
            }

            WriteLog(logPath, "All-users installation requires elevation; requesting Windows UAC consent.");
            return RelaunchElevated(args, logPath);
        }

        if (!options.SkipAutoCadCheck)
        {
            EnsureAutoCadClosed();
        }

        var destinationRoot = ResolveDestinationRoot(options);
        var destination = Path.Combine(destinationRoot, "QS3D.bundle");
        WriteLog(logPath, $"Selected install root: {destinationRoot}");

        if (options.Uninstall)
        {
            RemoveInstalledBundle(destination, logPath);

            if (options.IsAllUsers && options.InstallRoot is null)
            {
                var legacyDestination = GetLegacyProgramDataDestination();
                if (!PathsEqual(destination, legacyDestination))
                {
                    RemoveInstalledBundle(legacyDestination, logPath);
                }
            }

            var uninstallMessage = $"QS3D AutoCAD was removed successfully.\n\nLog: {logPath}";
            Console.WriteLine(uninstallMessage);
            ShowMessage(options, "QS3D AutoCAD Setup", uninstallMessage, isError: false);
            return 0;
        }

        await InstallAsync(destinationRoot, destination, logPath);

        string? warning = null;
        if (options.IsAllUsers && options.InstallRoot is null)
        {
            var legacyDestination = GetLegacyProgramDataDestination();
            if (!PathsEqual(destination, legacyDestination) && Directory.Exists(legacyDestination))
            {
                try
                {
                    Directory.Delete(legacyDestination, recursive: true);
                    WriteLog(logPath, $"Removed legacy installation: {legacyDestination}");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    warning = $"The new installation succeeded, but an older copy could not be removed from {legacyDestination}. Close Autodesk products and remove that old QS3D.bundle folder manually.";
                    WriteLog(logPath, $"WARNING: {warning} {ex}");
                }
            }
        }

        var successMessage = $"QS3D AutoCAD installed successfully to:\n{destination}\n\nStart AutoCAD and run QS3D.\n\nLog: {logPath}";
        if (warning is not null)
        {
            successMessage += $"\n\nWarning: {warning}";
        }

        Console.WriteLine(successMessage);
        ShowMessage(options, "QS3D AutoCAD Setup", successMessage, isError: false);
        return 0;
    }
    catch (Exception ex)
    {
        WriteLog(logPath, $"ERROR: {ex}");
        var errorMessage = $"QS3D AutoCAD installation failed.\n\n{ex.Message}\n\nDetails were written to:\n{logPath}";
        Console.Error.WriteLine(errorMessage);
        ShowMessage(options, "QS3D AutoCAD Setup - Error", errorMessage, isError: true);
        return 1;
    }
}

static SetupOptions ParseOptions(string[] args)
{
    var uninstall = false;
    var user = false;
    var allUsers = false;
    var quiet = false;
    var help = false;
    var skipAutoCadCheck = false;
    var elevatedChild = false;
    string? installRoot = null;

    for (var index = 0; index < args.Length; index++)
    {
        var arg = args[index];
        switch (arg.ToLowerInvariant())
        {
            case "--uninstall":
                uninstall = true;
                break;
            case "--user":
                user = true;
                break;
            case "--all-users":
                allUsers = true;
                break;
            case "--quiet":
                quiet = true;
                break;
            case "--help":
            case "-h":
            case "/?":
                help = true;
                break;
            case "--skip-autocad-check":
                skipAutoCadCheck = true;
                break;
            case "--elevated-child":
                elevatedChild = true;
                break;
            case "--install-root":
                if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                {
                    throw new ArgumentException("--install-root requires a directory path.");
                }

                installRoot = Path.GetFullPath(args[index]);
                break;
            default:
                throw new ArgumentException($"Unknown setup argument: {arg}");
        }
    }

    if (user && allUsers)
    {
        throw new ArgumentException("Choose either --user or --all-users, not both.");
    }

    if (installRoot is not null && (user || allUsers))
    {
        throw new ArgumentException("--install-root is an isolated test/development target and cannot be combined with --user or --all-users.");
    }

    if (skipAutoCadCheck && installRoot is null)
    {
        throw new ArgumentException("--skip-autocad-check is allowed only with --install-root for isolated test/development installs.");
    }

    return new SetupOptions(
        Uninstall: uninstall,
        User: user,
        AllUsers: allUsers,
        Quiet: quiet,
        Help: help,
        SkipAutoCadCheck: skipAutoCadCheck,
        ElevatedChild: elevatedChild,
        InstallRoot: installRoot);
}

static void PrintHelp()
{
    Console.WriteLine("QS3D AutoCAD Setup");
    Console.WriteLine("  no arguments                 Install/upgrade for all users (recommended Program Files location)");
    Console.WriteLine("  --user                       Install/upgrade for the current user under %APPDATA%");
    Console.WriteLine("  --all-users                  Explicit all-users install/upgrade");
    Console.WriteLine("  --uninstall                  Remove the selected installation");
    Console.WriteLine("  --quiet                      Suppress graphical result dialogs");
    Console.WriteLine("  --help                       Show this help");
    Console.WriteLine();
    Console.WriteLine("Development/CI only:");
    Console.WriteLine("  --install-root <directory>   Use an isolated ApplicationPlugins root");
    Console.WriteLine("  --skip-autocad-check         Allowed only together with --install-root");
}

static bool RequiresElevation(SetupOptions options)
{
    return options.InstallRoot is null && !options.User;
}

static string ResolveDestinationRoot(SetupOptions options)
{
    if (options.InstallRoot is not null)
    {
        return options.InstallRoot;
    }

    var basePath = options.User
        ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

    if (string.IsNullOrWhiteSpace(basePath))
    {
        throw new InvalidOperationException("Windows did not provide the requested application data directory.");
    }

    return Path.Combine(basePath, "Autodesk", "ApplicationPlugins");
}

static string GetLegacyProgramDataDestination()
{
    var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    return Path.Combine(commonData, "Autodesk", "ApplicationPlugins", "QS3D.bundle");
}

static int RelaunchElevated(string[] originalArgs, string logPath)
{
    var executable = Environment.ProcessPath;
    if (string.IsNullOrWhiteSpace(executable))
    {
        throw new InvalidOperationException("Unable to resolve the running Setup.exe path for elevation.");
    }

    var startInfo = new ProcessStartInfo
    {
        FileName = executable,
        UseShellExecute = true,
        Verb = "runas"
    };

    foreach (var arg in originalArgs)
    {
        startInfo.ArgumentList.Add(arg);
    }

    startInfo.ArgumentList.Add("--elevated-child");

    try
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows did not start the elevated QS3D Setup process.");
        process.WaitForExit();
        WriteLog(logPath, $"Elevated child exited with code {process.ExitCode}.");
        return process.ExitCode;
    }
    catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
    {
        throw new InvalidOperationException("Administrator permission was cancelled. QS3D was not installed.", ex);
    }
}

static bool IsAdministrator()
{
    using var identity = WindowsIdentity.GetCurrent();
    var principal = new WindowsPrincipal(identity);
    return principal.IsInRole(WindowsBuiltInRole.Administrator);
}

static async Task InstallAsync(string destinationRoot, string destination, string logPath)
{
    var assembly = Assembly.GetExecutingAssembly();
    await using var bundleStream = assembly.GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException("Embedded QS3D bundle payload is missing.");

    var operationId = Guid.NewGuid().ToString("N");
    var temporaryRoot = Path.Combine(Path.GetTempPath(), "QS3D-Setup-" + operationId);
    var archivePath = Path.Combine(temporaryRoot, "QS3D.bundle.zip");
    var extractRoot = Path.Combine(temporaryRoot, "extract");
    Directory.CreateDirectory(temporaryRoot);
    Directory.CreateDirectory(destinationRoot);

    var candidate = Path.Combine(destinationRoot, $".QS3D.bundle.install-{operationId}");
    var backup = Path.Combine(destinationRoot, $".QS3D.bundle.backup-{operationId}");
    var destinationMovedToBackup = false;

    try
    {
        await using (var archiveFile = File.Create(archivePath))
        {
            await bundleStream.CopyToAsync(archiveFile);
        }

        ZipFile.ExtractToDirectory(archivePath, extractRoot);
        var bundleSource = ResolveBundleSource(extractRoot);

        CopyDirectory(bundleSource, candidate);
        ValidateBundle(candidate);
        WriteLog(logPath, $"Validated embedded bundle candidate at {candidate}.");

        if (Directory.Exists(destination))
        {
            Directory.Move(destination, backup);
            destinationMovedToBackup = true;
            WriteLog(logPath, $"Moved existing installation to transaction backup {backup}.");
        }

        try
        {
            Directory.Move(candidate, destination);
        }
        catch
        {
            if (destinationMovedToBackup && !Directory.Exists(destination) && Directory.Exists(backup))
            {
                Directory.Move(backup, destination);
                destinationMovedToBackup = false;
                WriteLog(logPath, "Restored the previous installation after replacement failure.");
            }

            throw;
        }

        ValidateBundle(destination);
        WriteLog(logPath, $"Installed and revalidated bundle at {destination}.");

        if (destinationMovedToBackup && Directory.Exists(backup))
        {
            TryDeleteDirectory(backup);
            destinationMovedToBackup = false;
        }
    }
    catch
    {
        if (Directory.Exists(candidate))
        {
            TryDeleteDirectory(candidate);
        }

        if (destinationMovedToBackup && !Directory.Exists(destination) && Directory.Exists(backup))
        {
            Directory.Move(backup, destination);
            destinationMovedToBackup = false;
            WriteLog(logPath, "Restored the previous installation during setup rollback.");
        }

        throw;
    }
    finally
    {
        TryDeleteDirectory(temporaryRoot);
        if (!destinationMovedToBackup && Directory.Exists(backup))
        {
            TryDeleteDirectory(backup);
        }
    }
}

static void RemoveInstalledBundle(string destination, string logPath)
{
    if (!Directory.Exists(destination))
    {
        WriteLog(logPath, $"No installation found at {destination}.");
        return;
    }

    Directory.Delete(destination, recursive: true);
    WriteLog(logPath, $"Removed {destination}.");
}

static void EnsureAutoCadClosed()
{
    if (Process.GetProcessesByName("acad").Length > 0)
    {
        throw new InvalidOperationException("AutoCAD is running. Close all AutoCAD instances before installing, upgrading, or uninstalling QS3D.");
    }
}

static string ResolveBundleSource(string extractRoot)
{
    var direct = Path.Combine(extractRoot, "QS3D.bundle");
    if (Directory.Exists(direct))
    {
        return direct;
    }

    if (File.Exists(Path.Combine(extractRoot, "PackageContents.xml")))
    {
        return extractRoot;
    }

    var nested = Directory
        .EnumerateDirectories(extractRoot, "QS3D.bundle", SearchOption.AllDirectories)
        .FirstOrDefault();
    return nested ?? throw new InvalidDataException("The embedded package does not contain QS3D.bundle.");
}

static void ValidateBundle(string bundle)
{
    var required = new[]
    {
        "PackageContents.xml",
        Path.Combine("Contents", "2025-2026", "QS3D.AutoCAD.dll"),
        Path.Combine("Contents", "2025-2026", "QS3D.Core.dll"),
        Path.Combine("Contents", "2027", "QS3D.AutoCAD.dll"),
        Path.Combine("Contents", "2027", "QS3D.Core.dll")
    };

    foreach (var relativePath in required)
    {
        var path = Path.Combine(bundle, relativePath);
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"The embedded QS3D bundle is incomplete: missing {relativePath}.");
        }
    }
}

static void CopyDirectory(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (var file in Directory.EnumerateFiles(source))
    {
        File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
    }

    foreach (var directory in Directory.EnumerateDirectories(source))
    {
        CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}

static string CreateLogPath()
{
    var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    if (string.IsNullOrWhiteSpace(basePath))
    {
        basePath = Path.GetTempPath();
    }

    var logDirectory = Path.Combine(basePath, "QS3D", "Logs");
    Directory.CreateDirectory(logDirectory);
    return Path.Combine(logDirectory, $"setup-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");
}

static void WriteLog(string logPath, string message)
{
    try
    {
        File.AppendAllText(logPath, $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
    }
    catch
    {
        // Logging must never hide the original installer result.
    }
}

static void ShowMessage(SetupOptions options, string title, string message, bool isError)
{
    if (options.Quiet || !OperatingSystem.IsWindows())
    {
        return;
    }

    const uint ok = 0x00000000;
    const uint iconError = 0x00000010;
    const uint iconInformation = 0x00000040;
    _ = NativeMethods.MessageBoxW(IntPtr.Zero, message, title, ok | (isError ? iconError : iconInformation));
}

static bool PathsEqual(string left, string right)
{
    return string.Equals(
        Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
        Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
        StringComparison.OrdinalIgnoreCase);
}

static string QuoteForLog(string value)
{
    return value.Contains(' ') ? $"\"{value}\"" : value;
}

static void TryDeleteDirectory(string path)
{
    try
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
}

internal sealed record SetupOptions(
    bool Uninstall,
    bool User,
    bool AllUsers,
    bool Quiet,
    bool Help,
    bool SkipAutoCadCheck,
    bool ElevatedChild,
    string? InstallRoot)
{
    public bool IsAllUsers => !User && InstallRoot is null;
}

internal static class NativeMethods
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
