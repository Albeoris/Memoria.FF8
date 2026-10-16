using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativePlayerMovementTests
{
    [Fact]
    public void OriginalElevatorFirstMoveTraversesSavedTriangleAndRestoresItsGroundState()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = TestResources.FixturePath("App.Tests.NativePlayerMovementTests.1");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        ActorState player = world.Events.Actors.Single(actor => actor.ObjectId == 0);
        Assert.Equal(93, player.WalkmeshTriangle);
        Assert.True(NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, player));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 0, Vertical: -1)).Accepted);
        Assert.Equal(new FieldFixedPosition(-57344, -12824464, 5505024), world.Events.Scene.NativePlayerPosition);
        Assert.Equal(58, player.WalkmeshTriangle);
        Assert.Equal(1344, player.Z);
        Assert.Equal(1, world.Events.Scene.NativePlayerMovementVersion);
        using var resumed = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        resumed.EnsureField();
        for (int tick = 0; tick < 3; tick++)
        {
            var action = new PlayerAction(PlayerActionKind.Move, Horizontal: 0, Vertical: -1);
            Assert.Equal(world.Apply(action), resumed.Apply(action));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        }
        Assert.Null(world.Events.LastFault);
        Assert.Equal(172, world.Events.FieldId);
        Assert.True(player.Y < -3100);
        Assert.False(NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, world.Events.Actors.Single(actor => actor.ObjectId == 6)));
    }

    [Fact]
    public void NaturalSoloGardenPlayerKeepsBlockedProposalAndTakesClearAlternateDirection()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = TestResources.FixturePath("App.Tests.NativePlayerMovementTests.2");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        ActorState player = world.Events.Actors.Single(actor => actor.ObjectId == 0);
        Assert.Equal(62, world.Events.Frame);
        Assert.Equal(128, world.Events.InputAxisDirection);
        Assert.Equal(new[] { 0, 255, 255 }, world.Events.Scene.FieldPartyNativeActorIds);
        Assert.True(NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, player));
        JsonElement before = world.Events.Capture();
        Assert.Throws<NotSupportedException>(() => world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Vertical: 0)));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 0, Vertical: -1)).Accepted);
        Assert.Equal(new FieldFixedPosition(-2401664, -32579696, 2322432), world.Events.Scene.NativePlayerPosition);
        Assert.Equal(1, world.Events.Scene.NativePlayerMovementVersion);
        Assert.Equal(567, player.Z);
        using var resumed = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        resumed.EnsureField();
        for (int tick = 0; tick < 3; tick++)
        {
            var action = new PlayerAction(PlayerActionKind.Move, Horizontal: 0, Vertical: -1);
            Assert.Equal(world.Apply(action), resumed.Apply(action));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        }
        Assert.Null(world.Events.LastFault);
        Assert.Equal(165, world.Events.FieldId);
    }
}
