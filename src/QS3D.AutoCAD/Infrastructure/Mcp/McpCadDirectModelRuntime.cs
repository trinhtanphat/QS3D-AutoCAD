using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal static class McpCadDirectModelRuntime
{
    private static readonly HashSet<string> Tools = new(StringComparer.Ordinal)
    {
        "cad_create_box", "cad_extrude", "cad_boolean_union", "cad_boolean_subtract", "cad_boolean_intersect", "cad_save", "cad_save_as"
    };

    internal static bool IsTool(string? tool) => Tools.Contains(tool ?? string.Empty)
        || McpCadLayerStateRuntime.IsTool(tool) || McpCadViewStatusRuntime.IsTool(tool);

    internal static bool RequiresMutation(string? tool)
    {
        if (McpCadLayerStateRuntime.IsTool(tool)) return McpCadLayerStateRuntime.RequiresMutation(tool);
        if (McpCadViewStatusRuntime.IsTool(tool)) return McpCadViewStatusRuntime.RequiresMutation(tool);
        return Tools.Contains(tool ?? string.Empty);
    }

    internal static IEnumerable<McpToolDescriptor> ToolDescriptors() => McpToolRegistry.ToolDescriptors().Where(item => IsTool(item.Name));

    internal static string Call(string tool, string body)
    {
        if (McpCadLayerStateRuntime.IsTool(tool)) return McpCadLayerStateRuntime.Call(tool, body);
        if (McpCadViewStatusRuntime.IsTool(tool)) return McpCadViewStatusRuntime.Call(tool, body);
        return McpDiagnosticHub.InvokeInCadContext(() => tool switch
        {
            "cad_create_box" => CreateBox(body),
            "cad_extrude" => Extrude(body),
            "cad_boolean_union" => Boolean(body, BooleanOperationType.BoolUnite, "union"),
            "cad_boolean_subtract" => Boolean(body, BooleanOperationType.BoolSubtract, "subtract"),
            "cad_boolean_intersect" => Boolean(body, BooleanOperationType.BoolIntersect, "intersect"),
            "cad_save" => Save(),
            "cad_save_as" => SaveAs(body),
            _ => throw new InvalidOperationException("Unknown direct AutoCAD MCP tool: " + tool)
        });
    }

    private static string CreateBox(string body)
    {
        var x = McpTopLevelJson.RequireDouble(body, "x");
        var y = McpTopLevelJson.RequireDouble(body, "y");
        var z = McpTopLevelJson.RequireDouble(body, "z");
        var length = Positive(body, "length");
        var width = Positive(body, "width");
        var height = Positive(body, "height");
        var document = RequireDocument();
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var solid = new Solid3d();
        solid.CreateBox(length, width, height);
        solid.TransformBy(Matrix3d.Displacement(new Vector3d(x - length / 2, y - width / 2, z - height / 2)));
        var id = Append(transaction, document.Database, solid, body);
        transaction.Commit();
        return Created(id, "Solid3d");
    }

    private static string Extrude(string body)
    {
        var handle = McpTopLevelJson.ExtractString(body, "handle");
        var height = McpTopLevelJson.RequireDouble(body, "height");
        if (Math.Abs(height) < 1e-9) throw new InvalidOperationException("height must be non-zero.");
        var document = RequireDocument();
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var sourceId = McpCadAgentRuntime.ResolveObjectId(document.Database, handle);
        var source = transaction.GetObject(sourceId, OpenMode.ForRead, false) as Curve
            ?? throw new InvalidOperationException("cad_extrude requires a live Curve handle.");
        using var cloned = (Curve)source.Clone();
        var curves = new DBObjectCollection { cloned };
        var regions = Region.CreateFromCurves(curves);
        if (regions.Count != 1) throw new InvalidOperationException("Curve must produce exactly one closed planar region.");
        using var region = regions[0] as Region ?? throw new InvalidOperationException("Region creation failed.");
        var solid = new Solid3d();
        solid.Extrude(region, height, 0);
        var id = Append(transaction, document.Database, solid, body);
        for (var index = 1; index < regions.Count; index++) regions[index].Dispose();
        transaction.Commit();
        return Created(id, "Solid3d");
    }

    private static string Boolean(string body, BooleanOperationType operation, string action)
    {
        var targetHandle = McpTopLevelJson.ExtractString(body, "targetHandle");
        var toolHandle = McpTopLevelJson.ExtractString(body, "toolHandle");
        if (string.Equals(targetHandle, toolHandle, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("targetHandle and toolHandle must be different.");
        var document = RequireDocument();
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var target = transaction.GetObject(McpCadAgentRuntime.ResolveObjectId(document.Database, targetHandle), OpenMode.ForWrite, false) as Solid3d
            ?? throw new InvalidOperationException("targetHandle must identify a Solid3d.");
        var tool = transaction.GetObject(McpCadAgentRuntime.ResolveObjectId(document.Database, toolHandle), OpenMode.ForWrite, false) as Solid3d
            ?? throw new InvalidOperationException("toolHandle must identify a Solid3d.");
        target.BooleanOperation(operation, tool);
        tool.Erase();
        transaction.Commit();
        return McpJson.Serialize(new Dictionary<string, object?> { ["updated"] = true, ["operation"] = action, ["targetHandle"] = target.Handle.ToString(), ["toolConsumed"] = true });
    }

    private static string Save()
    {
        var document = RequireDocument();
        var path = Path.GetFullPath(document.Name);
        if (!Path.IsPathRooted(path) || !string.Equals(Path.GetExtension(path), ".dwg", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Active drawing must already have a rooted .dwg path before cad_save.");
        using var documentLock = document.LockDocument();
        document.Database.SaveAs(path, DwgVersion.Current);
        return McpJson.Serialize(new Dictionary<string, object?> { ["saved"] = true, ["path"] = path });
    }

    private static string SaveAs(string body)
    {
        var requested = McpTopLevelJson.ExtractString(body, "path");
        if (string.IsNullOrWhiteSpace(requested) || requested.Length > 1024) throw new InvalidOperationException("path is required and must be bounded.");
        var path = Path.GetFullPath(requested);
        if (!Path.IsPathRooted(path) || !string.Equals(Path.GetExtension(path), ".dwg", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("cad_save_as requires an absolute .dwg path.");
        var overwrite = McpTopLevelJson.ExtractBoolean(body, "overwrite");
        if (File.Exists(path) && !overwrite) throw new InvalidOperationException("Destination exists; overwrite=true is required.");
        RejectProtectedPath(path);
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Destination directory is unavailable.");
        if (!Directory.Exists(directory)) throw new InvalidOperationException("Destination directory does not exist.");
        var document = RequireDocument();
        using var documentLock = document.LockDocument();
        document.Database.SaveAs(path, true, DwgVersion.Current, document.Database.SecurityParameters);
        return McpJson.Serialize(new Dictionary<string, object?> { ["saved"] = true, ["path"] = path, ["renamed"] = true });
    }

    private static void RejectProtectedPath(string path)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Saving into a protected system/program directory is blocked.");
        }
    }

    private static double Positive(string body, string property)
    {
        var value = McpTopLevelJson.RequireDouble(body, property);
        if (value <= 0 || value > 1e9) throw new InvalidOperationException(property + " must be positive and bounded.");
        return value;
    }

    private static ObjectId Append(Transaction transaction, Database database, Entity entity, string body)
    {
        var layerName = McpTopLevelJson.ExtractString(body, "layer").Trim();
        var layerId = database.Clayer;
        if (layerName.Length > 0) layerId = QS3D.AutoCAD.Infrastructure.AutoCadDrawing.EnsureLayer(transaction, database, layerName, 7);
        return QS3D.AutoCAD.Infrastructure.AutoCadDrawing.Append(transaction, database, entity, layerId);
    }

    private static string Created(ObjectId id, string type) => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["created"] = true, ["handle"] = id.Handle.ToString(), ["type"] = type
    });

    private static Document RequireDocument() => AcApplication.DocumentManager.MdiActiveDocument
        ?? throw new InvalidOperationException("No active AutoCAD document is available.");
}