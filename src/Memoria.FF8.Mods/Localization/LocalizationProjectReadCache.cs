using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

/// <summary>Bounded parsed-payload reuse. File snapshots and complete-project validation still run for every read.</summary>
public sealed class LocalizationProjectReadCache
{
    private sealed record Entry(ImmutableArray<LocalizationRecord> Rows, long Cost, LinkedListNode<string> Node);
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> recent = new();
    private readonly long maximumBytes;
    private long usedBytes;
    private long hits;
    private long parses;

    public LocalizationProjectReadCache(long maximumBytes = 128 * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        this.maximumBytes = maximumBytes;
    }

    public long CacheHits => Interlocked.Read(ref hits);
    public long ParsedPayloads => Interlocked.Read(ref parses);

    internal IReadOnlyList<LocalizationRecord> Read(byte[] bytes, LocalizationManifest manifest, LocalizationPayload payload)
    {
        string contract = JsonSerializer.Serialize(new { manifest.Format, manifest.SourceColumn, manifest.TargetColumn, manifest.ReferenceLanguages, Payload = payload });
        string key = contract + "\n" + Convert.ToHexString(SHA256.HashData(bytes));
        lock (gate)
        {
            if (entries.TryGetValue(key, out Entry? found))
            {
                recent.Remove(found.Node);
                recent.AddLast(found.Node);
                Interlocked.Increment(ref hits);
                return found.Rows;
            }
        }
        IReadOnlyList<LocalizationRecord> parsed = manifest.Format == "json" ? LocalizationProjectJson.Read(bytes, manifest, payload) : LocalizationProjectWorkbook.Read(bytes, manifest, payload);
        Interlocked.Increment(ref parses);
        ImmutableArray<LocalizationRecord> rows = parsed.Select(row => row with
        {
            References = row.References.ToImmutableDictionary(StringComparer.Ordinal),
            ReferencePresence = row.ReferencePresence.ToImmutableDictionary(StringComparer.Ordinal),
            ExtensionData = row.ExtensionData.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal)
        }).ToImmutableArray();
        long cost = checked((long)bytes.Length * 4 + rows.Length * 512L + key.Length * 2L);
        if (cost > maximumBytes)
        {
            return rows;
        }
        lock (gate)
        {
            if (entries.ContainsKey(key))
            {
                return rows;
            }
            while (usedBytes + cost > maximumBytes && recent.First is { } oldest)
            {
                Entry removed = entries[oldest.Value];
                usedBytes -= removed.Cost;
                entries.Remove(oldest.Value);
                recent.RemoveFirst();
            }
            LinkedListNode<string> node = recent.AddLast(key);
            entries.Add(key, new(rows, cost, node));
            usedBytes += cost;
        }
        return rows;
    }
}
