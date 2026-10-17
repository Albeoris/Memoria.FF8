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
        NativeBattleRetainedMemory expected = assets.ReadColdProcessNativeBattleMemory();
        GameSession session = GameWorld.CreateColdProcess(TestResources.GameDirectory);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.State.NativeBattleMemory));
        session.StartNewGame();
        Assert.NotNull(session.State.NativeBattleMemory!.EffectMemory!.QuezacotlExecutableMemory);
        string path = TestResources.ArtifactFile("cold-process-roundtrip.json");
        session.Save(path);
        GameSession restored = new(GameSession.Load(path));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(restored.State.NativeBattleMemory));
        restored.StartNewGame();
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(restored.State.NativeBattleMemory));
    }
    [Fact]
    public void Autoplay_restart_uses_authenticated_cold_resources_before_creating_a_world()
    {
        GameSession session = GameSession.CreateColdProcess();
        AutoPlayController controller = new(session, () => throw new InvalidOperationException("World creation must remain deferred."), session.Restore, TestResources.ArtifactFile("cold-autoplay"), createColdProcess: () => GameWorld.CreateColdProcess(TestResources.GameDirectory));
        controller.Start();
        using GameAssets assets = new(TestResources.GameDirectory);
        Assert.Equal(JsonSerializer.Serialize(assets.ReadColdProcessNativeBattleMemory()), JsonSerializer.Serialize(session.State.NativeBattleMemory));
        Assert.Equal(74, session.State.FieldId);
    }

}
