using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

/// <summary>Binds a rendered native reading to the package's original source text fingerprint.</summary>
public static class LocalizationReferenceProvenance
{
    public const string ManifestMember = "referenceTextFingerprints";

    public static bool MatchesSource(LocalizationManifest manifest, LocalizationRecord row, string label, string reading)
    {
        string? sourceHash = SourceHash(row);
        return sourceHash is not null &&
            manifest.ExtensionData.TryGetValue(ManifestMember, out JsonElement references) && references.ValueKind == JsonValueKind.Object &&
            references.TryGetProperty(label, out JsonElement rows) && rows.ValueKind == JsonValueKind.Object &&
            rows.TryGetProperty(row.Key, out JsonElement binding) && binding.ValueKind == JsonValueKind.Array && binding.GetArrayLength() == 2 &&
            MatchesHash(binding[0], sourceHash) && MatchesHash(binding[1], ReadingHash(reading));
    }

    internal static string[]? CreateBinding(LocalizationRecord row, string nativeText, string reading)
    {
        string? sourceHash = SourceHash(row);
        if (sourceHash is null || !sourceHash.Equals(LocalizationProjectIO.FingerprintPlayerSource(row, nativeText), StringComparison.OrdinalIgnoreCase) ||
            sourceHash.Equals(LocalizationProjectIO.FingerprintPlayerSource(row, reading), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return [sourceHash, ReadingHash(reading)];
    }

    internal static string? SourceHash(LocalizationRecord row) => LocalizationProjectIO.PlayerSourceFingerprint(row) ??
        (row.SourcePresent && row.Source is not null ? LocalizationProjectIO.FingerprintPlayerSource(row, row.Source) : null);

    private static string ReadingHash(string reading) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reading)));

    private static bool MatchesHash(JsonElement value, string hash) => value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), hash, StringComparison.OrdinalIgnoreCase);
}
