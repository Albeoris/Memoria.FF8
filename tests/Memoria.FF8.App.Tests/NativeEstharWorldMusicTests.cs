using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeEstharWorldMusicTests
{
    [Fact]
    public void Actual422WorldDeparturePreservesPendingFieldPreloadAndRestores()
    {
        string root = AppContext.BaseDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.NativeEstharWorldMusicTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        NativeMusicProcessState before = world.Events.Scene.MusicProcess!;
        Assert.True(before.Pending);
        Assert.Equal((byte)85, before.Requested213);
        Assert.Equal((byte)1, before.Complete214);
        Assert.Equal((byte)41, world.Events.Scene.WorldMusicSelection!.Music);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 60)).Accepted);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Null(world.Events.LastFault);
        NativeWorldMusicLoad load = Assert.IsType<NativeWorldMusicLoad>(world.Events.Scene.WorldMusicLoad);
        Assert.Equal(NativeWorldMusicOperation.Play, load.Operation);
        Assert.Equal((byte)41, load.Music);
        Assert.Equal(42, load.Resource!.Section);
        Assert.Equal((byte)(before.Bank201!.Value ^ 1), load.Bank);
        Assert.Equal(before with { Bank201 = load.Bank }, world.Events.Scene.MusicProcess);
        Assert.Equal(new NativeMusicChannelState(0, 41), world.Events.Scene.MusicChannel108);
        session.SetDepartingFieldState(world.Events.Capture());
        GameState saved = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession restoredSession = new(saved);
        using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State.World), JsonSerializer.SerializeToElement(restoredSession.State.World)));
    }
}
