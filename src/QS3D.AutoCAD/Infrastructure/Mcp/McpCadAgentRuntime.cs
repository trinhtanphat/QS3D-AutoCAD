using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal static class McpCadAgentRuntime
{
    private const int DbmodPersistentContentMask = 1 | 4 | 32;
    internal const string Qs3dCommandPattern = "^QS3D[A-Za-z0-9_]*$";
    private static readonly HashSet<string> AllowedCadCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "LINE", "PLINE", "3DPOLY", "CIRCLE", "ARC", "RECTANG", "POLYGON", "ELLIPSE", "SPLINE", "POINT",
        "HATCH", "-HATCH", "BOUNDARY", "REGION", "BOX", "CYLINDER", "SPHERE", "CONE", "WEDGE", "TORUS",
        "EXTRUDE", "PRESSPULL", "REVOLVE", "SWEEP", "LOFT", "UNION", "SUBTRACT", "INTERSECT", "SLICE",
        "MOVE", "COPY", "ROTATE", "SCALE", "MIRROR", "OFFSET", "TRIM", "EXTEND", "FILLET", "CHAMFER",
        "STRETCH", "ARRAY", "ERASE", "EXPLODE", "JOIN", "PEDIT", "MATCHPROP", "CHPROP", "PROPERTIES",
        "LAYER", "-LAYER", "LINETYPE", "-LINETYPE", "COLOR", "STYLE", "-STYLE", "TEXT", "DTEXT", "MTEXT",
        "DIM", "DIMLINEAR", "DIMALIGNED", "DIMANGULAR", "DIMRADIUS", "DIMDIAMETER", "DIMSTYLE", "-DIMSTYLE",
        "LEADER", "MLEADER", "BLOCK", "-BLOCK", "WBLOCK", "INSERT", "-INSERT", "XREF", "-XREF", "IMAGEATTACH",
        "LAYOUT", "-LAYOUT", "MVIEW", "MSPACE", "PSPACE", "PLOT", "-PLOT", "PAGESETUP", "ZOOM", "PAN",
        "REGEN", "REGENALL", "UCS", "PLAN", "VPOINT", "VIEW", "-VIEW", "SELECT", "QSELECT", "ISOLATEOBJECTS",
        "UNISOLATEOBJECTS", "UNDO", "REDO", "QSAVE", "SAVEAS", "PURGE", "-PURGE", "AUDIT", "OVERKILL"
    };
    private static readonly HashSet<string> ReadableSystemVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "CMDACTIVE", "CMDNAMES", "INSUNITS", "CLAYER", "CTAB", "TILEMODE", "DWGNAME", "DWGPREFIX", "CVPORT", "ORTHOMODE", "OSMODE", "COLORTHEME", "DBMOD", "ACADVER"
    };

    private static volatile bool _automationStopped;
    private static int _automationEpoch;

    internal static bool AutomationStopped => _automationStopped;

    internal static void ResetForServerStart()
    {
        Interlocked.Increment(ref _automationEpoch);
        _automationStopped = false;
        McpMutationAckLedger.ResetForServerStart();
        McpCadMutationCoordinator.Reset();
        McpQs3dDomainRuntime.ResetForServerStart();
    }

    internal static string Call(string toolName, string arguments)
    {
        var tool = (toolName ?? string.Empty).Trim();
        var args = string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments;
        var descriptor = McpToolRegistry.Find(tool) ?? throw new InvalidOperationException("Unknown MCP AutoCAD tool: " + tool);
        var executionMode = McpToolCapabilityContract.ResolveExecutionMode(
            McpTopLevelJson.ExtractString(args, "executionMode"), McpTopLevelJson.ExtractString(args, "execution_mode"));
        McpToolCapabilityContract.EnsureAllowed(tool, executionMode, descriptor.RequiresMutation);

        if (tool == "cad_mutation_status") return McpMutationAckLedger.StatusJson(McpTopLevelJson.ExtractString(args, "actionId"));
        if (tool == "cad_audit_tail") return McpDiagnosticHub.TailJson(McpTopLevelJson.OptionalInt(args, "limit", 25, 1, 100));
        if (tool == "cad_agent_stop") return Mutation(args, tool, () => EmergencyStop(), allowStopped: true);
        if (tool == "cad_agent_resume") return Mutation(args, tool, () => ResumeAgent(), allowStopped: true);

        if (descriptor.RequiresMutation)
            return Mutation(args, tool, () => Dispatch(tool, args), allowStopped: false);
        return Dispatch(tool, args);
    }

    private static string Dispatch(string tool, string args)
    {
        return tool switch
        {
            "mcp_status" => BuildMcpStatusJson(),
            "autocad_status" => McpDiagnosticHub.InvokeInCadContext(BuildAutoCadStatusJson),
            "qs3d_status" => McpQs3dDomainRuntime.BuildStatusJson(true),
            "qs3d_domain_status" => McpQs3dDomainRuntime.BuildStatusJson(false),
            "cad_active_document" => McpDiagnosticHub.InvokeInCadContext(BuildActiveDocumentJson),
            "cad_selection" => McpDiagnosticHub.InvokeInCadContext(BuildSelectionJson),
            "cad_database_snapshot" => McpDiagnosticHub.InvokeInCadContext(() => BuildDatabaseSnapshotJson(McpTopLevelJson.OptionalInt(args, "limit", 250, 1, 1000))),
            "cad_entity_inspect" => McpDiagnosticHub.InvokeInCadContext(() => InspectEntity(args)),
            "cad_view_state" => McpDiagnosticHub.InvokeInCadContext(() => McpCadViewStatusRuntime.CurrentViewJson(RequireDocument(), "read")),
            "cad_wait_idle" => WaitUntilIdle(McpTopLevelJson.OptionalInt(args, "timeoutMs", 5000, 100, 7000)),
            "cad_sysvar" => McpDiagnosticHub.InvokeInCadContext(() => ReadSystemVariable(args)),
            "cad_create_line" => McpDiagnosticHub.InvokeInCadContext(() => CreateLine(args)),
            "cad_create_circle" => McpDiagnosticHub.InvokeInCadContext(() => CreateCircle(args)),
            "cad_create_arc" => McpDiagnosticHub.InvokeInCadContext(() => CreateArc(args)),
            "cad_create_polyline" => McpDiagnosticHub.InvokeInCadContext(() => CreatePolyline(args)),
            "cad_create_text" => McpDiagnosticHub.InvokeInCadContext(() => CreateText(args, false)),
            "cad_create_mtext" => McpDiagnosticHub.InvokeInCadContext(() => CreateText(args, true)),
            "cad_entity_transform" => McpDiagnosticHub.InvokeInCadContext(() => TransformEntity(args)),
            "cad_entity_delete" => McpDiagnosticHub.InvokeInCadContext(() => DeleteEntity(args)),
            "cad_entity_set_layer" => McpDiagnosticHub.InvokeInCadContext(() => SetEntityLayer(args)),
            "cad_layer" => McpDiagnosticHub.InvokeInCadContext(() => LayerAction(args)),
            "cad_command_catalog" => CommandCatalogJson(),
            "cad_command_sequence" => McpDiagnosticHub.InvokeInCadContext(() => RunCadCommandSequence(args)),
            "qs3d_run_command" => McpQs3dDomainRuntime.Call(tool, args),
            "cad_ui_click" => McpDesktopAutomationRuntime.Call("desktop_mouse_click", args),
            "cad_ui_type" => McpDesktopAutomationRuntime.Call("desktop_type", args),
            "cad_ui_key" => McpDesktopAutomationRuntime.Call("desktop_key", args),
            "cad_cancel_command" => McpDiagnosticHub.InvokeInCadContext(CancelCurrentCommand),
            _ when McpCadDirectModelRuntime.IsTool(tool) => McpCadDirectModelRuntime.Call(tool, args),
            _ when McpDesktopAutomationRuntime.IsTool(tool) => McpDesktopAutomationRuntime.Call(tool, args),
            _ => throw new InvalidOperationException("Unknown MCP AutoCAD tool: " + tool)
        };
    }

    private static string Mutation(string body, string tool, Func<string> action, bool allowStopped)
    {
        if (!McpTopLevelJson.ExtractBoolean(body, "confirmMutation"))
            throw new InvalidOperationException("confirmMutation=true is required for " + tool + ".");
        if (!allowStopped) EnsureAutomationRunning();
        var reservation = McpMutationAckLedger.ReserveOrReplay(tool, body);
        if (reservation.Replayed) return McpMutationAckLedger.BuildResponse(reservation, true);
        try
        {
            using var writer = McpCadMutationCoordinator.EnterMutation(
                McpTopLevelJson.ExtractString(body, "writerToken"), tool, detail => AuditDomainMutation(tool, detail));
            if (!allowStopped) EnsureAutomationRunning();
            var result = action();
            McpMutationAckLedger.Complete(reservation, result);
            AuditDomainMutation(tool, "actionId=" + reservation.ActionId + "; completed=true");
            return McpMutationAckLedger.BuildResponse(reservation, false);
        }
        catch (Exception ex)
        {
            McpMutationAckLedger.Fail(reservation, ex);
            AuditDomainMutation(tool, "actionId=" + reservation.ActionId + "; failed=" + ex.Message);
            throw;
        }
    }

    internal static void EnsureAutomationRunning()
    {
        if (_automationStopped) throw new InvalidOperationException("MCP AutoCAD automation is emergency-stopped. Use cad_agent_resume with confirmMutation=true.");
    }

    private static string EmergencyStop()
    {
        _automationStopped = true;
        Interlocked.Increment(ref _automationEpoch);
        McpCadMutationCoordinator.Reset();
        McpDiagnosticHub.Log("cad_agent_stop", "automation emergency stop engaged");
        return McpJson.Serialize(new Dictionary<string, object?> { ["stopped"] = true, ["epoch"] = _automationEpoch });
    }

    private static string ResumeAgent()
    {
        _automationStopped = false;
        Interlocked.Increment(ref _automationEpoch);
        McpDiagnosticHub.Log("cad_agent_resume", "automation resumed");
        return McpJson.Serialize(new Dictionary<string, object?> { ["stopped"] = false, ["epoch"] = _automationEpoch });
    }

    private static string BuildMcpStatusJson() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["running"] = McpEmbeddedServer.IsRunning,
        ["protocol"] = McpEmbeddedServer.ProtocolVersion,
        ["server"] = McpEmbeddedServer.ServerName,
        ["endpoint"] = McpTransportSettings.LocalEndpoint,
        ["toolCount"] = McpToolRegistry.ToolDescriptors().Count(),
        ["automationStopped"] = _automationStopped,
        ["externalTransportEnabled"] = McpTransportSettings.ExternalTransportEnabled
    });

    private static string BuildAutoCadStatusJson()
    {
        var document = AcApplication.DocumentManager.MdiActiveDocument;
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["host"] = "AutoCAD",
            ["acadVersion"] = SafeSystemVariable("ACADVER"),
            ["documentOpen"] = document is not null,
            ["document"] = document?.Name,
            ["cmdActive"] = SafeSystemVariable("CMDACTIVE"),
            ["mcp"] = McpJson.Parse(BuildMcpStatusJson())
        });
    }

    private static string BuildActiveDocumentJson()
    {
        var document = RequireDocument();
        var dbmod = Convert.ToInt32(AcApplication.GetSystemVariable("DBMOD"), CultureInfo.InvariantCulture);
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["name"] = document.Name,
            ["path"] = document.Name,
            ["saved"] = (dbmod & DbmodPersistentContentMask) == 0,
            ["modified"] = (dbmod & DbmodPersistentContentMask) != 0,
            ["dbmod"] = dbmod,
            ["currentLayer"] = Convert.ToString(AcApplication.GetSystemVariable("CLAYER"), CultureInfo.InvariantCulture),
            ["layout"] = Convert.ToString(AcApplication.GetSystemVariable("CTAB"), CultureInfo.InvariantCulture)
        });
    }

    private static string BuildSelectionJson()
    {
        var document = RequireDocument();
        var selection = document.Editor.SelectImplied();
        var items = new List<object?>();
        if (selection.Status == PromptStatus.OK && selection.Value is not null)
        {
            using var transaction = document.Database.TransactionManager.StartOpenCloseTransaction();
            foreach (var id in selection.Value.GetObjectIds().Take(500))
            {
                if (id.IsNull || id.IsErased) continue;
                var entity = transaction.GetObject(id, OpenMode.ForRead, false) as Entity;
                if (entity is null) continue;
                items.Add(EntitySummary(entity));
            }
        }
        return McpJson.Serialize(new Dictionary<string, object?> { ["count"] = items.Count, ["entities"] = items });
    }

    private static string BuildDatabaseSnapshotJson(int limit)
    {
        var document = RequireDocument();
        using var transaction = document.Database.TransactionManager.StartOpenCloseTransaction();
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForRead);
        var entities = new List<object?>();
        foreach (ObjectId id in space)
        {
            if (entities.Count >= limit) break;
            var entity = transaction.GetObject(id, OpenMode.ForRead, false) as Entity;
            if (entity is null || entity.IsErased) continue;
            entities.Add(EntitySummary(entity));
        }
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["document"] = document.Name, ["limit"] = limit, ["count"] = entities.Count, ["entities"] = entities
        });
    }

    private static string InspectEntity(string body)
    {
        var document = RequireDocument();
        var id = ResolveObjectId(document.Database, McpTopLevelJson.ExtractString(body, "handle"));
        using var transaction = document.Database.TransactionManager.StartOpenCloseTransaction();
        var entity = transaction.GetObject(id, OpenMode.ForRead, false) as Entity
            ?? throw new InvalidOperationException("Handle does not identify a live AutoCAD Entity.");
        var result = EntitySummary(entity);
        try
        {
            var extents = entity.GeometricExtents;
            result["extents"] = new Dictionary<string, object?>
            {
                ["min"] = Point(extents.MinPoint), ["max"] = Point(extents.MaxPoint)
            };
        }
        catch { result["extents"] = null; }
        return McpJson.Serialize(result);
    }

    private static string ReadSystemVariable(string body)
    {
        var name = McpTopLevelJson.ExtractString(body, "name").Trim().ToUpperInvariant();
        if (!ReadableSystemVariables.Contains(name)) throw new InvalidOperationException("System variable is not allow-listed: " + name);
        return McpJson.Serialize(new Dictionary<string, object?> { ["name"] = name, ["value"] = SafeSystemVariable(name) });
    }

    private static string WaitUntilIdle(int timeoutMs)
    {
        var started = Environment.TickCount;
        while (unchecked(Environment.TickCount - started) <= timeoutMs)
        {
            var active = McpDiagnosticHub.InvokeInCadContext(() => Convert.ToInt32(AcApplication.GetSystemVariable("CMDACTIVE"), CultureInfo.InvariantCulture));
            if (active == 0) return McpJson.Serialize(new Dictionary<string, object?> { ["idle"] = true, ["cmdActive"] = 0 });
            Thread.Sleep(100);
        }
        return McpJson.Serialize(new Dictionary<string, object?> { ["idle"] = false, ["timedOut"] = true });
    }

    private static string CreateLine(string body)
    {
        var entity = new Line(P3(body, "x1", "y1", "z1"), P3(body, "x2", "y2", "z2"));
        return AppendCreated(entity, body);
    }

    private static string CreateCircle(string body)
    {
        var radius = McpTopLevelJson.RequireDouble(body, "radius");
        if (radius <= 0) throw new InvalidOperationException("radius must be positive.");
        var entity = new Circle(P3(body, "x", "y", "z"), Vector3d.ZAxis, radius);
        return AppendCreated(entity, body);
    }

    private static string CreateArc(string body)
    {
        var radius = McpTopLevelJson.RequireDouble(body, "radius");
        if (radius <= 0) throw new InvalidOperationException("radius must be positive.");
        var entity = new Arc(P3(body, "x", "y", "z"), radius,
            McpTopLevelJson.RequireDouble(body, "startAngle"), McpTopLevelJson.RequireDouble(body, "endAngle"));
        return AppendCreated(entity, body);
    }

    private static string CreatePolyline(string body)
    {
        var points = McpTopLevelJson.ExtractArray(body, "points") ?? throw new InvalidOperationException("points array is required.");
        if (points.Count < 2 || points.Count > 256) throw new InvalidOperationException("points must contain between 2 and 256 items.");
        var polyline = new Polyline(points.Count);
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index] as Dictionary<string, object?> ?? throw new InvalidOperationException("Each polyline point must be an object.");
            polyline.AddVertexAt(index, new Point2d(Number(point, "x"), Number(point, "y")), 0, 0, 0);
        }
        polyline.Closed = McpTopLevelJson.ExtractBoolean(body, "closed");
        return AppendCreated(polyline, body);
    }

    private static string CreateText(string body, bool multiLine)
    {
        var text = McpTopLevelJson.ExtractString(body, "text");
        if (text.Length == 0 || text.Length > (multiLine ? 32000 : 8000)) throw new InvalidOperationException("text is required and must be bounded.");
        var height = McpTopLevelJson.OptionalDouble(body, "height", 2.5);
        if (height <= 0) throw new InvalidOperationException("height must be positive.");
        Entity entity;
        if (multiLine)
        {
            var mtext = new MText { Location = P3(body, "x", "y", "z"), Contents = text, TextHeight = height };
            var width = McpTopLevelJson.OptionalDouble(body, "width", 0);
            if (width > 0) mtext.Width = width;
            entity = mtext;
        }
        else entity = new DBText { Position = P3(body, "x", "y", "z"), TextString = text, Height = height };
        return AppendCreated(entity, body);
    }

    private static string AppendCreated(Entity entity, string body)
    {
        var document = RequireDocument();
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var layer = McpTopLevelJson.ExtractString(body, "layer").Trim();
        var layerId = layer.Length == 0 ? document.Database.Clayer : QS3D.AutoCAD.Infrastructure.AutoCadDrawing.EnsureLayer(transaction, document.Database, layer, 7);
        var id = QS3D.AutoCAD.Infrastructure.AutoCadDrawing.Append(transaction, document.Database, entity, layerId);
        transaction.Commit();
        return McpJson.Serialize(new Dictionary<string, object?> { ["created"] = true, ["handle"] = id.Handle.ToString(), ["type"] = entity.GetType().Name, ["layer"] = layer.Length == 0 ? null : layer });
    }

    private static string TransformEntity(string body)
    {
        var document = RequireDocument();
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var entity = transaction.GetObject(ResolveObjectId(document.Database, McpTopLevelJson.ExtractString(body, "handle")), OpenMode.ForWrite, false) as Entity
            ?? throw new InvalidOperationException("Handle does not identify a live entity.");
        var dx = McpTopLevelJson.OptionalDouble(body, "dx", 0);
        var dy = McpTopLevelJson.OptionalDouble(body, "dy", 0);
        var dz = McpTopLevelJson.OptionalDouble(body, "dz", 0);
        if (dx != 0 || dy != 0 || dz != 0) entity.TransformBy(Matrix3d.Displacement(new Vector3d(dx, dy, dz)));
        var rotation = McpTopLevelJson.OptionalDouble(body, "rotationRadians", 0);
        if (rotation != 0) entity.TransformBy(Matrix3d.Rotation(rotation, Vector3d.ZAxis, Point3d.Origin));
        var scale = McpTopLevelJson.OptionalDouble(body, "scale", 1);
        if (scale <= 0) throw new InvalidOperationException("scale must be positive.");
        if (Math.Abs(scale - 1) > 1e-12) entity.TransformBy(Matrix3d.Scaling(scale, Point3d.Origin));
        transaction.Commit();
        return McpJson.Serialize(new Dictionary<string, object?> { ["updated"] = true, ["handle"] = entity.Handle.ToString() });
    }

    private static string DeleteEntity(string body)
    {
        var document = RequireDocument();
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var entity = transaction.GetObject(ResolveObjectId(document.Database, McpTopLevelJson.ExtractString(body, "handle")), OpenMode.ForWrite, false) as Entity
            ?? throw new InvalidOperationException("Handle does not identify a live entity.");
        var handle = entity.Handle.ToString();
        entity.Erase();
        transaction.Commit();
        return McpJson.Serialize(new Dictionary<string, object?> { ["deleted"] = true, ["handle"] = handle });
    }

    private static string SetEntityLayer(string body)
    {
        var layer = McpTopLevelJson.ExtractString(body, "layer").Trim();
        if (layer.Length == 0 || layer.Length > 255) throw new InvalidOperationException("layer is required and bounded to 255 characters.");
        var document = RequireDocument();
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var layerId = QS3D.AutoCAD.Infrastructure.AutoCadDrawing.EnsureLayer(transaction, document.Database, layer, 7);
        var entity = transaction.GetObject(ResolveObjectId(document.Database, McpTopLevelJson.ExtractString(body, "handle")), OpenMode.ForWrite, false) as Entity
            ?? throw new InvalidOperationException("Handle does not identify a live entity.");
        entity.LayerId = layerId;
        transaction.Commit();
        return McpJson.Serialize(new Dictionary<string, object?> { ["updated"] = true, ["handle"] = entity.Handle.ToString(), ["layer"] = layer });
    }

    private static string LayerAction(string body)
    {
        var action = McpTopLevelJson.ExtractString(body, "action");
        var name = McpTopLevelJson.ExtractString(body, "name").Trim();
        if (name.Length == 0 || name.Length > 255) throw new InvalidOperationException("name is required and bounded to 255 characters.");
        var document = RequireDocument();
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var color = McpTopLevelJson.OptionalInt(body, "colorIndex", 7, 1, 255);
        var id = QS3D.AutoCAD.Infrastructure.AutoCadDrawing.EnsureLayer(transaction, document.Database, name, (short)color);
        if (string.Equals(action, "setCurrent", StringComparison.OrdinalIgnoreCase)) document.Database.Clayer = id;
        else if (!string.Equals(action, "create", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("action must be create or setCurrent.");
        transaction.Commit();
        return McpJson.Serialize(new Dictionary<string, object?> { ["updated"] = true, ["action"] = action, ["name"] = name, ["current"] = document.Database.Clayer == id });
    }

    private static string CommandCatalogJson() => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["commands"] = AllowedCadCommands.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
        ["count"] = AllowedCadCommands.Count
    });

    private static string RunCadCommandSequence(string body)
    {
        var command = McpTopLevelJson.ExtractString(body, "command").Trim().ToUpperInvariant();
        if (!AllowedCadCommands.Contains(command)) throw new InvalidOperationException("AutoCAD command is not allow-listed: " + command);
        var inputs = McpTopLevelJson.ExtractString(body, "inputs");
        if (inputs.Length > 4000 || inputs.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
            throw new InvalidOperationException("inputs must be one bounded single-line literal sequence.");
        var document = RequireDocument();
        var payload = "_." + command + (inputs.Length == 0 ? " " : " " + inputs + " ");
        document.SendStringToExecute(payload, true, false, false);
        AuditDomainMutation("cad_command_sequence", "command=" + command);
        return McpJson.Serialize(new Dictionary<string, object?> { ["accepted"] = true, ["queued"] = true, ["command"] = command });
    }

    private static string CancelCurrentCommand()
    {
        var document = RequireDocument();
        document.SendStringToExecute("\u001b\u001b", true, false, false);
        return McpJson.Serialize(new Dictionary<string, object?> { ["cancelQueued"] = true });
    }

    internal static ObjectId ResolveObjectId(Database database, string handleText)
    {
        var token = (handleText ?? string.Empty).Trim();
        if (token.Length == 0 || token.Length > 32 || !long.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            throw new InvalidOperationException("handle must be a hexadecimal AutoCAD entity handle.");
        var id = database.GetObjectId(false, new Handle(value), 0);
        if (id.IsNull || id.IsErased) throw new InvalidOperationException("Entity handle is invalid or erased: " + token);
        return id;
    }

    internal static void AuditDomainMutation(string tool, string detail) => McpDiagnosticHub.Log(tool, detail);

    private static Dictionary<string, object?> EntitySummary(Entity entity) => new()
    {
        ["handle"] = entity.Handle.ToString(), ["type"] = entity.GetType().Name, ["layer"] = entity.Layer, ["erased"] = entity.IsErased
    };

    private static Dictionary<string, object?> Point(Point3d point) => new() { ["x"] = point.X, ["y"] = point.Y, ["z"] = point.Z };

    private static Point3d P3(string body, string xName, string yName, string zName) => new(
        McpTopLevelJson.RequireDouble(body, xName), McpTopLevelJson.RequireDouble(body, yName),
        McpTopLevelJson.HasProperty(body, zName) ? McpTopLevelJson.RequireDouble(body, zName) : 0);

    private static double Number(Dictionary<string, object?> map, string name)
    {
        if (!map.TryGetValue(name, out var value)) throw new InvalidOperationException(name + " is required.");
        return value switch
        {
            long integer => integer,
            double number => number,
            int intValue => intValue,
            _ => throw new InvalidOperationException(name + " must be a number.")
        };
    }

    private static object? SafeSystemVariable(string name)
    {
        try
        {
            var value = AcApplication.GetSystemVariable(name);
            return value is null ? null : value is string or bool or int or short or long or double ? value : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) { return "unavailable: " + ex.Message; }
    }

    private static Document RequireDocument() => AcApplication.DocumentManager.MdiActiveDocument
        ?? throw new InvalidOperationException("No active AutoCAD document is available.");
}