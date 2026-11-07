using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayPositionGateTests
{
    [Fact]
    public void OrdinaryWalkingSatisfiesAnOriginalDefaultScriptPositionGate()
    {
        string snapshot = TestResources.FixturePath("App.Tests.AutoPlayPositionGateTests.1");
        GameSession session = new(GameSession.Load(snapshot));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Empty(world.DiscoverAutoPlayTargets());
        NativePlayerPositionGate gate = Assert.IsType<NativePlayerPositionGate>(world.Events.DiscoverAutoPlayPositionGate());
        Assert.Equal(new NativePlayerPositionGate(1, 1291), gate);
        Assert.True(world.Events.Scene.MapJumpDisabled);

        int moves = 0;
        world.Events.NativePlayerMovementProcessed += _ => moves++;
        Assert.True(world.WalkToAutoPlayPositionGate(gate, () => { }));

        Assert.True(moves > 0);
        Assert.True(world.Events.PlayerActor!.Y < 1291);
        Assert.False(world.Events.IsUserControlEnabled);
        Assert.Null(world.Events.LastFault);
    }
}
