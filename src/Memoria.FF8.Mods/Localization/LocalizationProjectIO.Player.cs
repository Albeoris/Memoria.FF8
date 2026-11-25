using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public static partial class LocalizationProjectIO
{
    private const string PlayerSourceHash = "Source Text SHA256";
    private static readonly string[] PlayerBindingHashes = ["Source MSD SHA256", "JSM SHA256", "Source SHA256", "Native Source Fingerprint"];

    internal static string FingerprintPlayerSource(LocalizationRecord row, string? text)
    {
        string value = row.Category == "Fields" ? LocalizationNativeEdition.FieldComparisonText(text ?? "") : text ?? "";
        return System.Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    public static void ExportPlayer(string input, string output, CancellationToken cancellation = default)
    {
        LocalizationProject project = Read(input);
        if (project.Manifest.PlayerExport || project.Manifest.SourceEdition is null || project.Manifest.BaseProjectId is not null)
        {
            throw new InvalidDataException("Player export requires a standalone localization with a declared original edition.");
        }

        string destination = Path.GetFullPath(output);
        if (Directory.Exists(destination) || File.Exists(destination))
        {
            throw new IOException("Choose a new player localization export directory.");
        }
        string parent = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, ".localization-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(staging, "Localization"));
        try
        {
            LocalizationRecord[] records = project.Entries.Select(row =>
            {
                cancellation.ThrowIfCancellationRequested();
                return PlayerRecord(row);
            }).ToArray();
            var files = records.GroupBy(row => (row.Category, row.File)).OrderBy(group => group.Key.Category, StringComparer.Ordinal).ThenBy(group => group.Key.File, StringComparer.Ordinal).ToArray();
            LocalizationPayload[] payloads = files.Select(group => new LocalizationPayload
            {
                Path = $"Localization/{group.Key.Category}/{group.Key.File}.json",
                Category = group.Key.Category,
                File = group.Key.File
            }).ToArray();
            LocalizationManifest manifest = project.Manifest with
            {
                PlayerExport = true,
                Format = "json",
                Layout = "split",
                Payloads = payloads,
                ReferenceLanguages = new Dictionary<string, string>(),
                SourceReferenceLanguage = project.Manifest.FieldScriptBaseLanguage,
                ExtensionsPath = null,
                Artwork = [],
                ExtensionData = project.Manifest.ExtensionData.Where(pair => pair.Key is "contributors" or "translationVersion" or "packageName").ToDictionary(StringComparer.Ordinal)
            };
            Validate(new LocalizationProject { Manifest = manifest, Entries = records });
            for (int index = 0; index < payloads.Length; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                string path = Resolve(staging, payloads[index].Path);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, LocalizationProjectJson.Write(files[index].OrderBy(row => row.Key, StringComparer.Ordinal).ToArray(), manifest));
            }
            File.WriteAllBytes(Resolve(staging, ManifestRelativePath), JsonBytes(manifest, playerExport: true));
            Directory.Move(staging, destination);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
            }
        }
    }

    private static LocalizationRecord PlayerRecord(LocalizationRecord row)
    {
        Dictionary<string, JsonElement> metadata = new(StringComparer.Ordinal);
        if (row.ExtensionData.TryGetValue(LocalizationNativeEdition.TargetsMember, out JsonElement targets))
        {
            metadata.Add(LocalizationNativeEdition.TargetsMember, targets);
        }
        if (!string.IsNullOrEmpty(row.Target) || targets.ValueKind == JsonValueKind.Object)
        {
            metadata.Add(PlayerSourceHash, JsonSerializer.SerializeToElement(FingerprintPlayerSource(row, row.Source)));
            bool hasContext = row.ExtensionData.TryGetValue("sourceContext", out JsonElement source) && source.ValueKind == JsonValueKind.Object;
            if (row.ExtensionData.TryGetValue("Choice Line Map", out JsonElement choices) || hasContext && source.TryGetProperty("Choice Line Map", out choices))
            {
                metadata.Add("Choice Line Map", choices.ValueKind == JsonValueKind.String ? ParseChoiceMap(choices) : choices);
            }
            Dictionary<string, JsonElement> bindings = new(StringComparer.Ordinal);
            string[] names = row.Category == "Fields" ? ["Source MSD SHA256", "JSM SHA256"] : ["Native Source Fingerprint", "Source SHA256"];
            foreach (string name in names)
            {
                if (hasContext && source.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String && IsSha256(value.GetString()))
                {
                    bindings.Add(name, value);
                    if (row.Category != "Fields")
                    {
                        break;
                    }
                }
            }
            if (bindings.Count > 0)
            {
                metadata.Add("sourceContext", JsonSerializer.SerializeToElement(bindings));
            }
        }
        return new LocalizationRecord
        {
            Category = row.Category,
            File = row.File,
            Key = row.Key,
            SourcePresent = false,
            Target = row.Target,
            TargetPresent = row.TargetPresent,
            ExtensionData = metadata
        };
    }

    private static JsonElement ParseChoiceMap(JsonElement map)
    {
        using JsonDocument document = ParseJson(Encoding.UTF8.GetBytes(map.GetString()!));
        return document.RootElement.Clone();
    }

    internal static string? PlayerSourceFingerprint(LocalizationRecord row) => row.ExtensionData.TryGetValue(PlayerSourceHash, out JsonElement hash) ? hash.GetString() : null;

    private static bool IsSha256(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    private static void ValidatePlayerRecord(LocalizationRecord row)
    {
        if (row.SourcePresent || row.Source is not null || row.References.Count != 0 || row.ReferenceKeys.Count != 0 ||
            row.ExtensionData.Keys.Any(name => name is not (PlayerSourceHash or "sourceContext" or "Choice Line Map" or LocalizationNativeEdition.TargetsMember)))
        {
            throw new InvalidDataException($"{row.Key}: player localization contains authoring data.");
        }
        if (row.ExtensionData.TryGetValue(PlayerSourceHash, out JsonElement hash)
            ? hash.ValueKind != JsonValueKind.String || !IsSha256(hash.GetString())
            : row.ExtensionData.ContainsKey("sourceContext"))
        {
            throw new InvalidDataException($"{row.Key}: invalid player localization text fingerprint.");
        }
        if (row.ExtensionData.TryGetValue("sourceContext", out JsonElement context) &&
            (context.ValueKind != JsonValueKind.Object || context.EnumerateObject().Any(pair => !PlayerBindingHashes.Contains(pair.Name) || pair.Value.ValueKind != JsonValueKind.String || !IsSha256(pair.Value.GetString()))))
        {
            throw new InvalidDataException($"{row.Key}: invalid player localization binding fingerprints.");
        }
        if (row.ExtensionData.TryGetValue("Choice Line Map", out JsonElement map) &&
            (map.ValueKind != JsonValueKind.Array || map.EnumerateArray().Any(entry => entry.ValueKind != JsonValueKind.Object || entry.EnumerateObject().Count() != 2 ||
                !entry.TryGetProperty("SourceLine", out JsonElement source) || source.ValueKind != JsonValueKind.Number || !source.TryGetInt32(out _) ||
                !entry.TryGetProperty("TargetLine", out JsonElement target) || target.ValueKind != JsonValueKind.Number || !target.TryGetInt32(out _))))
        {
            throw new InvalidDataException($"{row.Key}: invalid player localization choice line map.");
        }
    }
}
