using System.Globalization;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

/// <summary>Converts historical copied model rows to logical translation state before deserialization.</summary>
internal static class SnapshotModelPartMigration
{
    internal static void Apply(JsonObject saved)
    {
        List<(JsonObject Owner, string Key, JsonObject Replacement)> replacements = [];
        foreach (string owner in new[] { nameof(GameState.EventState), nameof(GameState.DepartingFieldState) })
        {
            if (saved[owner] is JsonObject events && events["Scene"] is JsonObject scene && scene["ModelParts"] is JsonObject bank)
            {
                Collect(bank, owner + ".Scene.ModelParts", replacements);
            }
        }
        foreach ((JsonObject owner, string key, JsonObject replacement) in replacements)
        {
            owner[key] = replacement;
        }
    }

    private static void Collect(JsonObject bank, string path, List<(JsonObject Owner, string Key, JsonObject Replacement)> replacements)
    {
        if (bank["Resources"] is JsonObject resources)
        {
            foreach ((string model, JsonNode? node) in resources)
            {
                if (node is not JsonObject resource || !new[] { "SourceHash", "SourceOffset", "Parts", "SkinFlags" }.Any(resource.ContainsKey))
                {
                    continue;
                }
                string address = path + ".Resources[" + model + "]";
                RequireKeys(resource, address, "SourceHash", "SourceOffset", "Parts", "SkinFlags");
                if (resource["SourceHash"] is not JsonValue hashValue || !hashValue.TryGetValue(out string? hash) || hash is null || hash.Length != 64 || !hash.All(Uri.IsHexDigit))
                {
                    throw new InvalidDataException(address + ": invalid legacy resource provenance.");
                }
                ReadInteger(resource["SourceOffset"], address + ".SourceOffset", 0, int.MaxValue);
                if (resource["Parts"] is not JsonArray rows || rows.Count > 1000 || resource["SkinFlags"] is not JsonArray flags || flags.Count > 1000)
                {
                    throw new InvalidDataException(address + ": legacy model tables must be bounded arrays.");
                }
                foreach (JsonNode? flag in flags)
                {
                    ReadInteger(flag, address + ".SkinFlags", ushort.MinValue, ushort.MaxValue);
                }
                JsonObject translations = new();
                for (int part = 0; part < rows.Count; part++)
                {
                    string rowAddress = address + ".Parts[" + part + "]";
                    if (rows[part] is not JsonArray { Count: 16 } row)
                    {
                        throw new InvalidDataException(rowAddress + ": expected sixteen signed native WORDs.");
                    }
                    int[] words = row.Select(word => ReadInteger(word, rowAddress, short.MinValue, short.MaxValue)).ToArray();
                    if (words[0] < 0 || words[1] < 0 || words[0] + words[1] > flags.Count || words[7] is not (0 or 1 or 4))
                    {
                        throw new InvalidDataException(rowAddress + ": invalid skin span or unsupported translation mode.");
                    }
                    for (int skin = words[0]; skin < words[0] + words[1]; skin++)
                    {
                        if (ReadInteger(flags[skin], address + ".SkinFlags", ushort.MinValue, ushort.MaxValue) != 0)
                        {
                            throw new InvalidDataException(rowAddress + ": selected legacy skin flags must retain their registration clears.");
                        }
                    }
                    if (words[7] == 0)
                    {
                        continue;
                    }
                    if (words[15] == 0)
                    {
                        throw new InvalidDataException(rowAddress + ": translated logical state requires a nonzero duration.");
                    }
                    translations[part.ToString(CultureInfo.InvariantCulture)] = new JsonObject
                    {
                        ["Counter"] = words[2],
                        ["StartX"] = words[4],
                        ["StartY"] = words[5],
                        ["StartZ"] = words[6],
                        ["TargetX"] = words[12],
                        ["TargetY"] = words[13],
                        ["TargetZ"] = words[14],
                        ["Duration"] = words[15],
                        ["Mode"] = words[7]
                    };
                }
                replacements.Add((resources, model, new JsonObject { ["Translations"] = translations }));
            }
        }
        if (bank["Draws"] is JsonObject draws)
        {
            foreach ((string actor, JsonNode? node) in draws)
            {
                if (node is not JsonObject draw || !draw.ContainsKey("SkinTranslations"))
                {
                    continue;
                }
                string address = path + ".Draws[" + actor + "]";
                RequireKeys(draw, address, "Frame", "ModelId", "SkinTranslations");
                if (draw["Frame"] is not JsonValue frameValue || !frameValue.TryGetValue(out long frame) || frame < 0)
                {
                    throw new InvalidDataException(address + ": invalid legacy draw frame.");
                }
                ReadInteger(draw["ModelId"], address + ".ModelId", 0, int.MaxValue);
                if (draw["SkinTranslations"] is not JsonObject { Count: 0 })
                {
                    throw new InvalidDataException(address + ": cannot migrate a nonempty legacy skin sample because its pre-callback logical sample was not saved. Shared instances, completion and later commands prevent reliable reconstruction; use a compatible save or an explicit evidence-backed migration.");
                }
                replacements.Add((draws, actor, new JsonObject
                {
                    ["Frame"] = frame,
                    ["ModelId"] = draw["ModelId"]!.DeepClone(),
                    ["SampleState"] = new JsonObject { ["Translations"] = new JsonObject() }
                }));
            }
        }
    }

    private static int ReadInteger(JsonNode? node, string path, int minimum, int maximum)
    {
        if (node is not JsonValue value || !value.TryGetValue(out int number) || number < minimum || number > maximum)
        {
            throw new InvalidDataException(path + ": expected an integer in the supported native range.");
        }
        return number;
    }

    private static void RequireKeys(JsonObject value, string path, params string[] keys)
    {
        if (value.Count != keys.Length || keys.Any(key => !value.ContainsKey(key)))
        {
            throw new InvalidDataException(path + ": expected the exact legacy model-part schema.");
        }
    }
}
