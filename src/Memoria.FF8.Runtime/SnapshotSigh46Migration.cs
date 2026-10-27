using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

/// <summary>Replaces the legacy uninitialized Sigh placeholder before constructing the current state.</summary>
internal static class SnapshotSigh46Migration
{
    internal static void Apply(JsonObject saved, JsonSerializerOptions options)
    {
        JsonObject?[] effects =
        [
            Find(saved, "NativeBattleMemory", "EffectMemory"),
            Find(saved, "EventState", "Scene", "NativeEyeProcess", "Memory", "EffectMemory"),
            Find(saved, "DepartingFieldState", "Scene", "NativeEyeProcess", "Memory", "EffectMemory"),
            Find(saved, "Battle", "NativeEffectMemory"),
            Find(saved, "Battle", "Definition", "NativeRetainedMemory", "EffectMemory")
        ];
        if (HasInitializedSigh(Find(saved, "Battle", "NativeGraphics", "OriginalEffect")) || effects.Any(memory => HasInitializedSigh(memory?["DetachedReturnedTask"] as JsonObject)))
        {
            return;
        }

        // Event checkpoints use a separate reader which expects byte arrays rather than the root's compact encoding.
        JsonSerializerOptions initialOptions = new(options);
        foreach (ImmutableBytesJsonConverter converter in initialOptions.Converters.OfType<ImmutableBytesJsonConverter>().ToArray())
        {
            initialOptions.Converters.Remove(converter);
        }
        JsonNode unknown = Serialize(NativeBattleSigh46State.FromUnknown());
        JsonNode cold = Serialize(NativeBattleSigh46State.FromColdProcess());
        foreach (JsonObject? memory in effects)
        {
            if (memory is null)
            {
                continue;
            }
            if (!memory.TryGetPropertyValue(nameof(NativeBattleEffectMemory.Sigh46), out JsonNode? current))
            {
                memory[nameof(NativeBattleEffectMemory.Sigh46)] = cold.DeepClone();
            }
            else if (current is JsonObject owner && IsLegacyPlaceholder(owner))
            {
                memory[nameof(NativeBattleEffectMemory.Sigh46)] = cold.DeepClone();
            }
        }

        JsonNode Serialize(NativeBattleSigh46State state) => JsonSerializer.SerializeToNode(NativeBattleStorageSnapshots.Capture(state), initialOptions)!;

        bool IsLegacyPlaceholder(JsonObject owner)
        {
            // Missing required fields and partial owners remain invalid; never complete them into a cold-looking state.
            string[] properties = [nameof(NativeBattleSigh46State.Globals), nameof(NativeBattleSigh46State.SourceHeader), nameof(NativeBattleSigh46State.Channels), nameof(NativeBattleSigh46State.MutablePe), nameof(NativeBattleSigh46State.InactiveBanks), nameof(NativeBattleSigh46State.SourcesLoaded)];
            if (owner.Count != properties.Length || properties.Any(name => !owner.ContainsKey(name)) || owner[nameof(NativeBattleSigh46State.SourcesLoaded)] is not null)
            {
                return false;
            }
            NativeBattleSigh46State state = owner.Deserialize<NativeBattleSigh46State>(options)!;
            return JsonNode.DeepEquals(Serialize(state), unknown);
        }
    }

    private static bool HasInitializedSigh(JsonObject? effect)
    {
        if (effect is null || !IsNumber(effect["RequestedEffect"], 46) && !IsNumber(effect["Binding"]?["Entry"], NativeBattleSigh46Profile.Entry))
        {
            return false;
        }
        return effect["Pools"] is not JsonArray { Count: 0 } || !effect.TryGetPropertyValue("ReturnedDescriptor", out JsonNode? descriptor) || descriptor is not null && !IsNumber(descriptor, 0);
    }

    private static bool IsNumber(JsonNode? node, long expected) => node is JsonValue value && value.GetValueKind() == JsonValueKind.Number && value.Deserialize<long>() == expected;

    private static JsonObject? Find(JsonObject saved, params string[] path)
    {
        JsonObject current = saved;
        foreach (string name in path)
        {
            if (current[name] is not JsonObject child)
            {
                return null;
            }
            current = child;
        }
        return current;
    }
}
