using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTerrainCellSceneTests
{
    [Fact]
    public void ActualEstharRoadCallbackExcludesUngeneratedCellAndColdRestores()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AGENTS.md")))
        {
            root = Directory.GetParent(root)!.FullName;
        }
        GameSession session = new(GameSession.Load(Path.Combine(root, "../fixtures/external-fixture.json")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: -0.018045771313400415f, Vertical: -0.9998371618107144f, Frames: 16)).Accepted);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        GameState saved = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession restoredSession = new(saved);
        using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
        restored.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
    }

    [Fact]
    public void ActualEstharFootCallbackPassesNativeFarCellOcclusionAndColdRestore()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AGENTS.md")))
        {
            root = Directory.GetParent(root)!.FullName;
        }
        GameSession session = new(GameSession.Load(Path.Combine(root, "../fixtures/external-fixture.json")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: -0.8226757f, Vertical: -0.56851095f)).Accepted);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.True(session.State.World!.NativeGardenRuntime!.PriorTerrainOcclusion);
        string snapshot = Path.Combine(Path.GetTempPath(), "Memoria-FF8-camera-cell-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.SetEventState(world.Events.Capture());
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
            restored.EnsureField();
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        }
        finally
        {
            File.Delete(snapshot);
        }
    }
}
