using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveSceneInitializationTests
{
    private static GameplaySaveSceneFacts Facts(GameplaySaveSceneFactsOrigin origin = GameplaySaveSceneFactsOrigin.Field) => new(origin, 180, new() { [0] = "Saved name" }, [3, 17], [4, 255, 0], 7, false, true, true, 91, 3, null, null) { PersistentBattleMode = 0x2200, ActiveBattleMode = 0x2200 };

    [Fact]
    public void Field_process_restores_semantic_facts_without_running_scripts_or_replacing_progress()
    {
        GameState state = new() { Scene = SceneKind.Field, FieldId = 180, X = 37, Y = 42, FieldLoadContext = NativeFieldLoadContext.FromGameplaySave(180), ExamTimer = NativeExamTimerState.FromColdProcess() with { Event182Mask4 = false, Event183Mask1 = false, SaveGlobalMask4 = false } };
        GameSession session = new(state);
        EventEngine engine = new(session);
        engine.InitializeGameplaySave(NativeFieldRuntimeProfile.FF8En, Facts());

        Assert.False(engine.HasFieldProgram);
        Assert.Same(state, session.State);
        Assert.Equal(new[] { 4, 255, 0 }, engine.Scene.Party);
        Assert.Equal("Saved name", engine.Scene.Names[0]);
        Assert.Equal(new[] { 3, 17 }, engine.Scene.UnlockedTutorials);
        Assert.Equal(7, engine.Scene.PartyPower);
        Assert.False(engine.Scene.MenuEnabled);
        Assert.True(engine.Scene.MapJumpDisabled);
        Assert.Equal(91, engine.Scene.PlaceId);
        Assert.Equal((byte?)3, engine.Scene.NativeSalaryMovementUnits);
        Assert.False(engine.Scene.MusicProcess!.Pending);
        Assert.Null(engine.Scene.MusicProcess.Buffer0);
        Assert.Null(engine.Scene.MusicProcess.Buffer1);
        Assert.Equal(NativeMusicBootstrap.GameplaySaveRule, engine.Scene.MusicProcess.Bootstrap!.Rule);
        engine.Scene.MusicProcess.Validate();
        Assert.Equal(NativeBattleFlagWriter.GameplaySave, Assert.Single(engine.Scene.NativeBattleFlagWrites!).Writer);
        Assert.Equal((byte)0, state.FieldLoadContext.Reason);
    }

    [Fact]
    public void World_process_prefers_current_semantic_party_to_stale_original_import_bytes_and_roundtrips()
    {
        byte[] bytes = new byte[NativePcSaveData.BlockSize];
        bytes[0] = (byte)'S';
        bytes[1] = (byte)'C';
        bytes[NativePcSaveData.SaveMapOffset + 2] = 255;
        bytes[NativePcSaveData.SaveMapOffset + 3] = 8;
        bytes[NativePcSaveData.MainOffset + 3304] = 1;
        GameState state = new() { Scene = SceneKind.WorldMap, FieldId = 180, WorldArrival = new(62, 0, -1), OriginalSave = new(bytes), ExamTimer = NativeExamTimerState.FromColdProcess() with { Event182Mask4 = false, Event183Mask1 = false, SaveGlobalMask4 = false } };
        GameSession session = new(state);
        EventEngine engine = new(session);
        engine.InitializeGameplaySave(NativeFieldRuntimeProfile.FF8En, Facts(GameplaySaveSceneFactsOrigin.WorldEventContext));
        Assert.Equal(new[] { 4, 255, 0 }, engine.Scene.Party);
        Assert.Equal("Saved name", engine.Scene.Names[0]);
        JsonElement captured = engine.CaptureWorldEventState();
        EventEngine restored = new(new GameSession(session.State));
        restored.RestoreWorldEventState(captured, NativeFieldRuntimeProfile.FF8En);
        Assert.Equal(engine.Scene.Party, restored.Scene.Party);
        Assert.Equal(engine.Scene.Names, restored.Scene.Names);
        Assert.Equal(engine.Scene.UnlockedTutorials, restored.Scene.UnlockedTutorials);
        Assert.Equal(NativeMusicBootstrap.GameplaySaveRule, restored.Scene.MusicProcess!.Bootstrap!.Rule);
        Assert.Equal((ushort)0x2200, Assert.Single(restored.Scene.NativeBattleFlagWrites!).PersistentFlags);
        Assert.Equal(1, bytes[NativePcSaveData.MainOffset + 3304]);
    }

    [Fact]
    public void Invalid_or_existing_continuation_refuses_before_process_initialization()
    {
        GameState state = new() { Scene = SceneKind.Field, FieldId = 180, FieldLoadContext = NativeFieldLoadContext.FromGameplaySave(180) };
        EventEngine wrongField = new(new GameSession(state));
        Assert.Throws<InvalidOperationException>(() => wrongField.InitializeGameplaySave(NativeFieldRuntimeProfile.FF8En, Facts() with { FieldId = 181 }));
        Assert.Null(wrongField.Scene.MusicProcess);
        EventEngine duplicate = new(new GameSession(state));
        Assert.Throws<InvalidDataException>(() => duplicate.InitializeGameplaySave(NativeFieldRuntimeProfile.FF8En, Facts() with { FieldParty = [4, 4, 255] }));
        Assert.Null(duplicate.Scene.MusicProcess);
        EventEngine exactSnapshot = new(new GameSession(state with { EventState = JsonSerializer.SerializeToElement(new { Version = 1 }) }));
        Assert.Throws<InvalidOperationException>(() => exactSnapshot.InitializeGameplaySave(NativeFieldRuntimeProfile.FF8En, Facts()));
        Assert.Null(exactSnapshot.Scene.MusicProcess);
    }

    [Fact]
    public void Current_prepared_world_facts_keep_full_battle_mode_without_adopting_frozen_globals()
    {
        GameState field = new() { Scene = SceneKind.Field, FieldId = 180, FieldLoadContext = NativeFieldLoadContext.FromGameplaySave(180) };
        EventEngine engine = new(new GameSession(field));
        engine.InitializeGameplaySave(NativeFieldRuntimeProfile.FF8En, Facts());
        JsonElement current = JsonSerializer.SerializeToElement(new { FieldId = 180, engine.Scene });
        GameState world = field with { Scene = SceneKind.WorldMap, EventState = current };
        GameplaySaveSceneFacts projected = GameplaySaveSceneFacts.FromPreparedState(world)!;
        Assert.Equal(GameplaySaveSceneFactsOrigin.PreparedWorldField, projected.Origin);
        Assert.Equal((ushort?)0x2200, projected.PersistentBattleMode);
        Assert.Equal((ushort?)0x2200, projected.ActiveBattleMode);
        Assert.NotNull(projected.Music);
        GameplaySaveSceneFacts frozen = GameplaySaveSceneFacts.FromPreparedState(world with { EventState = null, DepartingFieldState = current })!;
        Assert.Equal(GameplaySaveSceneFactsOrigin.SuspendedField, frozen.Origin);
        Assert.Null(frozen.PersistentBattleMode);
        Assert.Null(frozen.ActiveBattleMode);
        Assert.Null(frozen.Music);
    }

    [Fact]
    public void Missing_mode_and_contradictory_timer_mirrors_refuse_before_initialization()
    {
        GameState state = new() { Scene = SceneKind.Field, FieldId = 180, FieldLoadContext = NativeFieldLoadContext.FromGameplaySave(180), ExamTimer = NativeExamTimerState.FromColdProcess() with { Event182Mask4 = false, Event183Mask1 = false, SaveGlobalMask4 = false } };
        EventEngine missing = new(new GameSession(state));
        Assert.Throws<NotSupportedException>(() => missing.InitializeGameplaySave(NativeFieldRuntimeProfile.FF8En, Facts() with { PersistentBattleMode = null, ActiveBattleMode = null }));
        Assert.Null(missing.Scene.MusicProcess);
        EventEngine contradictory = new(new GameSession(state));
        Assert.Throws<InvalidDataException>(() => contradictory.InitializeGameplaySave(NativeFieldRuntimeProfile.FF8En, Facts() with { PersistentBattleMode = 0x2204 }));
        Assert.Null(contradictory.Scene.MusicProcess);
    }
}
