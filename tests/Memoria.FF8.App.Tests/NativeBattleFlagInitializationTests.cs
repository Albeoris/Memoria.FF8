using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleFlagInitializationTests
{
    [Theory]
    [InlineData(87)]
    [InlineData(89)]
    [InlineData(91)]
    [InlineData(92)]
    public void Original_cavern_encounters_enter_through_the_random_field_host_without_BATTLEMODE(ushort formation)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        using (GameWorld bootstrap = new(initial, assets.GameDirectory))
        {
            bootstrap.EnsureField();
            initial.SetEventState(bootstrap.Events.Capture());
        }
        // Isolated installed-field component: retain the executed cold reset while allocating the cavern directly.
        GameSession session = new(initial.State with { FieldId = 133, FieldName = "cavern-component", EventState = null, DepartingFieldState = initial.State.EventState, FieldLoadContext = null, FieldRequestPacket = null });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, assets.GameDirectory);
        world.EnsureField();
        world.Apply(new(PlayerActionKind.Wait, Frames: 4));
        Assert.Contains(formation, world.Field!.RandomEncounters!.Formations);
        Assert.DoesNotContain(world.Events.Scene.NativeBattleFlagWrites!, write => write.Writer == NativeBattleFlagWriter.BattleMode);
        string hash = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        RandomEncounterRequest request = new(SceneKind.Field, 133, world.Events.Frame, formation, hash);
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), request));
        ushort flags = world.Events.Scene.NativeBattleFlagWrites![^1].ActiveFlags;
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 0));
        Assert.NotNull(world.Battle);
        Assert.Equal(flags, world.Events.Scene.NativeRandomFieldBattle!.Flags);
        world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 1));
        session.SetEventState(world.Events.Capture());
        string json = JsonSerializer.Serialize(session.State);
        GameSession independent = new(JsonSerializer.Deserialize<GameState>(json)!);
        using GameWorld restored = new(independent, assets.GameDirectory);
        restored.EnsureField();
        world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 1));
        restored.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 1));
        Assert.Equal(JsonSerializer.Serialize(world.Battle!.Capture()), JsonSerializer.Serialize(restored.Battle!.Capture()));
    }

    [Fact]
    public void Explicitly_migrate_an_authenticated_pre_timer_snapshot_and_continue_one_field_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_FLAG_MIGRATION_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_FLAG_MIGRATION_OUTPUT");
        string? expectedHash = Environment.GetEnvironmentVariable("MEMORIA_FF8_FLAG_MIGRATION_SHA256");
        Assert.SkipUnless(source is not null && output is not null && expectedHash is not null, "An explicitly identified pre-timer snapshot and a separate migration destination are required.");
        byte[] bytes = File.ReadAllBytes(source!);
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(bytes)));
        JsonObject document = JsonNode.Parse(bytes)!.AsObject();
        JsonObject original = document.DeepClone().AsObject();
        JsonObject events = document["EventState"]!.AsObject();
        JsonObject scene = events["Scene"]!.AsObject();
        Assert.Null(scene["NativeBattleFlagWrites"]);
        Assert.Null(scene["NativeBattleCompletions"]);
        Assert.Equal("ff8-en-music-new-game-470560-52ca90-v1", scene["MusicProcess"]!["Bootstrap"]!["Rule"]!.GetValue<string>());
        Assert.False(document["ExamTimer"]!["TimerMemoryActivated"]!.GetValue<bool>());
        Assert.False(document["ExamTimer"]!["DisplayEnabled"]!.GetValue<bool>());
        GameState state = GameSession.Load(source!);
        Assert.False(state.EventMemory.Overlaps(182, 2));
        using GameAssets assets = new(TestResources.GameDirectory);
        string initialHash = Convert.ToHexString(SHA256.HashData(assets.LoadField(74).Jsm));
        NativeBattleFlagWrite[] history =
        [
            new(1, NativeBattleFlagWriter.NewGame, 74, initialHash, 0, -1, -1, -1, null, 0, 0),
            new(1, NativeBattleFlagWriter.FieldLoad, state.FieldId, events["ProgramHash"]!.GetValue<string>(), 0, -1, -1, -1, null, 0, 0)
        ];
        scene["NativeBattleFlagWrites"] = JsonSerializer.SerializeToNode(history, new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
        foreach (JsonObject timer in new[] { document["ExamTimer"]!.AsObject(), events["ExamTimer"]!.AsObject() })
        {
            Assert.False(timer["TimerMemoryActivated"]!.GetValue<bool>());
            foreach (string key in new[] { "Event182Mask4", "SaveGlobalMask4", "Event183Mask1" })
            {
                Assert.Null(timer[key]);
                timer[key] = false;
            }
        }
        JsonObject comparison = document.DeepClone().AsObject();
        comparison["EventState"]!["Scene"]!.AsObject().Remove("NativeBattleFlagWrites");
        foreach (JsonObject timer in new[] { comparison["ExamTimer"]!.AsObject(), comparison["EventState"]!["ExamTimer"]!.AsObject() })
        {
            foreach (string key in new[] { "Event182Mask4", "SaveGlobalMask4", "Event183Mask1" })
            {
                timer[key] = null;
            }
        }
        Assert.True(JsonNode.DeepEquals(original, comparison));
        using (FileStream stream = new(output!, FileMode.CreateNew))
        {
            JsonSerializer.Serialize(stream, document);
        }
        GameSession migrated = new(GameSession.Load(output!));
        using GameWorld world = new(migrated, assets.GameDirectory);
        world.EnsureField();
        long frame = world.Events.Frame;
        world.Apply(new(PlayerActionKind.Wait, Frames: 1));
        Assert.Equal(frame + 1, world.Events.Frame);
        Assert.Equal((ushort)0, world.Events.Scene.NativeBattleFlagWrites![^1].ActiveFlags);
        Assert.Equal(bytes, File.ReadAllBytes(source!));
    }
}
