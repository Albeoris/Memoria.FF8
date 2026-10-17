using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MovieResourceRoutingTests
{
    [Fact]
    public void NativeMovieAndCameraUseLogicalProviderPaths()
    {
        RecordingFiles files = new();

        Assert.Equal("resolved:movies/disc02_14h.avi", MovieClock.GetSourcePath(files, 2, 14));
        Assert.Equal("resolved:movies/disc02_14.cam", MovieClock.GetCameraPath(files, 2, 14));
        Assert.Equal(new[] { "movies/disc02_14h.avi", "movies/disc02_14.cam" }, files.Requests);
    }

    [Fact]
    public void InvalidNativeMovieBindingDoesNotAskTheProviderForAFile()
    {
        RecordingFiles files = new();

        Assert.Throws<NotSupportedException>(() => MovieClock.GetSourcePath(files, 4, 0));
        Assert.Throws<NotSupportedException>(() => MovieClock.GetCameraPath(files, 0, 256));
        Assert.Empty(files.Requests);
    }

    [Fact]
    public void PresentationMovieAndCameraUseTheApplicationResolver()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetMovieSource(new NativeMovieSource(1, 7));
        session.RequestMovie(7);
        RecordingFiles files = new();
        using MovieClock clock = new(session, Path.GetTempPath(), resolveGameFile: files.ResolveFile);

        Assert.Equal("resolved:movies/disc01_07h.avi", clock.SourcePath);
        Assert.Equal("resolved:movies/disc01_07.cam", clock.CameraPath);
        Assert.Equal(new[] { "movies/disc01_07h.avi", "movies/disc01_07.cam" }, files.Requests);
    }

    private sealed class RecordingFiles : IGameFileSource
    {
        public List<string> Requests { get; } = [];

        public string ResolveFile(string relativeDataPath)
        {
            Requests.Add(relativeDataPath);
            return "resolved:" + relativeDataPath;
        }
    }
}
