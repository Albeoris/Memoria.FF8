using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

/// <summary>Initializes absent process owners with the same factories used by a fresh process.</summary>
internal static class SnapshotInitialStateMigration
{
    internal static void Apply(JsonObject saved, JsonSerializerOptions options)
    {
        // Embedded event checkpoints read byte arrays as JSON arrays. The root reader accepts both encodings.
        JsonSerializerOptions initialOptions = new(options);
        foreach (var converter in initialOptions.Converters.OfType<ImmutableBytesJsonConverter>().ToArray())
        {
            initialOptions.Converters.Remove(converter);
        }
        JsonObject initial = JsonSerializer.SerializeToNode(GameSession.CreateColdProcess().State, initialOptions)!.AsObject();
        // Versioned effect initialization must also guard insertion of an entire absent parent owner.
        JsonObject initialEffects = initial[nameof(GameState.NativeBattleMemory)]![nameof(NativeBattleRetainedMemory.EffectMemory)]!.AsObject();
        initialEffects.Remove(nameof(NativeBattleEffectMemory.Sigh46));
        SnapshotRetainedEffectMigration.ExcludeFromDefaults(initialEffects);
        SnapshotDefaults.CompleteSelphieCardSlots(saved);
        SnapshotDefaults.Complete(saved, initial);
        JsonObject retained = initial[nameof(GameState.NativeBattleMemory)]!.AsObject();
        foreach (string owner in new[] { nameof(GameState.EventState), nameof(GameState.DepartingFieldState) })
        {
            if (saved[owner] is JsonObject checkpoint && checkpoint["Scene"] is JsonObject scene && scene["NativeEyeProcess"] is JsonObject eyes && eyes["Memory"] is JsonObject memory)
            {
                SnapshotDefaults.Complete(memory, retained);
            }
        }
        if (saved[nameof(GameState.Battle)] is not JsonObject battle)
        {
            return;
        }
        if (battle["Definition"] is JsonObject definition)
        {
            SnapshotDefaults.CompleteProperty(definition, "NativeRetainedMemory", retained);
        }
        // BattleSnapshot mirrors these physical owners separately from its entry-time definition.
        // Complete each copy independently; an entry-time value is not the current running value.
        foreach ((string name, JsonNode? value) in retained)
        {
            string mirror = "Native" + name;
            if (typeof(BattleSnapshot).GetProperty(mirror) is not null)
            {
                SnapshotDefaults.CompleteProperty(battle, mirror, value);
            }
        }
        if (battle["NativeScriptState"] is JsonObject script)
        {
            SnapshotDefaults.CompleteProperty(script, "AllocationControl", retained["AllocationControl"]);
        }
    }
}
