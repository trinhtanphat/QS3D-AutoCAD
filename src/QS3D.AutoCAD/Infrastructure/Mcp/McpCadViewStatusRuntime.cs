using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal static class McpCadViewStatusRuntime
{
    private static readonly HashSet<string> Tools = new(StringComparer.Ordinal)
    {
        "cad_view_zoom_extents", "cad_view_fit_entities", "cad_view_set", "agent_status", "cad_command_state"
    };

    internal static bool IsTool(string? tool) => Tools.Contains(tool ?? string.Empty);
    internal static bool RequiresMutation(string? tool) => tool is "cad_view_zoom_extents" or "cad_view_fit_entities" or "cad_view_set";
    internal static IEnumerable<McpToolDescriptor> ToolDescriptors() => McpToolRegistry.ToolDescriptors().Where(item => IsTool(item.Name));
    internal static string Call(string tool, string body) => McpDiagnosticHub.InvokeInCadContext(() => CallInCadContext(tool, body));

    internal static string CallInCadContext(string tool, string body) => tool switch
    {
        "cad_view_zoom_extents" => ZoomExtents(body),
        "cad_view_fit_entities" => FitEntities(body),
        "cad_view_set" => SetView(body),
        "agent_status" => AgentStatus(),
        "cad_command_state" => CommandState(),
        _ => throw new InvalidOperationException("Unknown AutoCAD MCP view/status tool: " + tool)
    };

    private static string ZoomExtents(string body)
    {
        var padding = RequireRange(McpTopLevelJson.OptionalDouble(body, "padding", 1.08), 1, 2, "padding");
        var document = RequireDocument();
        var min = document.Database.Extmin;
        var max = document.Database.Extmax;
        return ApplyExtents(document, new Extents3d(min, max), padding, "drawing_extents", 0);
    }

    private static string FitEntities(string body)
    {
        var csv = McpTopLevelJson.ExtractString(body, "handlesCsv");
        if (string.IsNullOrWhiteSpace(csv) || csv.Length > 1800) throw new InvalidOperationException("handlesCsv is required and bounded to 1800 characters.");
        var handles = csv.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (handles.Length == 0 || handles.Length > 100) throw new InvalidOperationException("Supply between 1 and 100 entity handles.");
        var padding = RequireRange(McpTopLevelJson.OptionalDouble(body, "padding", 1.12), 1, 2, "padding");
        var document = RequireDocument();
        using var transaction = document.Database.TransactionManager.StartOpenCloseTransaction();
        Extents3d? combined = null;
        var fitted = 0;
        var skipped = new List<string>();
        foreach (var token in handles)
        {
            try
            {
                var id = McpCadAgentRuntime.ResolveObjectId(document.Database, token);
                var entity = transaction.GetObject(id, OpenMode.ForRead, false) as Entity;
                if (entity is null || entity.IsErased) throw new InvalidOperationException("not a live entity");
                var extents = entity.GeometricExtents;
                if (combined is null) combined = extents;
                else { var value = combined.Value; value.AddExtents(extents); combined = value; }
                fitted++;
            }
            catch { skipped.Add(token); }
        }
        if (combined is null) throw new InvalidOperationException("No supplied entity had usable geometric extents.");
        var result = McpJson.ParseObject(ApplyExtents(document, combined.Value, padding, "entities", fitted));
        result["requestedEntityCount"] = handles.Length;
        result["skippedHandles"] = skipped;
        return McpJson.Serialize(result);
    }

    private static string SetView(string body)
    {
        var centerX = McpTopLevelJson.RequireDouble(body, "centerX");
        var centerY = McpTopLevelJson.RequireDouble(body, "centerY");
        var width = RequireRange(McpTopLevelJson.RequireDouble(body, "width"), 1e-6, 1e12, "width");
        var height = RequireRange(McpTopLevelJson.RequireDouble(body, "height"), 1e-6, 1e12, "height");
        var twist = McpTopLevelJson.OptionalDouble(body, "twistRadians", double.NaN);
        var document = RequireDocument();
        using var documentLock = document.LockDocument();
        using var view = document.Editor.GetCurrentView();
        view.CenterPoint = new Point2d(centerX, centerY);
        view.Width = width;
        view.Height = height;
        if (!double.IsNaN(twist))
        {
            if (twist < -Math.PI * 2 || twist > Math.PI * 2) throw new InvalidOperationException("twistRadians must be between -2π and 2π.");
            view.ViewTwist = twist;
        }
        document.Editor.SetCurrentView(view);
        return CurrentViewJson(document, "set");
    }

    private static string ApplyExtents(Document document, Extents3d extents, double padding, string source, int entityCount)
    {
        var min = extents.MinPoint;
        var max = extents.MaxPoint;
        var width = Math.Max(1e-6, (max.X - min.X) * padding);
        var height = Math.Max(1e-6, (max.Y - min.Y) * padding);
        using var documentLock = document.LockDocument();
        using var view = document.Editor.GetCurrentView();
        var aspect = view.Width > 0 && view.Height > 0 ? view.Width / view.Height : 1.0;
        if (width / height > aspect) height = width / Math.Max(aspect, 1e-9);
        else width = height * Math.Max(aspect, 1e-9);
        view.CenterPoint = new Point2d((min.X + max.X) / 2, (min.Y + max.Y) / 2);
        view.Width = width;
        view.Height = height;
        document.Editor.SetCurrentView(view);
        var result = McpJson.ParseObject(CurrentViewJson(document, source));
        if (entityCount > 0) result["entityCount"] = entityCount;
        return McpJson.Serialize(result);
    }

    internal static string CurrentViewJson(Document document, string source)
    {
        using var view = document.Editor.GetCurrentView();
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["source"] = source,
            ["center"] = new Dictionary<string, object?> { ["x"] = view.CenterPoint.X, ["y"] = view.CenterPoint.Y },
            ["width"] = view.Width,
            ["height"] = view.Height,
            ["direction"] = new Dictionary<string, object?> { ["x"] = view.ViewDirection.X, ["y"] = view.ViewDirection.Y, ["z"] = view.ViewDirection.Z },
            ["twistRadians"] = view.ViewTwist
        });
    }

    private static string AgentStatus() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["automationStopped"] = McpCadAgentRuntime.AutomationStopped,
        ["writer"] = McpJson.Parse(McpCadMutationCoordinator.StatusJson()),
        ["updatedUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
    });

    private static string CommandState()
    {
        var active = Convert.ToInt32(AcApplication.GetSystemVariable("CMDACTIVE"), CultureInfo.InvariantCulture);
        var names = Convert.ToString(AcApplication.GetSystemVariable("CMDNAMES"), CultureInfo.InvariantCulture) ?? string.Empty;
        if (names.Length > 512) names = names.Substring(0, 512);
        return McpJson.Serialize(new Dictionary<string, object?> { ["cmdActive"] = active, ["cmdNames"] = names, ["idle"] = active == 0 });
    }

    private static double RequireRange(double value, double minimum, double maximum, string label)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > maximum)
            throw new InvalidOperationException(label + " is outside the allowed range.");
        return value;
    }

    private static Document RequireDocument() => AcApplication.DocumentManager.MdiActiveDocument
        ?? throw new InvalidOperationException("No active AutoCAD document is available.");
}