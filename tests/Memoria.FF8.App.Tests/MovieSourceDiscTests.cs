using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MovieSourceDiscTests
{
    [Fact]
    public void Prepared_resources_keep_their_disc_across_probe_cam_cache_and_replay_hash_validation()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original movie corpus is required.");
        //Declared resource component: explicit native loader requests, no fabricated gameplay journal.
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.MarkReplayUnavailable("Declared disc/movie resource selection component.");
        MovieClock clock = new(session, directory);
        using GameWorld world = new(session, directory, clock, persistReplayJournal: false);
        session.SetMovieSource(new NativeMovieSource(0, 0));
        session.RequestMovie(0);
        var first = clock.Prepare();
        Assert.Equal(326, first.FrameCount);
        Assert.EndsWith("disc00_00h.avi", clock.SourcePath, StringComparison.OrdinalIgnoreCase);
        string firstHash = MovieClock.GetSourceHash(directory, session.State);
        var firstCamera = world.Events.ReadMovieCameraFlags!(0, 0);
        session.CompleteMovie(false);
        session.SetDiscProcess(session.DiscProcess!.Assign(2));
        Assert.EndsWith("disc00_00h.avi", clock.SourcePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(firstHash, MovieClock.GetSourceHash(directory, session.State));
        session.SetMovieSource(new NativeMovieSource(1, 0));
        session.RequestMovie(0);
        var second = clock.Prepare();
        Assert.Equal(453, second.FrameCount);
        Assert.NotSame(first, second);
        Assert.EndsWith("disc01_00h.avi", clock.SourcePath, StringComparison.OrdinalIgnoreCase);
        string secondHash = MovieClock.GetSourceHash(directory, session.State);
        Assert.NotEqual(firstHash, secondHash);
        var secondCamera = world.Events.ReadMovieCameraFlags!(1, 0);
        Assert.NotEqual(firstCamera.SourceSha256, secondCamera.SourceSha256);
        Assert.Same(firstCamera, world.Events.ReadMovieCameraFlags(0, 0));
        Assert.Same(secondCamera, world.Events.ReadMovieCameraFlags(1, 0));
        ReplayJournalEntry correct = new(ReplayEntryKind.MovieSample, SceneKind.Movie, 804, 0) { MovieSourceHash = secondHash };
        Assert.Equal(secondHash, GameWorld.ValidateMovieSource(correct, session.State, directory));
        Assert.Throws<InvalidDataException>(() => GameWorld.ValidateMovieSource(correct with { MovieSourceHash = firstHash }, session.State, directory));
        session.SetDiscProcess(session.DiscProcess.Assign(3));
        Assert.Equal((byte)1, clock.SourceDiscIndex);
        Assert.Same(second, clock.Prepare());
        session.CompleteMovie(false);
        session.SetMovieSource(new NativeMovieSource(1, 1));
        session.RequestMovie(1);
        var third = clock.Prepare();
        Assert.Equal(278, third.FrameCount);
        Assert.EndsWith("disc01_01h.avi", clock.SourcePath, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(secondHash, MovieClock.GetSourceHash(directory, session.State));
        Assert.Throws<NotSupportedException>(() => MovieClock.GetSourcePath(directory, 255, 0));
        Assert.Throws<NotSupportedException>(() => MovieClock.GetSourcePath(directory, 1, 256));
        GameSession unknown = GameSession.CreateColdProcess();
        unknown.StartNewGame();
        unknown.RequestMovie(0);
        Assert.Throws<NotSupportedException>(() => new MovieClock(unknown, directory).Prepare());
    }
}
