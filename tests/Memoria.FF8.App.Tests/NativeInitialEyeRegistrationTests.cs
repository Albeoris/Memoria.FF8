using System.Text.Json;
using Memoria.FF8.Runtime;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class NativeInitialEyeRegistrationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Battle_save_validation_preserves_current_memory_and_crt_while_restoring_frozen_field_eyes(int slot)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        world.Events.Step();
        JsonElement frozen = world.Events.Capture();
        NativeFieldEyeProcess cached = world.Events.Scene.NativeEyeProcess!;
        var field = world.Field!;
        NativeCrtRandomState current = session.State.CrtRandom!.Draw(out _);
        session.SetNativeFieldEyeOwners(session.State.NativeBattleMemory!, current);
        // Isolate persistence ownership with a component battle, without executing effects.
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = cached.Memory with { EffectIoResult = -1 },
            Combatants =
            [
                new() { Id = "party", IsPlayer = true, Name = "Party", MaximumHp = 1000, CurrentHp = 1000 },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000 }
            ]
        }, BattleAtbMode.Normal);
        session.SetBattle(battle.Capture());
        session.SetEventState(world.Events.Capture());
        Assert.NotEqual(cached.Memory.EffectIoResult, session.State.NativeBattleMemory!.EffectIoResult);
        Assert.NotEqual(cached.CrtRandom, current);

        EventEngine RestoreField(GameSession owner)
        {
            EventEngine engine = new(owner)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                ReadNativeEyes = model => field.Models[model].NativeEyes,
                ReadNativeModelParts = model => field.Models[model].NativeParts,
                InitInputAxisDirection = field.Information!.ControlDirection
            };
            engine.LoadField(field.Id, field.Jsm, field.Messages, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En, restoringCheckpoint: true);
            engine.Restore(owner.State.EventState!.Value);
            return engine;
        }

        void Validate(GameState state)
        {
            GameSession candidate = new(state);
            NativeBattleRetainedMemory memory = candidate.State.NativeBattleMemory!;
            EventEngine restored = RestoreField(candidate);
            Assert.Same(memory, candidate.State.NativeBattleMemory);
            Assert.Equal(current, candidate.State.CrtRandom);
            Assert.True(JsonElement.DeepEquals(frozen.GetProperty("Scene").GetProperty("NativeEyeProcess"), restored.Capture().GetProperty("Scene").GetProperty("NativeEyeProcess")));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(state.Battle), JsonSerializer.SerializeToElement(candidate.State.Battle)));
        }

        string directory = TestResources.ArtifactFile("battle-eye-save-" + slot);
        PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "saves"));
        store.Save(slot, session, Validate);
        GameState loaded = store.Load(slot);
        Validate(loaded);
        BattleSession resumed = BattleSession.Restore(loaded.Battle!);
        battle.Tick();
        resumed.Tick();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(battle.Capture()), JsonSerializer.SerializeToElement(resumed.Capture())));
    }

    [Fact]
    public void Original_new_game_field_registers_actual_models_and_restores_eye_owners_without_advancing_rng()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        world.Events.Step();
        NativeFieldEyeProcess process = Assert.IsType<NativeFieldEyeProcess>(world.Events.Scene.NativeEyeProcess);
        NativeFieldRegistrationState registration = Assert.IsType<NativeFieldRegistrationState>(world.Events.Scene.NativeFieldRegistration);
        Assert.Equal(74, registration.FieldId);
        process.Validate(registration);
        Assert.Same(process.Memory, session.State.NativeBattleMemory);
        Assert.Equal(process.CrtRandom, session.State.CrtRandom);
        session.SetEventState(world.Events.Capture());
        GameState snapshot = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        using GameWorld restored = new(new GameSession(snapshot), TestResources.GameDirectory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        NativeCrtRandomState? before = session.State.CrtRandom;
        world.Events.Step();
        restored.Events.Step();
        Assert.Equal(before, session.State.CrtRandom);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
    }
    [Fact]
    public void Dormant_original_field_retains_cached_registration_without_replacing_current_field_crt()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        world.Events.Step();
        JsonElement frozen = world.Events.Capture();
        NativeFieldEyeProcess cached = world.Events.Scene.NativeEyeProcess!;
        var field = world.Field!;
        // The ordinary field request changes the active owner; restoring the
        // outgoing field is inspection context, without an allocation or callback.
        session.RequestField(75, 0);
        NativeCrtRandomState current = session.State.CrtRandom!.Draw(out _);
        session.SetNativeFieldEyeOwners(session.State.NativeBattleMemory!, current);
        GameState before = session.State;
        EventEngine dormant = new(session)
        {
            AnimationFrameCount = field.GetAnimationFrameCount,
            ReadNativeEyes = model => field.Models[model].NativeEyes,
            ReadNativeModelParts = model => field.Models[model].NativeParts,
            InitInputAxisDirection = field.Information!.ControlDirection
        };
        dormant.LoadField(74, field.Jsm, field.Messages, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En, restoringCheckpoint: true);
        dormant.RestoreFieldState(frozen);
        Assert.Same(before, session.State);
        Assert.Equal(current, session.State.CrtRandom);
        Assert.Equal(cached.CrtRandom, dormant.Scene.NativeEyeProcess!.CrtRandom);
        Assert.True(JsonElement.DeepEquals(frozen.GetProperty("Scene").GetProperty("NativeEyeProcess"), dormant.Capture().GetProperty("Scene").GetProperty("NativeEyeProcess")));
        Assert.Same(before, session.State);
    }

}
