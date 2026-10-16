using Memoria.FF8.Events;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class QuistisMovieBoundaryTests
{
    [Fact]
    public void PublishedAfterMovieCheckpointRestoresAndAdvancesWithoutReplayingTheOpening()
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original field corpus is required.");
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        string source = Path.Combine(directory!.FullName, "../fixtures/external-fixture.json");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, game);
        world.EnsureField();
        Assert.Equal(180, session.State.FieldId);
        Assert.Equal(74, world.Events.Frame);
        Assert.True(world.CanPresentCurrentField);
        Assert.NotEmpty(world.Render(TimeSpan.Zero));
        PlayerActionResult continued = world.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.True(continued.Accepted);
        Assert.Equal(1, continued.FramesAdvanced);
        Assert.Equal(75, world.Events.Frame);
        Assert.NotEmpty(world.Render(TimeSpan.Zero));
        Assert.Null(world.Events.LastFault);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovieCleanupWaitsForTheFirstOrdinaryFieldDraw(bool skip)
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original Quistis movie and field corpus is required.");
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        Assert.NotNull(directory);
        string source = Path.Combine(directory.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(File.Exists(source), "The preserved authentic field181 movie1 startup save is required.");
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameSession.Load(source));
        MovieClock clock = new(session, game, Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS"));
        using GameWorld world = new(session, game, clock);
        world.EnsureField();
        Assert.Equal(SceneKind.Movie, session.State.Scene);
        Assert.Equal(181, session.State.FieldId);
        Assert.Equal(1, session.State.MovieIndex);
        Assert.Equal(1, world.Events.Scene.NativeMovieMask!.Playback!.AppliedRecord);
        MovieInformation information = clock.Prepare();

        if (skip)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.SkipMovie)).Accepted);
        }
        else
        {
            for (int record = 0; session.IsMoviePlaying && record < information.FrameCount; record++)
            {
                world.AdvanceMovieFrame(new MoviePlaybackState(record, information.FrameCount - 1, true, false, false), record / information.FramesPerSecond);
            }
        }

        Assert.False(session.IsMoviePlaying);
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(181, session.State.FieldId);
        Assert.True(session.State.MoviePlayback!.Completed);
        Assert.Equal(skip, session.State.MoviePlayback.Skipped);
        Assert.Equal(world.Events.Frame, world.Events.Scene.MovieActors!.EndFrame);
        Assert.False(world.CanPresentCurrentField);
        Assert.Null(world.Events.LastFault);

        PlayerActionResult next = world.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.True(next.Accepted);
        Assert.Equal(1, next.FramesAdvanced);
        if (world.Events.NextTrigger.Kind == PlayerTriggerKind.FieldTransition)
        {
            Assert.False(world.CanPresentCurrentField);
            Assert.NotEqual(world.Events.FieldId, session.State.FieldId);
        }
        else
        {
            Assert.True(world.CanPresentCurrentField);
            Assert.NotEmpty(world.Render(TimeSpan.Zero));
        }
        Assert.Null(world.Events.LastFault);
        Assert.Equal(original, File.ReadAllBytes(source));
    }
}
