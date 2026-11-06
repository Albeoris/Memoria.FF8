using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExecutionEnvironmentRecoveryTests
{
    [Fact]
    public void Selected_standalone_environment_survives_field_restore_and_an_independent_next_callback()
    {
        string path = TestResources.FixturePath("App.Tests.ExecutionEnvironmentRecoveryTests.1");
        byte[] original = File.ReadAllBytes(path);
        GameState state = GameSession.Load(path);
        NativeBattleExecutionStack profile = Assert.IsType<NativeBattleExecutionStack>(state.NativeBattleMemory!.EffectMemory!.ExecutionStack);
        NativeBattleHeroCrtStackContract crt = Assert.IsType<NativeBattleHeroCrtStackContract>(state.NativeBattleMemory.EffectMemory.HeroCrtStack);
        GameSession session = new(state);
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        GameSession independent = new(GameSession.Load(path));
        using GameWorld clone = new(independent, TestResources.GameDirectory);
        clone.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(clone.Apply(new(PlayerActionKind.Wait)).Accepted);
        GameState next = session.PrepareSnapshot();
        Assert.Equal(profile, next.NativeBattleMemory!.EffectMemory!.ExecutionStack);
        Assert.Equal(crt, next.NativeBattleMemory.EffectMemory.HeroCrtStack);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(independent.PrepareSnapshot()));
        Assert.Equal(SHA256.HashData(original), SHA256.HashData(File.ReadAllBytes(path)));
    }
}
