using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class TrainCouplingAnimationTests
{
    [Fact]
    public void OriginalMovieCallbacksReplaceTheEnteringRunPoseWithOrdinaryPartyIdle()
    {
        string snapshot = TestResources.FixturePath("App.Tests.TrainCouplingAnimationTests.2");
        byte[] original = File.ReadAllBytes(snapshot);
        string game = TestResources.GameDirectory;
        var session = new GameSession(GameSession.Load(snapshot));
        var clock = new MovieClock(session, game);
        using var world = new GameWorld(session, game, clock, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(902, world.Field!.Id);
        Assert.True(session.IsMoviePlaying);
        Assert.Equal(10, session.State.MovieIndex);
        Assert.True(world.Events.IsUserControlEnabled);
        ActorState player = Assert.IsType<ActorState>(world.Events.PlayerActor);
        Assert.Equal(player.RunAnimationId, player.AnimationId);
        Assert.Equal(9f, player.AnimationFrame);
        Assert.True(player.NativePartyFlags!.AutoAnimation4);
        var enteringPose = world.Field.Models[player.RenderModelId].Pose(player.AnimationId, player.AnimationFrame);
        NativeActorAnimationHeader beforeHeader = Assert.IsType<NativeActorAnimationHeader>(player.NativeAnimationHeader);

        Assert.Equal(1, clock.AdvanceFrames(1));

        Assert.True(session.IsMoviePlaying);
        Assert.Equal(player.BaseAnimationId, player.AnimationId);
        Assert.Equal(0f, player.AnimationFrame);
        Assert.False(enteringPose.SequenceEqual(world.Field.Models[player.RenderModelId].Pose(player.AnimationId, player.AnimationFrame)));
        Assert.Equal(beforeHeader, player.NativeAnimationHeader);
        Assert.Equal(player.BaseAnimationId, world.CreateVisual(player).AnimationId);
        foreach (int nativeId in world.Events.Scene.FieldPartyNativeActorIds.Where(id => id != byte.MaxValue))
        {
            ActorState member = world.Events.Actors[world.Events.Scene.NativeActorObjectIds[nativeId]];
            Assert.Equal(member.BaseAnimationId, member.AnimationId);
        }

        session.SetEventState(world.Events.Capture());
        GameState restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        var independentSession = new GameSession(restored);
        var independentClock = new MovieClock(independentSession, game);
        using var independent = new GameWorld(independentSession, game, independentClock, validateWorldPresentation: false);
        independent.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), independent.Events.Capture()));
        Assert.Equal(148, clock.AdvanceFrames(148));
        Assert.Equal(148, independentClock.AdvanceFrames(148));
        Assert.Equal(player.BaseAnimationId, player.AnimationId);
        Assert.Equal(0f, player.AnimationFrame);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), independent.Events.Capture()));
        Assert.Equal(original, File.ReadAllBytes(snapshot));
    }

    [Fact]
    public void OriginalCouplingReturnFinishesItsClimbAndRestoresTheIndependentLocomotionPose()
    {
        string snapshot = TestResources.FixturePath("App.Tests.TrainCouplingAnimationTests.1");
        byte[] original = File.ReadAllBytes(snapshot);
        var session = new GameSession(GameSession.Load(snapshot));
        using var world = new GameWorld(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(902, world.Field!.Id);
        Assert.Equal(2500, world.Events.Frame);
        ActorState player = Assert.IsType<ActorState>(world.Events.PlayerActor);
        Assert.Equal(6, player.AnimationId);
        Assert.Equal(58f, player.AnimationFrame);
        Assert.False(world.Events.IsUserControlEnabled);
        var climbingPose = world.Field.Models[player.RenderModelId].Pose(player.AnimationId, player.AnimationFrame);
        bool poseAdvanced = false;
        for (int callback = 0; callback < 48 && !world.Events.IsUserControlEnabled; callback++)
        {
            world.Events.Step();
            Assert.Null(world.Events.LastFault);
            poseAdvanced |= !climbingPose.SequenceEqual(world.Field.Models[player.RenderModelId].Pose(player.AnimationId, player.AnimationFrame));
        }
        Assert.True(poseAdvanced);
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.Equal(2541, world.Events.Frame);
        Assert.True(player.NativePartyFlags!.AutoAnimation4);
        Assert.Equal(player.BaseAnimationId, player.AnimationId);
        Assert.Equal(0f, player.AnimationFrame);
        NativeActorAnimationHeader header = Assert.IsType<NativeActorAnimationHeader>(player.NativeAnimationHeader);
        Assert.Equal(6, header.AnimationId);
        Assert.Equal((ushort)1200, header.Frame);
        Assert.Equal((ushort)0x1800, header.Flags);
        Assert.Equal(player.BaseAnimationId, world.CreateVisual(player).AnimationId);
        Assert.Equal(0f, world.CreateVisual(player).AnimationFrame);

        session.SetEventState(world.Events.Capture());
        GameState restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        using var independent = new GameWorld(new GameSession(restored), TestResources.GameDirectory, validateWorldPresentation: false);
        independent.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), independent.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), independent.Render(TimeSpan.Zero));
        PlayerAction move = new(PlayerActionKind.Move, Vertical: 1);
        Assert.True(world.Apply(move).Accepted);
        Assert.True(independent.Apply(move).Accepted);
        Assert.Equal(player.RunAnimationId, player.AnimationId);
        Assert.Equal(player.AnimationSpeed / 16f, player.AnimationFrame);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), independent.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), independent.Render(TimeSpan.Zero));
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(player.BaseAnimationId, player.AnimationId);
        Assert.Equal(0f, player.AnimationFrame);
        Assert.Equal(original, File.ReadAllBytes(snapshot));
    }
}
