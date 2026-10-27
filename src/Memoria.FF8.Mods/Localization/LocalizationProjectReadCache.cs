using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Resources;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Mods.Localization;

/// <summary>Bounded content-keyed reuse of parsed rows, validated PNG dimensions and decoded target pixels. Files and project contracts are checked on every read.</summary>
public sealed class LocalizationProjectReadCache
{
    private sealed record Entry(object Value, long Cost, LinkedListNode<string> Node);
    internal sealed record ArtworkDimensions(int Width, int Height);
    internal sealed record ArtworkImage(int Width, int Height, ImmutableArray<Rgba32> Pixels);
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> recent = new();
    private readonly long maximumBytes;
    private long usedBytes;
    private long hits;
    private long parses;
    private long artworkDecodes;
    private long artworkHits;

    public LocalizationProjectReadCache(long maximumBytes = 128 * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        this.maximumBytes = maximumBytes;
    }

    public long CacheHits => Interlocked.Read(ref hits);
    public long ParsedPayloads => Interlocked.Read(ref parses);
    public long DecodedArtworkImages => Interlocked.Read(ref artworkDecodes);
    public long ArtworkCacheHits => Interlocked.Read(ref artworkHits);
    public long CachedBytes
    {
        get
        {
            lock (gate)
            {
                return usedBytes;
            }
        }
    }
    public long MaximumBytes => maximumBytes;

    internal IReadOnlyList<LocalizationRecord> Read(byte[] bytes, LocalizationManifest manifest, LocalizationPayload payload)
    {
        string contract = JsonSerializer.Serialize(new { manifest.SchemaVersion, manifest.Format, manifest.SourceColumn, manifest.TargetColumn, manifest.ReferenceLanguages, Payload = payload });
        string key = contract + "\n" + Convert.ToHexString(SHA256.HashData(bytes));
        lock (gate)
        {
            if (entries.TryGetValue(key, out Entry? found))
            {
                recent.Remove(found.Node);
                recent.AddLast(found.Node);
                Interlocked.Increment(ref hits);
                return (ImmutableArray<LocalizationRecord>)found.Value;
            }
        }
        IReadOnlyList<LocalizationRecord> parsed = manifest.Format == "json" ? LocalizationProjectJson.Read(bytes, manifest, payload) : LocalizationProjectWorkbook.Read(bytes, manifest, payload);
        Interlocked.Increment(ref parses);
        ImmutableArray<LocalizationRecord> rows = parsed.Select(row => row with
        {
            References = row.References.ToImmutableDictionary(StringComparer.Ordinal),
            ReferencePresence = row.ReferencePresence.ToImmutableDictionary(StringComparer.Ordinal),
            ReferenceKeys = row.ReferenceKeys.ToImmutableDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value.ToImmutableArray(), StringComparer.Ordinal),
            ExtensionData = row.ExtensionData.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal)
        }).ToImmutableArray();
        long cost = checked((long)bytes.Length * 4 + rows.Length * 512L + key.Length * 2L + 512L);
        Store(key, rows, cost);
        return rows;
    }

    internal ArtworkDimensions ReadArtworkDimensions(byte[] bytes, string path)
    {
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        string key = "png-dimensions:" + hash;
        if (TryGet(key, out ArtworkDimensions? dimensions))
        {
            Interlocked.Increment(ref artworkHits);
            return dimensions;
        }
        if (TryGet("png-rgba:" + hash, out ArtworkImage? image))
        {
            Interlocked.Increment(ref artworkHits);
            return new(image.Width, image.Height);
        }
        PixelImage decoded = TextureImageDecoder.Decode(bytes, path).Image;
        Interlocked.Increment(ref artworkDecodes);
        dimensions = new(decoded.Width, decoded.Height);
        Store(key, dimensions, 512L + key.Length * 2L);
        return dimensions;
    }

    internal ArtworkImage ReadArtworkImage(byte[] bytes, string path)
    {
        string key = "png-rgba:" + Convert.ToHexString(SHA256.HashData(bytes));
        if (TryGet(key, out ArtworkImage? image))
        {
            Interlocked.Increment(ref artworkHits);
            return image;
        }
        PixelImage decoded = TextureImageDecoder.Decode(bytes, path).Image;
        Interlocked.Increment(ref artworkDecodes);
        image = new(decoded.Width, decoded.Height, decoded.Pixels.ToImmutableArray());
        Store(key, image, checked((long)decoded.Width * decoded.Height * 4 + 512L + key.Length * 2L));
        return image;
    }

    private bool TryGet<T>(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? value) where T : class
    {
        lock (gate)
        {
            if (entries.TryGetValue(key, out Entry? found))
            {
                recent.Remove(found.Node);
                recent.AddLast(found.Node);
                value = (T)found.Value;
                return true;
            }
        }
        value = null;
        return false;
    }

    private void Store(string key, object value, long cost)
    {
        if (cost > maximumBytes)
        {
            return;
        }
        lock (gate)
        {
            if (entries.ContainsKey(key))
            {
                return;
            }
            while (usedBytes + cost > maximumBytes && recent.First is { } oldest)
            {
                Entry removed = entries[oldest.Value];
                usedBytes -= removed.Cost;
                entries.Remove(oldest.Value);
                recent.RemoveFirst();
            }
            LinkedListNode<string> node = recent.AddLast(key);
            entries.Add(key, new(value, cost, node));
            usedBytes += cost;
        }
    }
}
