using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

/// <summary>Completes absent saved properties from the same initial state used by New Game.</summary>
internal static class SnapshotDefaults
{
    internal static GameState Deserialize(Stream stream, JsonSerializerOptions options)
    {
        JsonObject saved = JsonSerializer.Deserialize<JsonObject>(stream, options) ?? throw new JsonException("Expected a saved game object.");
        int originalVersion = SnapshotMigrations.Apply(saved, options);
        if (saved["GameplayPreferences"] is JsonObject preferences && new[] { "SharedMagicStock", "InfiniteMagic", "SmartAttack" }.Any(flag => preferences[flag] is JsonValue value && value.TryGetValue(out bool enabled) && enabled) && saved["CharacterVitals"] is JsonObject vitals && !vitals.ContainsKey("MagicSystem"))
        {
            throw new InvalidDataException("Enabled magic rules require their saved magic-system owner.");
        }
        CompleteSelphieCardSlots(saved);
        CompleteTripleTriadSettings(saved, options);
        PreserveHistoricalWorldGlobals(saved);
        // Execution snapshots require their saved counters and matching retained-memory copies verbatim.
        if (saved["Battle"] is JsonObject existingBattle && !IsIdleBattle(existingBattle))
        {
            return saved.Deserialize<GameState>(options) ?? throw new JsonException("Empty checkpoint.");
        }
        GameSession initial = GameSession.CreateColdProcess();
        JsonObject defaults = JsonSerializer.SerializeToNode(initial.State, options)!.AsObject();
        JsonObject effects = defaults[nameof(GameState.NativeBattleMemory)]![nameof(NativeBattleRetainedMemory.EffectMemory)]!.AsObject();
        effects.Remove(nameof(NativeBattleEffectMemory.Sigh46));
        SnapshotRetainedEffectMigration.ExcludeFromDefaults(effects);
        Complete(saved, defaults);

        // A live battle is a continuation, not a new process. Its missing execution state must remain invalid.
        if (saved["Battle"] is JsonObject battle)
        {
            JsonObject retained = defaults["NativeBattleMemory"]!.AsObject();
            CompleteProperty(battle, "NativeEffectMemory", retained["EffectMemory"]);
            if (battle["Definition"] is JsonObject definition)
            {
                CompleteProperty(definition, "NativeRetainedMemory", retained);
            }
        }

        SnapshotMigrations.CompleteInitialOwners(saved, options, originalVersion);
        return saved.Deserialize<GameState>(options) ?? throw new JsonException("Empty checkpoint.");
    }

    internal static void PreserveHistoricalWorldGlobals(JsonObject saved)
    {
        if (saved["WorldProcess"] is JsonObject worldProcess && worldProcess["MainLoopCallbacks"] is JsonValue callbacks && callbacks.TryGetValue(out long count) && count > 0)
        {
            // A historical process has no cold-start proof for newly retained presentation globals.
            foreach (string owner in new[] { "PreviousContactC76784", "WaterStepEdges2045C7C" })
            {
                if (!worldProcess.ContainsKey(owner))
                {
                    worldProcess[owner] = null;
                }
            }
        }
    }

    internal static void CompleteSelphieCardSlots(JsonObject saved)
    {
        foreach (string owner in new[] { "GameplayPreferences", "PendingGameplayPreferences" })
        {
            if (saved[owner] is JsonObject preferences && !preferences.ContainsKey("SelphieCardSlots"))
            {
                JsonNode? difficulty = preferences["Difficulty"];
                bool hardcore = difficulty is JsonValue value && (value.TryGetValue(out int number) && number == (int)BattleDifficulty.Hardcore || value.TryGetValue(out string? name) && name == nameof(BattleDifficulty.Hardcore));
                preferences["SelphieCardSlots"] = hardcore;
            }
        }
        if (saved["Battle"] is JsonObject battle && battle["Definition"] is JsonObject definition && !definition.ContainsKey("SelphieCardSlots"))
        {
            definition["SelphieCardSlots"] = saved["GameplayPreferences"]?["SelphieCardSlots"]?.DeepClone() ?? JsonValue.Create(false);
        }
    }

    internal static void CompleteTripleTriadSettings(JsonObject saved, JsonSerializerOptions options)
    {
        foreach (string owner in new[] { nameof(GameState.GameplayPreferences), nameof(GameState.PendingGameplayPreferences) })
        {
            if (saved[owner] is not JsonObject preferences)
            {
                continue;
            }
            JsonNode? difficulty = preferences[nameof(GameplayPreferences.Difficulty)];
            bool hardcore = difficulty is JsonValue value && (value.TryGetValue(out int number) && number == (int)BattleDifficulty.Hardcore || value.TryGetValue(out string? name) && name == nameof(BattleDifficulty.Hardcore));
            if (!preferences.ContainsKey(nameof(GameplayPreferences.TripleTriad)))
            {
                TripleTriadSettings defaults = new TripleTriadSettings().NormalizeForDifficulty(hardcore ? BattleDifficulty.Hardcore : BattleDifficulty.Normal);
                preferences[nameof(GameplayPreferences.TripleTriad)] = JsonSerializer.SerializeToNode(defaults, options);
            }
            else if (hardcore && preferences[nameof(GameplayPreferences.TripleTriad)] is JsonObject triad && !triad.ContainsKey(nameof(TripleTriadSettings.BlockedCellCount)))
            {
                triad[nameof(TripleTriadSettings.BlockedCellCount)] = 2;
            }
        }
    }

    private static bool IsIdleBattle(JsonObject battle)
    {
        if (!IsKnownNull(battle, "ActiveAction") || !IsKnownNull(battle, "NativeTimedAction") || battle["QueuedActions"] is not JsonArray { Count: 0 } || battle["GuardianForceCasts"] is not JsonArray { Count: 0 })
        {
            return false;
        }
        // Match the pending native work checked by BattleSession.HasPendingTurn, including deferred completions.
        return battle["NativeActionScratch"] is JsonObject scratch && IsKnownNull(scratch, "ComponentCompletion")
            && battle["NativeScriptState"] is JsonObject script && script["WaitCounters"] is JsonArray { Count: 0 } && script["AllocationControl"] is JsonObject allocation && allocation["ActionGate"] is JsonValue gate && gate.TryGetValue(out bool pending) && !pending
            && battle["NativeGraphics"] is JsonObject graphics && IsKnownNull(graphics, "OriginalEffect") && graphics["Requests"] is JsonArray { Count: 0 } && graphics["Descriptors"] is JsonObject descriptors && descriptors["Queue"] is JsonArray { Count: 0 };
    }

    private static bool IsKnownNull(JsonObject value, string name) => value.TryGetPropertyValue(name, out JsonNode? property) && property is null;

    internal static void Complete(JsonObject saved, JsonObject defaults, bool effectMemory = false)
    {
        foreach ((string name, JsonNode? value) in defaults)
        {
            // Retained effect processes with an executed entry need their own saved counters.
            if (effectMemory && value is JsonObject process && process.ContainsKey("Entry") && process["Entry"] is null && saved[name] is JsonObject currentProcess && currentProcess["Entry"] is JsonObject)
            {
                continue;
            }
            CompleteProperty(saved, name, value);
        }
    }

    internal static void CompleteProperty(JsonObject saved, string name, JsonNode? value)
    {
        // Versioned migration owns this complete state; filling individual fields can erase unknown history.
        if (name == nameof(NativeBattleEffectMemory.Sigh46) || SnapshotRetainedEffectMigration.Owns(name))
        {
            return;
        }
        if (!saved.TryGetPropertyValue(name, out JsonNode? current))
        {
            saved.Add(name, value?.DeepClone());
        }
        else if (name != "MagicSystem" && current is JsonObject savedObject && value is JsonObject defaultObject)
        {
            Complete(savedObject, defaultObject, name is "EffectMemory" or "NativeEffectMemory");
        }
    }
}
