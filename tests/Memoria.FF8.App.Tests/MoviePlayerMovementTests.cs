using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MoviePlayerMovementTests
{
    [Theory]
    [InlineData(false, 15859)]
    [InlineData(true, 5392)]
    public void MovieMovementMatchesDecodedCallbacksAndRestoresWithoutRepeatingMovement(bool walking, int speed)
    {
        string snapshot = TestResources.FixturePath("App.Tests.TrainCouplingAnimationTests.2");
        byte[] original = File.ReadAllBytes(snapshot);
        string game = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(snapshot));
        MovieClock clock = new(session, game);
        using GameWorld world = new(session, game, clock, validateWorldPresentation: false);
        world.EnsureField();
        GameSession referenceSession = new(GameSession.Load(snapshot));
        MovieClock referenceClock = new(referenceSession, game);
        using GameWorld reference = new(referenceSession, game, referenceClock, validateWorldPresentation: false);
        reference.EnsureField();
        Assert.True(session.IsMoviePlaying);
        Assert.True(world.Events.IsUserControlEnabled);
        ushort buttons = walking ? WalkButton(session) : (ushort)0;
        Assert.True(world.Apply(new(PlayerActionKind.FieldButtons, Value: buttons)).Accepted);
        Assert.True(reference.Apply(new(PlayerActionKind.FieldButtons, Value: buttons)).Accepted);
        ActorState player = Assert.IsType<ActorState>(world.Events.PlayerActor);
        var enteringPosition = (player.X, player.Y, player.Z);
        long frame = world.Events.Frame;
        MovieInformation information = referenceClock.Prepare();
        int next = Math.Max(2, referenceClock.State.FrameIndex + 1);
        MoviePlaybackState playback = new(next, information.FrameCount - 1, true, false, false);

        PlayerActionResult result = world.Apply(new(PlayerActionKind.Move, Vertical: 1));
        Assert.True(reference.AdvanceMovieFrame(playback, next / information.FramesPerSecond, 0, 1));

        Assert.True(result.Accepted);
        Assert.Equal(1, result.FramesAdvanced);
        Assert.Equal(frame + 1, world.Events.Frame);
        Assert.NotEqual(enteringPosition, (player.X, player.Y, player.Z));
        Assert.Equal(speed, player.MovementSpeed);
        Assert.Equal(walking ? player.WalkAnimationId : player.RunAnimationId, player.AnimationId);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), reference.Events.Capture()));
        Assert.Equal(session.State.MoviePlayback, referenceSession.State.MoviePlayback);

        JsonElement afterMovement = reference.Events.Capture();
        Assert.True(reference.AdvanceMovieFrame(playback, next / information.FramesPerSecond, 0, 1));
        Assert.True(JsonElement.DeepEquals(afterMovement, reference.Events.Capture()));

        session.SetEventState(world.Events.Capture());
        GameSession resumed = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        MovieClock resumedClock = new(resumed, game);
        using GameWorld restored = new(resumed, game, resumedClock, validateWorldPresentation: false);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.True(world.Apply(new(PlayerActionKind.Move, Vertical: 1)).Accepted);
        Assert.True(restored.Apply(new(PlayerActionKind.Move, Vertical: 1)).Accepted);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(session.State.MoviePlayback, resumed.State.MoviePlayback);
        Assert.True(world.Apply(new(PlayerActionKind.FieldButtons, Value: 0)).Accepted);
        Assert.True(restored.Apply(new(PlayerActionKind.FieldButtons, Value: 0)).Accepted);
        var releasePosition = (player.X, player.Y, player.Z);
        Assert.Equal(1, world.Apply(new(PlayerActionKind.Wait)).FramesAdvanced);
        Assert.Equal(1, restored.Apply(new(PlayerActionKind.Wait)).FramesAdvanced);
        Assert.Equal(releasePosition, (player.X, player.Y, player.Z));
        Assert.Equal(player.BaseAnimationId, player.AnimationId);
        Assert.Equal(0f, player.AnimationFrame);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Null(world.Events.LastFault);
        Assert.Equal(original, File.ReadAllBytes(snapshot));
    }

    [Theory]
    [InlineData(false, 7929)]
    [InlineData(true, 2696)]
    public void OrdinaryMovementRetainsItsNativeSpeed(bool walking, int speed)
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.TrainCouplingAnimationTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        for (int callback = 0; callback < 48 && !world.Events.IsUserControlEnabled; callback++)
        {
            world.Events.Step();
            Assert.Null(world.Events.LastFault);
        }
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.False(session.IsMoviePlaying);
        Assert.True(world.Apply(new(PlayerActionKind.FieldButtons, Value: walking ? WalkButton(session) : 0)).Accepted);
        Assert.True(world.Apply(new(PlayerActionKind.Move, Vertical: 1)).Accepted);
        Assert.Equal(speed, world.Events.PlayerActor!.MovementSpeed);
    }

    [Fact]
    public void ScriptOwnedClimbIgnoresMovieAxesAndStillRunsItsOrdinaryCallback()
    {
        string snapshot = TestResources.FixturePath("App.Tests.TrainCouplingAnimationTests.1");
        GameSession session = new(GameSession.Load(snapshot));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        GameSession referenceSession = new(GameSession.Load(snapshot));
        using GameWorld reference = new(referenceSession, TestResources.GameDirectory, validateWorldPresentation: false);
        reference.EnsureField();
        Assert.False(world.Events.IsUserControlEnabled);
        long frame = world.Events.Frame;

        // This original script-owned state exercises the callback's input gate directly.
        world.Events.StepMovieFrame(0, 1);
        reference.Events.Step();

        Assert.Equal(frame + 1, world.Events.Frame);
        Assert.False(world.Events.IsUserControlEnabled);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), reference.Events.Capture()));
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void ScriptRestoringControlAdmitsHeldAxesOnThatSameCallback()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.TrainCouplingAnimationTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        for (int callback = 0; callback < 40; callback++)
        {
            world.Events.Step();
        }
        Assert.False(world.Events.IsUserControlEnabled);
        ActorState player = Assert.IsType<ActorState>(world.Events.PlayerActor);
        var before = (player.X, player.Y, player.Z);
        long frame = world.Events.Frame;

        world.Events.StepMovieFrame(0, 1);

        Assert.Equal(frame + 1, world.Events.Frame);
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.NotEqual(before, (player.X, player.Y, player.Z));
        Assert.Equal(player.RunAnimationId, player.AnimationId);
        Assert.Null(world.Events.LastFault);
    }

    private static ushort WalkButton(GameSession session)
    {
        return Enumerable.Range(0, 16).Select(bit => (ushort)(1 << bit)).First(button => (session.InputConfiguration!.Map(button) & 0x10) != 0);
    }
}
