using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AsyncRfMoveContinuationTests
{
    [Fact]
    public void ActualSecondFloorNpcStartsAsyncRfMoveAndRestoresEveryFollowingTick()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        string source = TestResources.FixturePath("App.Tests.AsyncRfMoveContinuationTests.1");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(139, world.Events.FieldId);
        Assert.Equal(7, world.Events.Frame);
        ActorState npc = world.Events.Actors.Single(actor => actor.ObjectId == 11);
        int initialAnimation = npc.AnimationId;
        bool initialPlaying = npc.AnimationPlaying;
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 0.94423f, Vertical: 0.32929f)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.True(npc.NativeScriptMovement!.Asynchronous);
        Assert.True(npc.NativeScriptMovement.Active);
        Assert.Equal(1576, npc.NativeScriptMovement.Operation);
        Assert.Equal(initialAnimation, npc.AnimationId);
        Assert.Equal(initialPlaying, npc.AnimationPlaying);
        Assert.False(npc.MovementIgnoresWalkmesh);
        Assert.True(npc.X != 10 || npc.Y != -6770);
        using var replay = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        replay.EnsureField();
        for (int tick = 0; tick < 4; tick++)
        {
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
            var wait = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(wait), replay.Apply(wait));
            Assert.Null(world.Events.LastFault);
        }
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
    }
}
