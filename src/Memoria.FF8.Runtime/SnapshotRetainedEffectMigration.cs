using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

/// <summary>Upgrades complete legacy placeholders while preserving retained effect execution and source history.</summary>
internal static class SnapshotRetainedEffectMigration
{
    private sealed record Rule(string Owner, ushort Effect, uint Entry, Func<JsonSerializerOptions, JsonNode> Cold, Func<JsonObject, JsonSerializerOptions, JsonSerializerOptions, bool> IsPlaceholder);

    private static readonly Rule[] Rules =
    [
        Create<NativeBattleOilBlast137State>(nameof(NativeBattleEffectMemory.OilBlast137), 137, NativeBattleOilBlast137Profile.Entry, NativeBattleOilBlast137State.FromColdProcess, NativeBattleOilBlast137State.FromUnknown),
        Create<NativeBattleBadBreath131State>(nameof(NativeBattleEffectMemory.BadBreath131), 131, NativeBattleBadBreath131Profile.Entry, NativeBattleBadBreath131State.FromColdProcess, NativeBattleBadBreath131State.FromUnknown),
        Create<NativeBattleSaliva138State>(nameof(NativeBattleEffectMemory.Saliva138), 138, NativeBattleSaliva138Profile.Entry, NativeBattleSaliva138State.FromColdProcess, NativeBattleSaliva138State.FromUnknown),
        Create<NativeBattleMagmaBreath48State>(nameof(NativeBattleEffectMemory.MagmaBreath48), 48, NativeBattleMagmaBreath48Profile.Entry, NativeBattleMagmaBreath48State.FromColdProcess, NativeBattleMagmaBreath48State.FromUnknown),
        Create<NativeBattleBladeShot12State>(nameof(NativeBattleEffectMemory.BladeShot12), 12, NativeBattleBladeShot12Profile.Entry, NativeBattleBladeShot12State.FromColdProcess, NativeBattleBladeShot12State.FromUnknown),
        Create<NativeBattleOilShot136State>(nameof(NativeBattleEffectMemory.OilShot136), 136, NativeBattleOilShot136Profile.Entry, NativeBattleOilShot136State.FromColdProcess, NativeBattleOilShot136State.FromUnknown),
        Create<NativeBattleElectricDischarge126State>(nameof(NativeBattleEffectMemory.ElectricDischarge126), 126, NativeBattleElectricDischarge126Profile.Entry, NativeBattleElectricDischarge126State.FromColdProcess, NativeBattleElectricDischarge126State.FromUnknown),
        Create<NativeBattleEffect213State>(nameof(NativeBattleEffectMemory.Effect213), 213, NativeBattleEffect213Profile.Entry, NativeBattleEffect213State.FromColdProcess, NativeBattleEffect213State.FromUnknown),
        Create<NativeBattleEffect301State>(nameof(NativeBattleEffectMemory.Effect301), 301, NativeBattleEffect301Profile.Entry, NativeBattleEffect301State.FromColdProcess, NativeBattleEffect301State.FromUnknown),
        Create<NativeBattleEffect290State>(nameof(NativeBattleEffectMemory.Effect290), 290, NativeBattleEffect290Profile.Entry, NativeBattleEffect290State.FromColdProcess, NativeBattleEffect290State.FromUnknown),
        Create<NativeBattleEffect54State>(nameof(NativeBattleEffectMemory.Effect54), 54, NativeBattleEffect54Profile.Entry, NativeBattleEffect54State.FromColdProcess, NativeBattleEffect54State.FromUnknown),
        Create<NativeBattleEffect320State>(nameof(NativeBattleEffectMemory.Effect320), 320, NativeBattleEffect320Profile.Entry, NativeBattleEffect320State.FromColdProcess, NativeBattleEffect320State.FromUnknown),
        Create<NativeBattleEffect308State>(nameof(NativeBattleEffectMemory.Effect308), 308, NativeBattleEffect308Profile.Entry, NativeBattleEffect308State.FromColdProcess, NativeBattleEffect308State.FromUnknown),
        Create<NativeBattleEffect251State>(nameof(NativeBattleEffectMemory.Effect251), 251, NativeBattleEffect251Profile.Entry, NativeBattleEffect251State.FromColdProcess, NativeBattleEffect251State.FromUnknown),
        Create<NativeBattleEffect83State>(nameof(NativeBattleEffectMemory.Effect83), 83, NativeBattleEffect83Profile.Entry, NativeBattleEffect83State.FromColdProcess, NativeBattleEffect83State.FromUnknown),
        Create<NativeBattleEffect79State>(nameof(NativeBattleEffectMemory.Effect79), 79, NativeBattleEffect79Profile.Entry, NativeBattleEffect79State.FromColdProcess, NativeBattleEffect79State.FromUnknown),
        Create<NativeBattleEffect80State>(nameof(NativeBattleEffectMemory.Effect80), 80, NativeBattleEffect80Profile.Entry, NativeBattleEffect80State.FromColdProcess, NativeBattleEffect80State.FromUnknown),
        Create<NativeBattleEffect68State>(nameof(NativeBattleEffectMemory.Effect68), 68, NativeBattleEffect68Profile.Entry, NativeBattleEffect68State.FromColdProcess, NativeBattleEffect68State.FromUnknown),
        Create<NativeBattleEffect345State>(nameof(NativeBattleEffectMemory.Effect345), 345, NativeBattleEffect345Profile.Entry, NativeBattleEffect345State.FromColdProcess, NativeBattleEffect345State.FromUnknown)
    ];

    private static readonly HashSet<string> Owners = Rules.Select(rule => rule.Owner).ToHashSet(StringComparer.Ordinal);

    internal static bool Owns(string owner) => Owners.Contains(owner);

    internal static void ExcludeFromDefaults(JsonObject effects)
    {
        foreach (string owner in Owners)
        {
            effects.Remove(owner);
        }
    }

    internal static void Apply(JsonObject saved, JsonSerializerOptions options)
    {
        JsonObject?[] memories =
        [
            Find(saved, "NativeBattleMemory", "EffectMemory"),
            Find(saved, "EventState", "Scene", "NativeEyeProcess", "Memory", "EffectMemory"),
            Find(saved, "DepartingFieldState", "Scene", "NativeEyeProcess", "Memory", "EffectMemory"),
            Find(saved, "Battle", "NativeEffectMemory"),
            Find(saved, "Battle", "Definition", "NativeRetainedMemory", "EffectMemory")
        ];
        JsonObject? selected = Find(saved, "Battle", "NativeGraphics", "OriginalEffect");
        JsonSerializerOptions initialOptions = new(options);
        foreach (ImmutableBytesJsonConverter converter in initialOptions.Converters.OfType<ImmutableBytesJsonConverter>().ToArray())
        {
            initialOptions.Converters.Remove(converter);
        }
        foreach (Rule rule in Rules)
        {
            if (HasInitializedEffect(selected, rule) || memories.Any(memory => HasInitializedEffect(memory?["DetachedReturnedTask"] as JsonObject, rule)))
            {
                continue;
            }
            JsonNode? cold = null;
            foreach (JsonObject? memory in memories)
            {
                if (memory is null)
                {
                    continue;
                }
                if (!memory.TryGetPropertyValue(rule.Owner, out JsonNode? owner) || owner is JsonObject current && rule.IsPlaceholder(current, options, initialOptions))
                {
                    cold ??= rule.Cold(initialOptions);
                    memory[rule.Owner] = cold.DeepClone();
                }
            }
        }
    }

    private static Rule Create<T>(string owner, ushort effect, uint entry, Func<T> cold, Func<T> unknown) where T : class
    {
        return new(owner, effect, entry, options => Serialize(cold(), options), IsPlaceholder);

        bool IsPlaceholder(JsonObject saved, JsonSerializerOptions readOptions, JsonSerializerOptions normalizedOptions)
        {
            JsonObject placeholder = Serialize(unknown(), normalizedOptions).AsObject();
            if (saved.Count != placeholder.Count || saved.Any(pair => !placeholder.ContainsKey(pair.Key)))
            {
                return false;
            }
            foreach ((string name, JsonNode? value) in placeholder)
            {
                if (value is null && saved[name] is not null || value is JsonValue && !JsonNode.DeepEquals(value, saved[name]))
                {
                    return false;
                }
            }
            T current = saved.Deserialize<T>(readOptions)!;
            return JsonNode.DeepEquals(Serialize(current, normalizedOptions), placeholder);
        }
    }

    private static JsonNode Serialize<T>(T value, JsonSerializerOptions options) => JsonSerializer.SerializeToNode(NativeBattleStorageSnapshots.Capture(value), options)!;

    private static bool HasInitializedEffect(JsonObject? effect, Rule rule)
    {
        if (effect is null || !IsNumber(effect["RequestedEffect"], rule.Effect) && !IsNumber(effect["Binding"]?["Entry"], rule.Entry))
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
