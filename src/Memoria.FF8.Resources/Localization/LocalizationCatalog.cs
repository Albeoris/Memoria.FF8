using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF8.Resources.Localization;

public sealed record LocalizationValue(string Text, string PackageId, string ProjectId)
{
    public LocalizationFieldText? Field { get; init; }
}

public sealed record LocalizationCallBinding(int Operation, int? MessageId, bool Dynamic, int? FirstChoice, int? LastChoice, int? InitialChoice, int? CancelChoice)
{
    public bool IsChoice { get; init; }
}
public sealed record LocalizationChoiceLineMapEntry(int SourceLine, int TargetLine);
public sealed record LocalizationFieldText(string Body, string ScriptHash, int? X, int? Y, int? Width, int? Height, int? FirstChoice, int? LastChoice, int? InitialChoice, int? CancelChoice, ImmutableArray<LocalizationCallBinding> Calls)
{
    public string? BaseKey { get; init; }
    public ImmutableArray<LocalizationChoiceLineMapEntry> ChoiceLineMap { get; init; } = [];
}

/// <summary>One fully validated, immutable presentation generation. Empty targets never shadow another layer.</summary>
public sealed class LocalizationCatalog
{
    public static LocalizationCatalog Empty { get; } = new([]);
    public ImmutableDictionary<string, LocalizationValue> Values { get; }
    public ImmutableDictionary<TextureKey, LocalizationArtworkValue> Artwork { get; }
    private readonly ImmutableArray<(ImmutableDictionary<string, LocalizationValue> Values, ImmutableDictionary<(string Key, int Operation), LocalizationValue> Variants)> layers;
    public string Fingerprint { get; }
    public string ArtworkFingerprint { get; }
    public ImmutableDictionary<TextureKey, string> ArtworkFingerprints { get; }

    public LocalizationCatalog(IEnumerable<KeyValuePair<string, LocalizationValue>> values, IEnumerable<IReadOnlyDictionary<string, LocalizationValue>>? priorityLayers = null, IEnumerable<KeyValuePair<TextureKey, LocalizationArtworkValue>>? artwork = null)
    {
        Values = values.ToImmutableDictionary(StringComparer.Ordinal);
        Artwork = (artwork ?? []).ToImmutableDictionary();
        foreach ((TextureKey key, LocalizationArtworkValue value) in Artwork)
        {
            value.Validate();
            if (key != value.Key)
            {
                throw new InvalidDataException("Artwork catalog key differs from its declared native identity.");
            }
        }
        layers = (priorityLayers ?? new[] { Values }).Where(layer => layer.Count != 0).Select(layer =>
        {
            ImmutableDictionary<string, LocalizationValue> owned = layer.ToImmutableDictionary(StringComparer.Ordinal);
            Dictionary<(string Key, int Operation), LocalizationValue> variants = [];
            foreach ((string key, LocalizationValue value) in owned.Where(pair => pair.Value.Field?.BaseKey is string baseKey && baseKey != pair.Key))
            {
                string baseKey = value.Field!.BaseKey!;
                foreach (LocalizationCallBinding call in value.Field!.Calls)
                {
                    if (!variants.TryAdd((baseKey, call.Operation), value))
                    {
                        throw new InvalidDataException($"{baseKey}: multiple context variants address operation {call.Operation} in one layer.");
                    }
                }
            }
            return (owned, variants.ToImmutableDictionary());
        }).ToImmutableArray();
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var layer in layers)
        {
            hash.AppendData(new byte[] { 0xFF });
            foreach ((string key, LocalizationValue value) in layer.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                foreach (string part in new[] { key, value.Text, value.Field is null ? "" : System.Text.Json.JsonSerializer.Serialize(value.Field) })
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(part);
                    hash.AppendData(BitConverter.GetBytes(bytes.Length));
                    hash.AppendData(bytes);
                }
            }
        }
        ImmutableDictionary<TextureKey, string>.Builder artworkFingerprints = ImmutableDictionary.CreateBuilder<TextureKey, string>();
        using IncrementalHash artworkHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach ((TextureKey key, LocalizationArtworkValue value) in Artwork.OrderBy(pair => pair.Key.Owner.Path, StringComparer.Ordinal).ThenBy(pair => pair.Key.Subresource, StringComparer.Ordinal).ThenBy(pair => pair.Key.Palette))
        {
            using IncrementalHash entryHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] identity = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new { Owner = key.Owner.Path, key.Subresource, key.Palette, value.SourceSha256, value.LogicalWidth, value.LogicalHeight, value.Width, value.Height }));
            entryHash.AppendData(BitConverter.GetBytes(identity.Length));
            entryHash.AppendData(identity);
            entryHash.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(value.Pixels.AsSpan()));
            byte[] digest = entryHash.GetHashAndReset();
            artworkFingerprints.Add(key, Convert.ToHexString(digest));
            artworkHash.AppendData(digest);
        }
        ArtworkFingerprints = artworkFingerprints.ToImmutable();
        byte[] artworkDigest = artworkHash.GetHashAndReset();
        ArtworkFingerprint = Convert.ToHexString(artworkDigest);
        if (Artwork.Count != 0)
        {
            hash.AppendData(new byte[] { 0xFE });
            hash.AppendData(artworkDigest);
        }
        Fingerprint = Convert.ToHexString(hash.GetHashAndReset());
    }

    public TextureReplacement? ResolveArtwork(TextureBuildContext context) => Artwork.TryGetValue(context.Key, out LocalizationArtworkValue? value) ? value.CreateReplacement(context) : null;

    public string Resolve(string key, string original) => Values.TryGetValue(key, out LocalizationValue? value) ? value.Text : original;

    public string ResolveAliases(IReadOnlyList<string> keys, string original)
    {
        foreach (var layer in layers.Reverse())
        {
            foreach (string key in keys)
            {
                if (layer.Values.TryGetValue(key, out LocalizationValue? value) && !string.IsNullOrEmpty(value.Text))
                {
                    return value.Text;
                }
            }
        }
        return original;
    }

    public LocalizationFieldText? ResolveField(string key, int operation, string scriptHash, int? nativeFirst, int? nativeLast, int? nativeInitial, int? nativeCancel)
    {
        foreach (var layer in layers.Reverse())
        {
            foreach (LocalizationValue? candidate in new[] { layer.Variants.GetValueOrDefault((key, operation)), layer.Values.GetValueOrDefault(key) })
            {
                if (candidate?.Field is not { } field || field.ScriptHash != scriptHash)
                {
                    continue;
                }
                LocalizationCallBinding? call = field.Calls.FirstOrDefault(call => call.Operation == operation);
                if (call is null)
                {
                    continue;
                }
                bool Matches(int? expected, int? actual) => expected == actual || call.Dynamic && expected is null;
                if (!Matches(call.FirstChoice, nativeFirst) || !Matches(call.LastChoice, nativeLast) || !Matches(call.InitialChoice, nativeInitial) || !Matches(call.CancelChoice, nativeCancel))
                {
                    continue;
                }
                if (call.Dynamic && call.IsChoice)
                {
                    if (nativeFirst is not int first || nativeLast is not int last || nativeInitial is not int initial || nativeCancel is not int cancel || first < 0 || last < first || initial < first || initial > last)
                    {
                        continue;
                    }
                    Dictionary<int, int> map = field.ChoiceLineMap.ToDictionary(entry => entry.SourceLine, entry => entry.TargetLine);
                    int Map(int line) => map.Count == 0 ? line : map.GetValueOrDefault(line, -1);
                    int translatedFirst = Map(first);
                    int translatedLast = Map(last);
                    int lineCount = CountAuthoredLines(field.Body);
                    if (translatedFirst < 0 || translatedLast >= lineCount || (long)translatedLast - translatedFirst != (long)last - first || Enumerable.Range(0, translatedLast - translatedFirst + 1).Any(offset => Map(first + offset) != translatedFirst + offset))
                    {
                        continue;
                    }
                    return field with { FirstChoice = translatedFirst, LastChoice = translatedLast, InitialChoice = Map(initial), CancelChoice = cancel >= first && cancel <= last ? Map(cancel) : cancel };
                }
                if (call.FirstChoice == nativeFirst && call.LastChoice == nativeLast && call.InitialChoice == nativeInitial && call.CancelChoice == nativeCancel || call.Dynamic && !call.IsChoice && nativeFirst is null && nativeLast is null && nativeInitial is null && nativeCancel is null)
                {
                    return field;
                }
            }
        }
        return null;
    }

    public static int CountAuthoredLines(string body) => body.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Count(character => character == '\n') + 1 + System.Text.RegularExpressions.Regex.Matches(body, "\\{(?:Line|Page|Next)\\}").Count;
}
