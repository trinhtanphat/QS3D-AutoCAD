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

var temporaryRoot = Path.Combine(Path.GetTempPath(), "QS3D-Setup-" + Guid.NewGuid().ToString("N"));
var archivePath = Path.Combine(temporaryRoot, "QS3D.bundle.zip");
var extractRoot = Path.Combine(temporaryRoot, "extract");
Directory.CreateDirectory(temporaryRoot);

try
{
    await using (var archiveFile = File.Create(archivePath))
    {
        await bundleStream.CopyToAsync(archiveFile);
    }

    ZipFile.ExtractToDirectory(archivePath, extractRoot);
    var bundleSource = ResolveBundleSource(extractRoot);

    Directory.CreateDirectory(destinationRoot);
    if (Directory.Exists(destination))
    {
        Directory.Delete(destination, recursive: true);
    }

    CopyDirectory(bundleSource, destination);
    Console.WriteLine($"Installed QS3D AutoCAD to {destination}");
    Console.WriteLine("Restart AutoCAD if it is already running, then execute QS3D.");
    return 0;
}
finally
{
    try
    {
        if (Directory.Exists(temporaryRoot))
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
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
