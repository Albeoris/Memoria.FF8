using System.Text.Json;
using Memoria.FF8.Runtime;

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
}
