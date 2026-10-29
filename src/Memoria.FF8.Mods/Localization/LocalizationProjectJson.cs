using System.Text.Json;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

internal static class LocalizationProjectJson
{
    public static IReadOnlyList<LocalizationRecord> Read(byte[] bytes, LocalizationManifest manifest, LocalizationPayload payload)
    {
        using JsonDocument document = LocalizationProjectIO.ParseJson(bytes);
        RequireObject(document.RootElement, "payload");
        List<LocalizationRecord> result = [];
        foreach (JsonProperty category in document.RootElement.EnumerateObject())
        {
            RequireObject(category.Value, category.Name);
            foreach (JsonProperty file in category.Value.EnumerateObject())
            {
                RequireObject(file.Value, file.Name);
                if ((payload.Category is not null && payload.Category != category.Name) || (payload.File is not null && payload.File != file.Name))
                {
                    throw new InvalidDataException($"Payload {payload.Path} contains unrelated category/file {category.Name}/{file.Name}.");
                }
                foreach (JsonProperty row in file.Value.EnumerateObject())
                {
                    RequireObject(row.Value, row.Name);
                    Dictionary<string, JsonElement> values = row.Value.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
                    JsonElement source = default;
                    bool present = manifest.SchemaVersion == 1 && values.Remove(manifest.SourceColumn, out source);
                    bool targetPresent = values.Remove(manifest.TargetColumn, out JsonElement target);
                    Dictionary<string, string?> references = new(StringComparer.Ordinal);
                    Dictionary<string, bool> referencePresence = new(StringComparer.Ordinal);
                    Dictionary<string, IReadOnlyList<string>> referenceKeys = new(StringComparer.Ordinal);
                    foreach (string label in manifest.ReferenceLanguages.Keys)
                    {
                        referencePresence[label] = values.Remove(label, out JsonElement reference);
                        if (referencePresence[label])
                        {
                            references[label] = Text(reference, row.Name, label);
                        }
                    }
                    if (manifest.SchemaVersion >= 2)
                    {
                        foreach (string label in manifest.ReferenceLanguages.Keys)
                        {
                            string keyLabel = "ReferenceKey " + label["Reference ".Length..];
                            if (!values.Remove(keyLabel, out JsonElement keys))
                            {
                                throw new InvalidDataException("Missing reference-key property: " + row.Name + "/" + keyLabel);
                            }
                            referenceKeys[label] = keys.ValueKind switch
                            {
                                JsonValueKind.Null => [],
                                JsonValueKind.String => string.IsNullOrEmpty(keys.GetString()) ? [] : [keys.GetString()!],
                                JsonValueKind.Array when keys.EnumerateArray().All(key => key.ValueKind == JsonValueKind.String) => keys.EnumerateArray().Select(key => key.GetString()!).ToArray(),
                                _ => throw new InvalidDataException("Reference keys must be a key, an array of keys or null: " + row.Name)
                            };
                        }
                    }
                    result.Add(new LocalizationRecord
                    {
                        Category = category.Name,
                        File = file.Name,
                        Key = row.Name,
                        Source = Text(source, row.Name, manifest.SourceColumn),
                        SourcePresent = present,
                        Target = Text(target, row.Name, manifest.TargetColumn),
                        TargetPresent = targetPresent,
                        References = references,
                        ReferencePresence = referencePresence,
                        ReferenceKeys = referenceKeys,
                        ExtensionData = values.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal)
                    });
                }
            }
        }
        return result;
    }

    public static byte[] Write(IReadOnlyList<LocalizationRecord> rows, LocalizationManifest manifest)
    {
        Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, object?>>>> categories = new(StringComparer.Ordinal);
        foreach (LocalizationRecord row in rows)
        {
            if (!categories.TryGetValue(row.Category, out Dictionary<string, Dictionary<string, Dictionary<string, object?>>>? files))
            {
                files = new(StringComparer.Ordinal);
                categories.Add(row.Category, files);
            }
            if (!files.TryGetValue(row.File, out Dictionary<string, Dictionary<string, object?>>? entries))
            {
                entries = new(StringComparer.Ordinal);
                files.Add(row.File, entries);
            }
            Dictionary<string, object?> values = new(StringComparer.Ordinal);
            if (manifest.SchemaVersion == 1 && (row.SourcePresent || !string.IsNullOrEmpty(row.Source)))
            {
                values.Add(manifest.SourceColumn, row.Source);
            }
            if (row.TargetPresent)
            {
                values.Add(manifest.TargetColumn, row.Target);
            }
            foreach (string label in manifest.ReferenceLanguages.Keys)
            {
                if (!string.IsNullOrEmpty(row.References.GetValueOrDefault(label)) || (row.ReferencePresence.TryGetValue(label, out bool present) ? present : row.References.ContainsKey(label)))
                {
                    values.Add(label, row.References.GetValueOrDefault(label));
                }
            }
            if (manifest.SchemaVersion >= 2)
            {
                foreach (string label in manifest.ReferenceLanguages.Keys)
                {
                    IReadOnlyList<string> keys = row.ReferenceKeys.GetValueOrDefault(label) ?? [];
                    values.Add("ReferenceKey " + label["Reference ".Length..], keys.Count == 1 ? keys[0] : keys.Count == 0 ? null : keys);
                }
            }
            foreach ((string key, JsonElement value) in row.ExtensionData.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (manifest.SchemaVersion == 1 || key == "Field Name")
                {
                    values.Add(key, value);
                }
            }
            entries.Add(row.Key, values);
        }
        return LocalizationProjectIO.JsonBytes(categories);
    }

    private static string? Text(JsonElement value, string key, string label)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"{key}: role {label} must be a string or null.");
        }
        return value.GetString();
    }

    private static void RequireObject(JsonElement value, string identity)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"Localization JSON {identity} must be an object.");
        }
    }
}
