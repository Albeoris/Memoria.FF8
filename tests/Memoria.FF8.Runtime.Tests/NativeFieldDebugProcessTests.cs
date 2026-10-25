using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFieldDebugProcessTests
{
    [Fact]
    public void NewGameResetsTheExecutedDebugProcessWrite()
    {
        GameSession session = GameSession.CreateColdProcess();
        Assert.False(session.NativeFieldDebugRequested);
        session.SetNativeFieldDebugRequested(true);

        session.StartNewGame();

        Assert.False(session.NativeFieldDebugRequested);
        Assert.False(session.State.NativeFieldDebugRequested);
        session.StartNewGame();
        Assert.False(session.NativeFieldDebugRequested);
        Assert.False(GameSession.CreateColdProcess().NativeFieldDebugRequested);
    }

    [Theory]
    [InlineData(SceneKind.Title)]
    [InlineData(SceneKind.Field)]
    [InlineData(SceneKind.WorldMap)]
    public void RootJsonRetainsDebugWithoutAnActiveFieldVm(SceneKind scene)
    {
        GameSession source = GameSession.CreateColdProcess();
        source.SetNativeFieldDebugRequested(true);
        GameState dormant = source.State with { Scene = scene, World = scene == SceneKind.WorldMap ? new WorldState() : null, WorldArrival = scene == SceneKind.WorldMap ? new WorldTransition(0, 0, 0) : null, EventState = null, DepartingFieldState = null };
        GameState decoded = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(dormant))!;

        GameSession coldHost = GameSession.CreateColdProcess();
        coldHost.Restore(decoded);

        Assert.True(coldHost.NativeFieldDebugRequested);
        Assert.Null(coldHost.State.EventState);
        Assert.Equal(scene, coldHost.State.Scene);
        coldHost.SetNativeFieldDebugRequested(false);
        Assert.False(coldHost.NativeFieldDebugRequested);
        Assert.True(source.NativeFieldDebugRequested);
    }

    [Fact]
    public void HistoricalRootWithoutThePreviouslyUnsupportedDebugCommandRetainsBssZero()
    {
        JsonObject historical = JsonSerializer.SerializeToNode(GameSession.CreateColdProcess().State)!.AsObject();
        Assert.True(historical.Remove(nameof(GameState.NativeFieldDebugRequested)));

        GameSession restored = new(JsonSerializer.Deserialize<GameState>(historical.ToJsonString())!);

        Assert.False(restored.NativeFieldDebugRequested);
    }
}
