using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

const string resourceName = "QS3D.BundleZip";
var destinationRoot = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "Autodesk",
    "ApplicationPlugins");
var destination = Path.Combine(destinationRoot, "QS3D.bundle");

if (args.Any(arg => string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine("QS3D AutoCAD Setup");
    Console.WriteLine("  no arguments   Install or upgrade QS3D.bundle");
    Console.WriteLine("  --uninstall    Remove QS3D.bundle");
    return 0;
}

EnsureAutoCadClosed();

if (args.Any(arg => string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase)))
{
    if (Directory.Exists(destination))
    {
        Directory.Delete(destination, recursive: true);
        Console.WriteLine($"Removed {destination}");
    }
    else
    {
        Console.WriteLine("QS3D AutoCAD is not installed in the all-users ApplicationPlugins folder.");
    }

    return 0;
}

var assembly = Assembly.GetExecutingAssembly();
using var bundleStream = assembly.GetManifestResourceStream(resourceName)
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

    if (Directory.Exists(destination))
    {
        Directory.Move(destination, backup);
        destinationMovedToBackup = true;
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
        }

        throw;
    }

    if (destinationMovedToBackup && Directory.Exists(backup))
    {
        TryDeleteDirectory(backup);
        destinationMovedToBackup = false;
    }

    Console.WriteLine($"Installed QS3D AutoCAD to {destination}");
    Console.WriteLine("Start AutoCAD and execute QS3D.");
    return 0;
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
