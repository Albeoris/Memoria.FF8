using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public static class LocalizationBindingValidation
{
    /// <summary>Rejects unknown physical table layouts before any non-field translation is published.</summary>
    public static void Validate(LocalizationPreparation preparation, IResourceSource source, CancellationToken cancellation = default, IReadOnlyDictionary<string, LocalizationSourceEntry>? knownEntries = null)
    {
        Dictionary<string, string> hashes = new(StringComparer.Ordinal);
        foreach (LocalizationLayer layer in preparation.Layers.Where(layer => preparation.RequestedOrder.Contains(layer.PackageId, StringComparer.OrdinalIgnoreCase)))
        {
            foreach (var row in layer.Project.Entries.Where(row => !string.IsNullOrEmpty(row.Target) && row.Category is not ("Fields" or "System")))
            {
                cancellation.ThrowIfCancellationRequested();
                if (knownEntries is not null && (!knownEntries.TryGetValue(row.Key, out LocalizationSourceEntry? known) || known.Category != row.Category || known.File != row.File))
                {
                    throw new InvalidDataException($"{row.Key}: no supported original text identity exists in this resource profile.");
                }
                if (!row.ExtensionData.TryGetValue("sourceContext", out JsonElement context) || context.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException($"{row.Key}: the original resource identity is missing.");
                }
                string? Metadata(string name) => context.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (Metadata("Native Source Fingerprint") is string nativeFingerprint)
                {
                    if (source is not IGameNativeMemorySource native || !native.SourceHash.Equals(nativeFingerprint, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"{row.Key}: the authenticated native text profile is unavailable.");
                    }
                    continue;
                }
                string resource = Metadata("Resource") ?? throw new InvalidDataException($"{row.Key}: missing source resource.");
                string fingerprint = Metadata("Source SHA256") ?? throw new InvalidDataException($"{row.Key}: this source table has no verified runtime fingerprint.");
                Check(resource, fingerprint, row.Key);
                if (Metadata("Companion Resource") is string companion)
                {
                    Check(companion, Metadata("Companion SHA256") ?? throw new InvalidDataException($"{row.Key}: missing companion resource fingerprint."), row.Key);
                }
            }
        }
        void Check(string resource, string expected, string key)
        {
            if (!hashes.TryGetValue(resource, out string? actual))
            {
                ResourceContent content = source.Find(new ResourceKey(resource)) ?? throw new InvalidDataException($"{key}: resource {resource} is unavailable.");
                using Stream stream = content.OpenRead();
                hashes.Add(resource, actual = Convert.ToHexString(SHA256.HashData(stream)));
            }
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"{key}: resource {resource} differs from the declared source profile. Export or explicitly review the new layout first.");
            }
        }
    }
}
