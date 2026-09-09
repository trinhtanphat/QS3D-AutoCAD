using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal static class McpCadLayerStateRuntime
{
    private const int MaxSnapshotLayers = 4096;
    private const int MaxSnapshotTokenLength = 512 * 1024;
    private const string SnapshotVersion = "QS3D-AUTOCAD-LAYER-STATE-V1";
    private static readonly HashSet<string> Tools = new(StringComparer.Ordinal)
    {
        "cad_layer_state", "cad_layer_set_state", "cad_layer_snapshot", "cad_layer_restore"
    };

    internal static bool IsTool(string? tool) => Tools.Contains(tool ?? string.Empty);
    internal static bool RequiresMutation(string? tool) => tool is "cad_layer_set_state" or "cad_layer_restore";
    internal static IEnumerable<McpToolDescriptor> ToolDescriptors() =>
        McpToolRegistry.ToolDescriptors().Where(item => IsTool(item.Name));

    internal static string Call(string tool, string body) => McpDiagnosticHub.InvokeInCadContext(() => CallInCadContext(tool, body));

    internal static string CallInCadContext(string tool, string body)
    {
        return tool switch
        {
            "cad_layer_state" => ReadLayerState(body),
            "cad_layer_set_state" => SetLayerState(body),
            "cad_layer_snapshot" => CaptureSnapshot(),
            "cad_layer_restore" => RestoreSnapshot(body),
            _ => throw new InvalidOperationException("Unknown AutoCAD MCP layer-state tool: " + tool)
        };
    }

    private static string ReadLayerState(string body)
    {
        var name = RequireLayerName(McpTopLevelJson.ExtractString(body, "name"));
        var document = RequireDocument();
        using var transaction = document.Database.TransactionManager.StartOpenCloseTransaction();
        var record = RequireLayer(transaction, document.Database, name, OpenMode.ForRead);
        return LayerStateJson(record, record.ObjectId == document.Database.Clayer);
    }

    private static string SetLayerState(string body)
    {
        var name = RequireLayerName(McpTopLevelJson.ExtractString(body, "name"));
        var map = McpTopLevelJson.ParseObject(body);
        var hasOn = map.TryGetValue("on", out var onValue) && onValue is bool;
        var hasFrozen = map.TryGetValue("frozen", out var frozenValue) && frozenValue is bool;
        var hasLocked = map.TryGetValue("locked", out var lockedValue) && lockedValue is bool;
        if (!hasOn && !hasFrozen && !hasLocked) throw new InvalidOperationException("At least one of on, frozen or locked is required.");
        var requestedOn = onValue is bool on && on;
        var requestedFrozen = frozenValue is bool frozen && frozen;
        var requestedLocked = lockedValue is bool locked && locked;
        var document = RequireDocument();
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var table = (LayerTable)transaction.GetObject(document.Database.LayerTableId, OpenMode.ForRead);
        if (!table.Has(name)) throw new InvalidOperationException("Layer does not exist: " + name);
        var id = table[name];
        var isCurrent = id == document.Database.Clayer;
        if (isCurrent && ((hasOn && !requestedOn) || (hasFrozen && requestedFrozen)))
            throw new InvalidOperationException("The current layer cannot be turned off or frozen: " + name);
        var record = (LayerTableRecord)transaction.GetObject(id, OpenMode.ForWrite);
        if (hasOn) record.IsOff = !requestedOn;
        if (hasFrozen) record.IsFrozen = requestedFrozen;
        if (hasLocked) record.IsLocked = requestedLocked;
        transaction.Commit();
        McpCadAgentRuntime.AuditDomainMutation("cad_layer_set_state", "name=" + name);
        return LayerStateJson(record, isCurrent);
    }

    private static string CaptureSnapshot()
    {
        var document = RequireDocument();
        using var transaction = document.Database.TransactionManager.StartOpenCloseTransaction();
        var table = (LayerTable)transaction.GetObject(document.Database.LayerTableId, OpenMode.ForRead);
        var entries = new List<LayerEntry>();
        foreach (ObjectId id in table)
        {
            if (entries.Count >= MaxSnapshotLayers) throw new InvalidOperationException("Layer snapshot exceeds bounded layer count.");
            var record = (LayerTableRecord)transaction.GetObject(id, OpenMode.ForRead);
            entries.Add(new LayerEntry(record.Name, !record.IsOff, record.IsFrozen, record.IsLocked));
        }
        entries.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        var current = (LayerTableRecord)transaction.GetObject(document.Database.Clayer, OpenMode.ForRead);
        var token = EncodeSnapshot(current.Name, entries);
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["captured"] = true, ["layerCount"] = entries.Count, ["currentLayer"] = current.Name, ["snapshot"] = token
        });
    }

    private static string RestoreSnapshot(string body)
    {
        var token = McpTopLevelJson.ExtractString(body, "snapshot");
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxSnapshotTokenLength)
            throw new InvalidOperationException("snapshot is required and must be bounded.");
        var snapshot = DecodeSnapshot(token);
        var document = RequireDocument();
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var table = (LayerTable)transaction.GetObject(document.Database.LayerTableId, OpenMode.ForRead);
        var current = (LayerTableRecord)transaction.GetObject(document.Database.Clayer, OpenMode.ForRead);
        if (!string.Equals(snapshot.CurrentLayer, current.Name, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Snapshot current-layer identity no longer matches the active drawing.");
        var ids = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in snapshot.Entries)
        {
            if (!table.Has(entry.Name)) throw new InvalidOperationException("Snapshot layer no longer exists: " + entry.Name);
            var id = table[entry.Name];
            if (!ids.TryAdd(entry.Name, id)) throw new InvalidOperationException("Snapshot contains duplicate layer: " + entry.Name);
            if (id == document.Database.Clayer && (!entry.On || entry.Frozen))
                throw new InvalidOperationException("Snapshot would turn off/freeze current layer.");
        }
        foreach (var entry in snapshot.Entries)
        {
            var record = (LayerTableRecord)transaction.GetObject(ids[entry.Name], OpenMode.ForWrite);
            record.IsOff = !entry.On;
            record.IsFrozen = entry.Frozen;
            record.IsLocked = entry.Locked;
        }
        transaction.Commit();
        McpCadAgentRuntime.AuditDomainMutation("cad_layer_restore", "layerCount=" + snapshot.Entries.Count);
        return McpJson.Serialize(new Dictionary<string, object?> { ["restored"] = true, ["layerCount"] = snapshot.Entries.Count, ["currentLayer"] = snapshot.CurrentLayer });
    }

    private static string EncodeSnapshot(string currentLayer, IReadOnlyList<LayerEntry> entries)
    {
        var text = new StringBuilder().Append(SnapshotVersion).Append('\n').Append(ToBase64(currentLayer)).Append('\n');
        foreach (var entry in entries)
            text.Append(ToBase64(entry.Name)).Append('|').Append(entry.On ? '1' : '0').Append('|').Append(entry.Frozen ? '1' : '0').Append('|').Append(entry.Locked ? '1' : '0').Append('\n');
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(text.ToString()));
        if (token.Length > MaxSnapshotTokenLength) throw new InvalidOperationException("Layer snapshot token exceeds bounded size.");
        return token;
    }

    private static LayerSnapshot DecodeSnapshot(string token)
    {
        string text;
        try { text = Encoding.UTF8.GetString(Convert.FromBase64String(token)); }
        catch (Exception ex) { throw new InvalidOperationException("snapshot is malformed.", ex); }
        var lines = text.Split(new[] { '\n' }, StringSplitOptions.None);
        if (lines.Length < 2 || lines[0] != SnapshotVersion) throw new InvalidOperationException("snapshot version is unsupported.");
        var current = FromBase64(lines[1]);
        var entries = new List<LayerEntry>();
        for (var index = 2; index < lines.Length; index++)
        {
            if (lines[index].Length == 0) continue;
            var parts = lines[index].Split('|');
            if (parts.Length != 4 || entries.Count >= MaxSnapshotLayers) throw new InvalidOperationException("snapshot layer entry is malformed.");
            entries.Add(new LayerEntry(RequireLayerName(FromBase64(parts[0])), parts[1] == "1", parts[2] == "1", parts[3] == "1"));
        }
        if (entries.Count == 0) throw new InvalidOperationException("snapshot contains no layers.");
        return new LayerSnapshot(RequireLayerName(current), entries);
    }

    private static string LayerStateJson(LayerTableRecord record, bool current) => McpJson.Serialize(new Dictionary<string, object?>
    {
        ["name"] = record.Name, ["on"] = !record.IsOff, ["frozen"] = record.IsFrozen, ["locked"] = record.IsLocked, ["current"] = current
    });

    private static LayerTableRecord RequireLayer(Transaction transaction, Database database, string name, OpenMode mode)
    {
        var table = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (!table.Has(name)) throw new InvalidOperationException("Layer does not exist: " + name);
        return (LayerTableRecord)transaction.GetObject(table[name], mode);
    }

    private static string RequireLayerName(string? name)
    {
        var value = (name ?? string.Empty).Trim();
        if (value.Length == 0 || value.Length > 255) throw new InvalidOperationException("Layer name must contain 1-255 characters.");
        return value;
    }

    private static Document RequireDocument() => AcApplication.DocumentManager.MdiActiveDocument
        ?? throw new InvalidOperationException("No active AutoCAD document is available.");
    private static string ToBase64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    private static string FromBase64(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
    private sealed record LayerEntry(string Name, bool On, bool Frozen, bool Locked);
    private sealed record LayerSnapshot(string CurrentLayer, List<LayerEntry> Entries);
}