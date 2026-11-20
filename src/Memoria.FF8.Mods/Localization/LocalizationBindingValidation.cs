using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Albeoris.Games.FF8.Msd;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public static class LocalizationBindingValidation
{
    /// <summary>Rejects unknown physical table layouts before any non-field translation is published.</summary>
    public static void Validate(LocalizationPreparation preparation, IResourceSource source, CancellationToken cancellation = default, IReadOnlyDictionary<string, LocalizationSourceEntry>? knownEntries = null)
    {
        Dictionary<string, string> hashes = new(StringComparer.Ordinal);
        Dictionary<string, int> slots = new(StringComparer.Ordinal);
        foreach (LocalizationLayer layer in preparation.Layers.Where(layer => preparation.RequestedOrder.Contains(layer.PackageId, StringComparer.OrdinalIgnoreCase)))
        {
            foreach (var row in layer.Project.Entries.Where(row => !string.IsNullOrEmpty(row.Target) && row.Category != "System" && (row.Category != "Fields" || layer.EditionProjected)))
            {
                cancellation.ThrowIfCancellationRequested();
                LocalizationSourceEntry? known = knownEntries?.GetValueOrDefault(row.Key);
                if (row.Category != "Fields" && knownEntries is not null && (known is null || known.Category != row.Category || known.File != row.File))
                {
                    throw new InvalidDataException($"{row.Key}: no supported original text identity exists in this resource profile.");
                }
                if (!row.ExtensionData.TryGetValue("sourceContext", out JsonElement context) || context.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException($"{row.Key}: the original resource identity is missing.");
                }
                string? Metadata(string name) => context.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (row.Category == "Fields")
                {
                    string field = $"field/{row.File}/{row.File}";
                    string bank = field + ".msd";
                    Check(bank, Metadata("Source MSD SHA256") ?? throw new InvalidDataException($"{row.Key}: missing projected MSD fingerprint."), row.Key);
                    if (!slots.TryGetValue(bank, out int count))
                    {
                        using Stream stream = source.Find(new ResourceKey(bank))!.OpenRead();
                        using MemoryStream bytes = new();
                        stream.CopyTo(bytes);
                        count = MsdFile.Read(bytes.ToArray(), Encoding.Latin1).Texts.Count;
                        slots.Add(bank, count);
                    }
                    if (!int.TryParse(Metadata("Source Ordinal"), NumberStyles.None, CultureInfo.InvariantCulture, out int ordinal) || ordinal >= count || row.Key.Split("_ctx_", 2, StringSplitOptions.None)[0] != FieldLocalizationKey.Create(source.Descriptor.Language, row.File, ordinal))
                    {
                        throw new InvalidDataException($"{row.Key}: projected MSD slot is outside its authenticated native bank.");
                    }
                    if (Metadata("JSM SHA256") is { Length: > 0 } script)
                    {
                        Check(field + ".jsm", script, row.Key);
                    }
                    continue;
                }
                bool information = known?.Metadata.GetValueOrDefault("Structure") == "information-body-v1";
                if (Metadata("Structure") == "information-body-v1" || information)
                {
                    if (!information || !NativeTutorialInformationText.ValidateTarget(known!.Text, LocalizationValue.RenderTarget(row.Target!)))
                    {
                        throw new InvalidDataException($"{row.Key}: translated Information text must preserve its original conditions, links and value operands.");
                    }
                }
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
