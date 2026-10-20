using System.Text.Json;
using Memoria.FF8.Runtime;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class NativeInitialEyeRegistrationTests
{
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
