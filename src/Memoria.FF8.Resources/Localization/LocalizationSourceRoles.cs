using System.Text.Json;

namespace Memoria.FF8.Resources.Localization;

public sealed record LocalizationSourceRole(string Language, bool Inferred, string Explanation);
public static class LocalizationSourceRoles
{
    public static LocalizationSourceRole Resolve(LocalizationManifest manifest)
    {
        string? recovered = null;
        if (manifest.ExtensionData.TryGetValue("sourceDatasets", out JsonElement datasets) && datasets.ValueKind == JsonValueKind.Array)
        {
            string[] primary = datasets.EnumerateArray().Where(item => item.TryGetProperty("PrimaryReference", out JsonElement value) && value.ValueKind == JsonValueKind.True || item.TryGetProperty("primaryReference", out value) && value.ValueKind == JsonValueKind.True).Select(item => item.TryGetProperty("Id", out JsonElement id) ? id.GetString() : item.TryGetProperty("id", out id) ? id.GetString() : null).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (primary.Length == 1)
            {
                recovered = primary[0];
            }
            else if (primary.Length > 1)
            {
                throw new InvalidDataException("sourceDatasets declares multiple primary Source References.");
            }
        }
        if (!string.IsNullOrWhiteSpace(manifest.SourceReferenceLanguage))
        {
            if (!manifest.ReferenceLanguages.Values.Contains(manifest.SourceReferenceLanguage, StringComparer.OrdinalIgnoreCase) && !manifest.FieldScriptBaseLanguage.Equals(manifest.SourceReferenceLanguage, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The explicit Source Reference must identify a declared reference or the script-base dataset.");
            }
            if (recovered is not null && !recovered.Equals(manifest.SourceReferenceLanguage, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Explicit Source Reference contradicts sourceDatasets PrimaryReference.");
            }
            return new(manifest.SourceReferenceLanguage, false, "Explicit Source Reference");
        }
        if (recovered is not null)
        {
            return new(recovered, true, "Recovered sourceDatasets PrimaryReference");
        }
        string language = !string.IsNullOrWhiteSpace(manifest.SourceLanguage) ? manifest.SourceLanguage : manifest.ReferenceLanguages.Values.FirstOrDefault() ?? manifest.FieldScriptBaseLanguage;
        return new(language, true, "Legacy Source metadata; otherwise first reference, then script base");
    }

    public static (string Text, string Language) Seed(LocalizationManifest manifest, LocalizationRecord row)
    {
        string primary = Resolve(manifest).Language;
        foreach (string language in new[] { primary, manifest.FieldScriptBaseLanguage }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string? label = manifest.ReferenceLanguages.FirstOrDefault(pair => pair.Value.Equals(language, StringComparison.OrdinalIgnoreCase)).Key;
            bool referencePresent = label is not null && (row.ReferencePresence.TryGetValue(label, out bool declared) ? declared : row.References.ContainsKey(label));
            if (referencePresent && !string.IsNullOrEmpty(row.References.GetValueOrDefault(label!)))
            {
                return (row.References[label!]!, language);
            }
            if (row.SourcePresent && manifest.SourceLanguage.Equals(language, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(row.Source))
            {
                return (row.Source, language);
            }
        }
        throw new InvalidOperationException("No designated Source Reference or script-base body is available.");
    }
}
