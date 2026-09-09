using System.Security.Cryptography;
using System.Text;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal sealed class McpMutationReservation
{
    internal required string ActionId { get; init; }
    internal required string Tool { get; init; }
    internal required string Fingerprint { get; init; }
    internal bool Replayed { get; init; }
    internal string Status { get; set; } = "reserved";
    internal string? ResultJson { get; set; }
    internal string? Error { get; set; }
    internal DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

internal static class McpMutationAckLedger
{
    private const int MaxEntries = 512;
    private static readonly object Sync = new();
    private static readonly Dictionary<string, McpMutationReservation> Entries = new(StringComparer.Ordinal);
    private static readonly Queue<string> Order = new();

    internal static void ResetForServerStart()
    {
        lock (Sync)
        {
            Entries.Clear();
            Order.Clear();
        }
    }

    internal static McpMutationReservation ReserveOrReplay(string tool, string body)
    {
        var actionId = McpTopLevelJson.ExtractString(body, "actionId").Trim();
        if (actionId.Length == 0) actionId = Guid.NewGuid().ToString("N");
        if (actionId.Length > 128) throw new InvalidOperationException("actionId exceeds 128 characters.");
        var fingerprint = Fingerprint(tool, body);

        lock (Sync)
        {
            if (Entries.TryGetValue(actionId, out var existing))
            {
                if (!string.Equals(existing.Tool, tool, StringComparison.Ordinal)
                    || !string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
                    throw new InvalidOperationException("actionId already belongs to a different MCP mutation payload.");
                return new McpMutationReservation
                {
                    ActionId = existing.ActionId,
                    Tool = existing.Tool,
                    Fingerprint = existing.Fingerprint,
                    Replayed = true,
                    Status = existing.Status,
                    ResultJson = existing.ResultJson,
                    Error = existing.Error,
                    UpdatedUtc = existing.UpdatedUtc
                };
            }

            while (Entries.Count >= MaxEntries && Order.Count > 0)
            {
                var oldest = Order.Dequeue();
                Entries.Remove(oldest);
            }

            var created = new McpMutationReservation
            {
                ActionId = actionId,
                Tool = tool,
                Fingerprint = fingerprint,
                Replayed = false
            };
            Entries.Add(actionId, created);
            Order.Enqueue(actionId);
            return created;
        }
    }

    internal static void Complete(McpMutationReservation reservation, string resultJson)
    {
        lock (Sync)
        {
            if (!Entries.TryGetValue(reservation.ActionId, out var entry)) return;
            entry.Status = "completed";
            entry.ResultJson = resultJson;
            entry.Error = null;
            entry.UpdatedUtc = DateTime.UtcNow;
            reservation.Status = entry.Status;
            reservation.ResultJson = entry.ResultJson;
            reservation.UpdatedUtc = entry.UpdatedUtc;
        }
    }

    internal static void Fail(McpMutationReservation reservation, Exception error)
    {
        lock (Sync)
        {
            if (!Entries.TryGetValue(reservation.ActionId, out var entry)) return;
            entry.Status = "failed";
            entry.Error = Bounded(error.Message, 1024);
            entry.UpdatedUtc = DateTime.UtcNow;
            reservation.Status = entry.Status;
            reservation.Error = entry.Error;
            reservation.UpdatedUtc = entry.UpdatedUtc;
        }
    }

    internal static string StatusJson(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId)) throw new InvalidOperationException("actionId is required.");
        lock (Sync)
        {
            if (!Entries.TryGetValue(actionId, out var entry))
                return McpJson.Serialize(new Dictionary<string, object?> { ["actionId"] = actionId, ["found"] = false });
            return BuildJson(entry, true);
        }
    }

    internal static string BuildResponse(McpMutationReservation reservation, bool replayed)
    {
        lock (Sync)
        {
            if (Entries.TryGetValue(reservation.ActionId, out var entry)) return BuildJson(entry, replayed);
        }
        return BuildJson(reservation, replayed);
    }

    private static string BuildJson(McpMutationReservation entry, bool replayed)
    {
        object? result = null;
        if (!string.IsNullOrWhiteSpace(entry.ResultJson))
        {
            try { result = McpJson.Parse(entry.ResultJson!); }
            catch { result = entry.ResultJson; }
        }
        return McpJson.Serialize(new Dictionary<string, object?>
        {
            ["actionId"] = entry.ActionId,
            ["tool"] = entry.Tool,
            ["status"] = entry.Status,
            ["replayed"] = replayed,
            ["result"] = result,
            ["error"] = entry.Error,
            ["updatedUtc"] = entry.UpdatedUtc.ToUniversalTime().ToString("o")
        });
    }

    private static string Fingerprint(string tool, string body)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes((tool ?? string.Empty) + "\n" + (body ?? "{}")));
        return Convert.ToBase64String(bytes);
    }

    private static string Bounded(string value, int max) => value.Length <= max ? value : value.Substring(0, max);
}