using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

/// <summary>Completes only Angelo fields omitted by historical writers, before constructing the current state model.</summary>
internal static class SnapshotAngeloMigration
{
    private static readonly string[] Properties = [nameof(NativeLimitProgress.AngeloFlags), nameof(NativeLimitProgress.TrainingSelection), nameof(NativeLimitProgress.TrainingCounters), nameof(NativeLimitProgress.PetPalsFlags)];

    internal static void Apply(JsonObject saved, JsonSerializerOptions options, int originalVersion, Func<NativeLimitProgress>? initialize)
    {
        if (originalVersion > 8 || initialize is null || saved[nameof(GameState.Battle)] is not null)
        {
            return;
        }
        JsonObject? root = saved[nameof(GameState.LimitProgress)] as JsonObject;
        JsonObject? current = saved[nameof(GameState.EventState)]?[nameof(GameState.LimitProgress)] as JsonObject;
        JsonObject? departing = saved[nameof(GameState.DepartingFieldState)]?[nameof(GameState.LimitProgress)] as JsonObject;
        JsonObject[] owners = new[] { root, current, departing }.OfType<JsonObject>().ToArray();
        if (!owners.Any(owner => Properties.Any(name => !owner.ContainsKey(name))))
        {
            return;
        }
        bool currentField = saved[nameof(GameState.Scene)] is JsonValue scene && (scene.TryGetValue(out string? text) && text == nameof(SceneKind.Field) || scene.TryGetValue(out int number) && number == (int)SceneKind.Field);
        List<(JsonObject Owner, string Name, JsonNode? Value)> additions = [];
        JsonObject? baseline = null;
        foreach (JsonObject owner in owners)
        {
            foreach (string name in Properties)
            {
                if (owner.ContainsKey(name))
                {
                    continue;
                }
                JsonObject? mirror = null;
                if (currentField && ReferenceEquals(owner, root))
                {
                    mirror = current;
                }
                else if (currentField && ReferenceEquals(owner, current))
                {
                    mirror = root;
                }
                if (mirror is not null && mirror.TryGetPropertyValue(name, out JsonNode? represented))
                {
                    additions.Add((owner, name, CopyProperty(name, represented, !ReferenceEquals(owner, root))));
                    continue;
                }
                if (baseline is null)
                {
                    NativeLimitProgress initial = initialize();
                    initial.Validate();
                    if (initial.TrainingSelection >= 8 || initial.PetPalsFlags is null || (initial.AngeloFlags & 0x10) == 0)
                    {
                        throw new InvalidDataException("Legacy Angelo initialization requires a usable initial finisher, availability and training selection.");
                    }
                    baseline = JsonSerializer.SerializeToNode(initial, options)!.AsObject();
                }
                if (owner[nameof(NativeLimitProgress.InitOutSha256)] is not JsonValue hash || !hash.TryGetValue(out string? savedHash) || !string.Equals(savedHash, baseline[nameof(NativeLimitProgress.InitOutSha256)]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Legacy Angelo initialization requires the saved Limit owner's selected initial template.");
                }
                additions.Add((owner, name, CopyProperty(name, baseline[name], !ReferenceEquals(owner, root))));
            }
        }
        foreach ((JsonObject owner, string name, JsonNode? value) in additions)
        {
            owner.Add(name, value);
        }
    }

    private static JsonNode? CopyProperty(string name, JsonNode? value, bool embedded)
    {
        if (embedded && name == nameof(NativeLimitProgress.TrainingCounters) && value is JsonValue scalar && scalar.TryGetValue(out string? encoded))
        {
            byte[] bytes = JsonSerializer.Deserialize<byte[]>(JsonSerializer.Serialize(encoded))!;
            return new JsonArray(bytes.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        }
        return value?.DeepClone();
    }
}
