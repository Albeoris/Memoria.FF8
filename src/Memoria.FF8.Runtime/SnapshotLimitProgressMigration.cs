using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

/// <summary>Completes omitted historical Limit features from current saved owners, an original save, or the selected initial template.</summary>
internal static class SnapshotLimitProgressMigration
{
    private static readonly string[] Properties = [nameof(NativeLimitProgress.InitOutSha256), nameof(NativeLimitProgress.BlueMagicFlags), nameof(NativeLimitProgress.DuelFlags), nameof(NativeLimitProgress.AngeloFlags), nameof(NativeLimitProgress.TrainingSelection), nameof(NativeLimitProgress.TrainingCounters), nameof(NativeLimitProgress.SpecialMagicFlags), nameof(NativeLimitProgress.PetPalsFlags), nameof(NativeLimitProgress.Options978)];

    internal static bool Apply(JsonObject saved, JsonSerializerOptions options, int originalVersion, Func<NativeLimitProgress>? initialize)
    {
        if (originalVersion > 8 || saved[nameof(GameState.Battle)] is not null)
        {
            return false;
        }
        SceneKind? scene = ReadScene(saved);
        JsonObject? currentCheckpoint = scene is SceneKind.Field or SceneKind.Movie ? CurrentCheckpoint(saved) : null;
        List<(JsonObject Parent, JsonNode? Value)> newOwners = [];
        JsonObject? root = ReadOwner(saved, currentCheckpoint, scene is SceneKind.Field or SceneKind.Movie or SceneKind.WorldMap, initialize is not null || saved[nameof(GameState.OriginalSave)] is JsonObject, newOwners);
        JsonObject? current = currentCheckpoint is null ? null : ReadOwner(currentCheckpoint, saved, true, root is not null, newOwners);
        JsonObject? frozenCurrent = currentCheckpoint is null ? (saved[nameof(GameState.EventState)] as JsonObject)?[nameof(GameState.LimitProgress)] as JsonObject : null;
        JsonObject? departing = (saved[nameof(GameState.DepartingFieldState)] as JsonObject)?[nameof(GameState.LimitProgress)] as JsonObject;
        JsonObject[] owners = new[] { root, current, frozenCurrent, departing }.OfType<JsonObject>().Distinct().ToArray();
        List<(JsonObject Owner, string Name, JsonNode? Value)> additions = [];
        JsonObject? baseline = null;
        JsonObject? original = null;
        foreach (JsonObject owner in owners)
        {
            bool canonical = ReferenceEquals(owner, root) || ReferenceEquals(owner, current);
            JsonObject? counterpart = ReferenceEquals(owner, root) ? current : ReferenceEquals(owner, current) ? root : null;
            JsonNode? identity = owner[nameof(NativeLimitProgress.InitOutSha256)];
            foreach (string name in Properties)
            {
                if (owner.ContainsKey(name))
                {
                    continue;
                }
                JsonNode? value;
                if (counterpart is not null && counterpart.TryGetPropertyValue(name, out value))
                {
                    ValidateMatchingOwners(identity, counterpart);
                }
                else if (name == nameof(NativeLimitProgress.InitOutSha256) && TryCharacterHash(ReferenceEquals(owner, current) ? currentCheckpoint! : saved, saved, out value))
                {
                    // Initialization identity is resource metadata, independently of saved feature progress.
                }
                else if (canonical && TryOriginalProperty(saved, options, name, ref original, out value))
                {
                    // The retained import image supplies a recorded baseline, not proof of current progress.
                }
                else
                {
                    if (initialize is null)
                    {
                        continue;
                    }
                    if (baseline is null)
                    {
                        NativeLimitProgress initial = initialize();
                        initial.Validate();
                        if (initial.TrainingSelection >= 8 || initial.PetPalsFlags is null || initial.SpecialMagicFlags is null || initial.Options978 is null || (initial.AngeloFlags & 0x10) == 0)
                        {
                            throw new InvalidDataException("Legacy Limit initialization requires complete initial feature flags, a usable finisher and training selection.");
                        }
                        baseline = JsonSerializer.SerializeToNode(initial, options)!.AsObject();
                    }
                    // Current definitions supply only absent feature values; saved owners retain their initialization identity.
                    value = baseline[name];
                }
                if (name == nameof(NativeLimitProgress.InitOutSha256))
                {
                    identity = value;
                }
                additions.Add((owner, name, CopyProperty(name, value, !ReferenceEquals(owner, root))));
            }
        }
        foreach ((JsonObject owner, string name, JsonNode? value) in additions)
        {
            owner.Add(name, value);
        }
        foreach ((JsonObject parent, JsonNode? value) in newOwners)
        {
            parent[nameof(GameState.LimitProgress)] = value;
        }
        return owners.Any(owner => Properties.Any(name => !owner.ContainsKey(name))) || root is null && scene is SceneKind.Field or SceneKind.Movie or SceneKind.WorldMap;
    }

    private static JsonObject? ReadOwner(JsonObject parent, JsonObject? counterpart, bool active, bool canInitialize, List<(JsonObject Parent, JsonNode? Value)> additions)
    {
        if (parent.TryGetPropertyValue(nameof(GameState.LimitProgress), out JsonNode? present))
        {
            if (present is JsonObject represented)
            {
                return represented;
            }
            if (present is not null)
            {
                return null;
            }
            // Historical nullable writers used a whole null owner for an uninitialized feature.
        }
        if (!active)
        {
            return null;
        }
        if (counterpart is not null && counterpart.TryGetPropertyValue(nameof(GameState.LimitProgress), out JsonNode? mirrored))
        {
            if (mirrored is not JsonObject)
            {
                if (mirrored is not null || !canInitialize)
                {
                    return null;
                }
            }
        }
        else if (!canInitialize)
        {
            return null;
        }
        JsonObject owner = new();
        additions.Add((parent, owner));
        return owner;
    }

    private static bool TryCharacterHash(JsonObject owner, JsonObject root, out JsonNode? value)
    {
        JsonObject? characters = owner[nameof(GameState.CharacterVitals)] as JsonObject ?? root[nameof(GameState.CharacterVitals)] as JsonObject;
        value = null;
        return characters is not null && characters.TryGetPropertyValue(nameof(NativeLimitProgress.InitOutSha256), out value);
    }

    private static bool TryOriginalProperty(JsonObject saved, JsonSerializerOptions options, string name, ref JsonObject? original, out JsonNode? value)
    {
        value = null;
        if (saved[nameof(GameState.OriginalSave)] is not JsonObject image || name == nameof(NativeLimitProgress.InitOutSha256))
        {
            return false;
        }
        if (original is null)
        {
            NativePcSaveData source = image.Deserialize<NativePcSaveData>(options) ?? throw new InvalidDataException("Empty original save owner.");
            source.Validate();
            byte[] block = source.Block;
            byte B(int offset) => block[NativePcSaveData.MainOffset + offset];
            ushort W(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(NativePcSaveData.MainOffset + offset));
            original = new JsonObject
            {
                [nameof(NativeLimitProgress.BlueMagicFlags)] = W(2756),
                [nameof(NativeLimitProgress.DuelFlags)] = W(2758),
                [nameof(NativeLimitProgress.SpecialMagicFlags)] = B(2761),
                [nameof(NativeLimitProgress.AngeloFlags)] = B(2762),
                [nameof(NativeLimitProgress.PetPalsFlags)] = B(2763),
                [nameof(NativeLimitProgress.TrainingSelection)] = B(2747),
                [nameof(NativeLimitProgress.TrainingCounters)] = new JsonArray(block.AsSpan(NativePcSaveData.MainOffset + 2764, 8).ToArray().Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
                [nameof(NativeLimitProgress.Options978)] = W(3280)
            };
        }
        return original.TryGetPropertyValue(name, out value);
    }

    private static void ValidateMatchingOwners(JsonNode? targetIdentity, JsonObject source)
    {
        if (targetIdentity is JsonValue targetValue && targetValue.TryGetValue(out string? targetHash) && source[nameof(NativeLimitProgress.InitOutSha256)] is JsonValue sourceValue && sourceValue.TryGetValue(out string? sourceHash) && !string.Equals(targetHash, sourceHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Saved Limit owners disagree about their initialization identity.");
        }
    }

    private static SceneKind? ReadScene(JsonObject saved)
    {
        if (saved[nameof(GameState.Scene)] is JsonValue scene)
        {
            if (scene.TryGetValue(out string? name) && Enum.TryParse(name, out SceneKind kind))
            {
                return kind;
            }
            if (scene.TryGetValue(out int number) && Enum.IsDefined((SceneKind)number))
            {
                return (SceneKind)number;
            }
        }
        return null;
    }

    private static JsonObject? CurrentCheckpoint(JsonObject saved)
    {
        if (saved[nameof(GameState.EventState)] is JsonObject current)
        {
            return current;
        }
        if (saved[nameof(GameState.DepartingFieldState)] is JsonObject pending && pending["TransitionRequested"] is JsonValue requested && requested.TryGetValue(out bool transition) && transition && pending["Scene"] is JsonObject scene && scene.TryGetPropertyValue("WorldTransitionRequest", out JsonNode? world) && world is null)
        {
            return pending;
        }
        return null;
    }

    private static JsonNode? CopyProperty(string name, JsonNode? value, bool embedded)
    {
        if (embedded && name == nameof(NativeLimitProgress.TrainingCounters) && value is JsonValue scalar && scalar.TryGetValue<string>(out _))
        {
            byte[] bytes = scalar.Deserialize<byte[]>()!;
            return new JsonArray(bytes.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        }
        return value?.DeepClone();
    }
}
