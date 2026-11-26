using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeColdProcessResourceTests
{
    [Fact]
    public void Production_cold_process_retains_authenticated_executable_memory_across_new_game_and_restore()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleRetainedMemory expected = ReadExpectedColdMemory(assets);
        GameSession session = GameWorld.CreateColdProcess(TestResources.GameDirectory);
        AssertExecutableOwners(expected, session.State.NativeBattleMemory!);
        session.StartNewGame();
        Assert.NotNull(session.State.NativeBattleMemory!.EffectMemory!.QuezacotlExecutableMemory);
        string path = TestResources.ArtifactFile("cold-process-roundtrip.json");
        session.Save(path);
        GameState checkpoint = GameSession.Load(path);
        AssertExecutableOwners(expected, checkpoint.NativeBattleMemory!);
        GameSession restored = GameWorld.CreateColdProcess(TestResources.GameDirectory);
        restored.Restore(checkpoint);
        restored.StartNewGame();
        AssertExecutableOwners(expected, restored.State.NativeBattleMemory!);
    }
    [Fact]
    public void Autoplay_restart_uses_authenticated_cold_resources_before_creating_a_world()
    {
        GameSession session = GameSession.CreateColdProcess();
        AutoPlayController controller = new(session, () => throw new InvalidOperationException("World creation must remain deferred."), session.Restore, TestResources.ArtifactFile("cold-autoplay"), createColdProcess: () => GameWorld.CreateColdProcess(TestResources.GameDirectory));
        controller.Start();
        using GameAssets assets = new(TestResources.GameDirectory);
        AssertExecutableOwners(ReadExpectedColdMemory(assets), session.State.NativeBattleMemory!);
        Assert.Equal(74, session.State.FieldId);
    }

    private static NativeBattleRetainedMemory ReadExpectedColdMemory(GameAssets assets)
    {
        return NativeExecutionStackSelection.Apply(assets.ReadColdProcessNativeBattleMemory(), NativeExecutionStackSelection.Read(NativeExecutionStackSelection.ConfiguredPath));
    }

    private static void AssertExecutableOwners(NativeBattleRetainedMemory expected, NativeBattleRetainedMemory actual)
    {
        Assert.Equal(expected.ColdExecutableSha256, actual.ColdExecutableSha256);
        Assert.NotNull(expected.EffectMemory!.QuezacotlExecutableMemory);
        Assert.NotNull(actual.EffectMemory!.QuezacotlExecutableMemory);
        NativeBattleQuezacotlExecutableMemory expectedExecutable = NativeBattleStorageSnapshots.Capture(expected.EffectMemory.QuezacotlExecutableMemory);
        NativeBattleQuezacotlExecutableMemory actualExecutable = NativeBattleStorageSnapshots.Capture(actual.EffectMemory.QuezacotlExecutableMemory);
        Assert.Equal(JsonSerializer.Serialize(expectedExecutable), JsonSerializer.Serialize(actualExecutable));
        Assert.Equal(expected.EffectMemory.ExecutionStack, actual.EffectMemory.ExecutionStack);
    }

}
