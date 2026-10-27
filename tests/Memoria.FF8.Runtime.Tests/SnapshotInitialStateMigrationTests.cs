using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotInitialStateMigrationTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter(), new ImmutableBytesJsonConverter() } };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Historical_battle_and_all_retained_copies_receive_missing_new_process_owners(int schema)
    {
        JsonObject saved = HistoricalBattle(schema);
        JsonObject cold = JsonSerializer.SerializeToNode(NativeBattleRetainedMemory.FromColdProcess(), Options)!.AsObject();
        string[] added = ["CameraAliases", "Effect30", "Effect9", "Effect314", "Effect338", "Effect187", "Effect218", "Effect326", "UiImmediate"];
        JsonObject[] copies = [saved["NativeBattleMemory"]!.AsObject(), saved["Battle"]!["Definition"]!["NativeRetainedMemory"]!.AsObject()];
        foreach (JsonObject memory in copies)
        {
            foreach (string name in added)
            {
                memory["EffectMemory"]!.AsObject().Remove(name);
            }
        }
        foreach (string name in added)
        {
            saved["Battle"]!["NativeEffectMemory"]!.AsObject().Remove(name);
        }
        saved["EventState"] = new JsonObject { ["Scene"] = new JsonObject { ["NativeEyeProcess"] = new JsonObject { ["Memory"] = copies[0].DeepClone() } } };
        saved["DepartingFieldState"] = saved["EventState"]!.DeepClone();
        saved["Battle"]!["NativeStage"]!.AsObject().Remove("FinalStage142");
        saved["Battle"]!["NativeStage"]!.AsObject().Remove("Specialized");
        saved["Battle"]!["Frame"] = 120L;
        GameState restored = Deserialize(saved);

        Assert.Equal(GameState.CurrentSnapshotSchemaVersion, restored.SnapshotSchemaVersion);
        Assert.Equal(120, restored.Battle!.Frame);
        JsonObject result = JsonSerializer.SerializeToNode(restored, Options)!.AsObject();
        JsonNode?[] effects = [result["NativeBattleMemory"]!["EffectMemory"], result["Battle"]!["NativeEffectMemory"], result["Battle"]!["Definition"]!["NativeRetainedMemory"]!["EffectMemory"], result["EventState"]!["Scene"]!["NativeEyeProcess"]!["Memory"]!["EffectMemory"], result["DepartingFieldState"]!["Scene"]!["NativeEyeProcess"]!["Memory"]!["EffectMemory"]];
        foreach (JsonNode? effect in effects)
        {
            JsonObject normalized = JsonSerializer.SerializeToNode(effect!.Deserialize<NativeBattleEffectMemory>(Options), Options)!.AsObject();
            foreach (string name in added)
            {
                Assert.True(JsonNode.DeepEquals(cold["EffectMemory"]![name], normalized[name]), name);
            }
        }
        Assert.True(JsonNode.DeepEquals(cold["Stage"]!["FinalStage142"], result["Battle"]!["NativeStage"]!["FinalStage142"]));
        Assert.True(JsonNode.DeepEquals(cold["Stage"]!["Specialized"], result["Battle"]!["NativeStage"]!["Specialized"]));
    }

    [Fact]
    public void Current_and_entry_memory_values_are_completed_independently_without_overwriting_nulls()
    {
        JsonObject saved = HistoricalBattle(1);
        saved["NativeBattleMemory"]!["ActionScratch"]!["GuardianBoostPercent"] = 31;
        saved["Battle"]!["NativeActionScratch"]!["GuardianBoostPercent"] = 62;
        saved["Battle"]!["Definition"]!["NativeRetainedMemory"]!["ActionScratch"]!["GuardianBoostPercent"] = 93;
        saved["Battle"]!["NativeEffectMemory"]!["Effect326"] = null;
        saved["Battle"]!["NativeStage"] = null;
        saved["WorldProcess"]!["PreviousContactC76784"] = null;
        saved["WorldProcess"]!["WaterStepEdges2045C7C"] = null;
        GameState restored = Deserialize(saved);
        Assert.Equal((byte)31, restored.NativeBattleMemory!.ActionScratch.GuardianBoostPercent);
        Assert.Equal((byte)62, restored.Battle!.NativeActionScratch!.GuardianBoostPercent);
        Assert.Equal((byte)93, restored.Battle.Definition.NativeRetainedMemory!.ActionScratch.GuardianBoostPercent);
        Assert.Null(JsonSerializer.SerializeToNode(restored.Battle.NativeEffectMemory, Options)!["Effect326"]);
        Assert.Null(restored.Battle.NativeStage);
        Assert.Null(restored.WorldProcess!.PreviousContactC76784);
        Assert.Null(restored.WorldProcess.WaterStepEdges2045C7C);
    }

    [Fact]
    public void Historical_executed_process_counters_are_not_recreated_as_cold_values()
    {
        JsonObject saved = HistoricalBattle(1);
        JsonObject process = saved["Battle"]!["NativeEffectMemory"]!["Shiva"]!.AsObject();
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        process["Entry"] = JsonSerializer.SerializeToNode(new NativeBattleShivaEntryState(0, 1, new(0, 0, 0), matrix, 7), Options);
        process.Remove("SpawnedChildD32508");
        JsonException error = Assert.Throws<JsonException>(() => Deserialize(saved));
        Assert.Contains("SpawnedChildD32508", error.Message);
    }

    [Fact]
    public void Missing_new_world_globals_receive_cold_values_without_resetting_callback_history()
    {
        JsonObject saved = HistoricalBattle(1);
        saved["WorldProcess"]!["MainLoopCallbacks"] = 20L;
        saved["WorldProcess"]!.AsObject().Remove("PreviousContactC76784");
        saved["WorldProcess"]!.AsObject().Remove("WaterStepEdges2045C7C");
        GameState restored = Deserialize(saved);
        NativeWorldProcess initial = NativeWorldProcess.FromColdProcess();
        Assert.Equal(initial.PreviousContactC76784, restored.WorldProcess!.PreviousContactC76784);
        Assert.Equal(initial.WaterStepEdges2045C7C, restored.WorldProcess.WaterStepEdges2045C7C);
        Assert.Equal(20, restored.WorldProcess.MainLoopCallbacks);
    }

    [Fact]
    public void All_absent_independent_effect_owners_use_the_current_initialization_factories()
    {
        JsonObject saved = HistoricalBattle(1);
        JsonObject initial = JsonSerializer.SerializeToNode(NativeBattleStorageSnapshots.Capture(NativeBattleEffectMemory.FromColdProcess()), Options)!.AsObject();
        JsonObject effects = saved["Battle"]!["NativeEffectMemory"]!.AsObject();
        string[] allocationOwner = ["Arena", "Cursor", "AllocationCount", "Allocations", "Active", "SelectedIndex", "SoundPendingFlags", "SoundPublishedFlags"];
        foreach (string name in effects.Select(pair => pair.Key).Except(allocationOwner).ToArray())
        {
            effects.Remove(name);
        }
        effects["SelectedIndex"] = 17;
        GameState restored = Deserialize(saved);
        JsonObject upgraded = JsonSerializer.SerializeToNode(restored.Battle!.NativeEffectMemory, Options)!.AsObject();
        foreach ((string name, JsonNode? value) in initial)
        {
            if (!allocationOwner.Contains(name))
            {
                Assert.True(JsonNode.DeepEquals(value, upgraded[name]), name);
            }
        }
        Assert.Equal(17, upgraded["SelectedIndex"]!.GetValue<int>());
    }

    [Fact]
    public void Historical_hardcore_defaults_run_before_generic_cold_completion()
    {
        JsonObject saved = HistoricalBattle(1);
        foreach (string name in new[] { "GameplayPreferences", "PendingGameplayPreferences" })
        {
            saved[name] = new JsonObject { ["Difficulty"] = nameof(BattleDifficulty.Hardcore) };
        }
        saved["Battle"]!["Definition"]!.AsObject().Remove("SelphieCardSlots");
        GameState restored = Deserialize(saved);
        Assert.True(restored.GameplayPreferences.SelphieCardSlots);
        Assert.True(restored.PendingGameplayPreferences!.SelphieCardSlots);
        Assert.True(restored.Battle!.Definition.SelphieCardSlots);
    }

    [Fact]
    public void Inserted_event_memory_remains_readable_by_the_event_checkpoint_serializer()
    {
        JsonObject saved = HistoricalBattle(1);
        JsonSerializerOptions eventOptions = new() { Converters = { new JsonStringEnumConverter() } };
        JsonObject memory = JsonSerializer.SerializeToNode(NativeBattleRetainedMemory.FromColdProcess(), eventOptions)!.AsObject();
        memory["EffectMemory"]!.AsObject().Remove("BoneMatrixAnchor2795BC0");
        saved["EventState"] = new JsonObject { ["Scene"] = new JsonObject { ["NativeEyeProcess"] = new JsonObject { ["Memory"] = memory } } };
        GameState restored = Deserialize(saved);
        JsonElement result = restored.EventState!.Value.GetProperty("Scene").GetProperty("NativeEyeProcess").GetProperty("Memory");
        Assert.Equal(JsonValueKind.Array, result.GetProperty("EffectMemory").GetProperty("BoneMatrixAnchor2795BC0").ValueKind);
        NativeBattleRetainedMemory typed = result.Deserialize<NativeBattleRetainedMemory>(eventOptions)!;
        Assert.Equal(new byte[8], typed.EffectMemory!.BoneMatrixAnchor2795BC0!.Value.ToArray());
    }

    private static JsonObject HistoricalBattle(int schema)
    {
        GameSession session = GameSession.CreateColdProcess();
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = session.State.NativeBattleMemory,
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000 }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000 }]
        }, BattleAtbMode.Normal);
        JsonObject saved = JsonSerializer.SerializeToNode(session.State with { Battle = battle.Capture() }, Options)!.AsObject();
        saved["SnapshotSchemaVersion"] = schema;
        return saved;
    }

    private static GameState Deserialize(JsonObject saved)
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes(saved.ToJsonString(Options)));
        return SnapshotDefaults.Deserialize(input, Options);
    }
}
