using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Tracks decoded clip frames separately from script simulation frames.</summary>
internal sealed class MovieClock
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string Path, long Length, long Modified), string> SourceHashes = new();
    private readonly GameSession session;
    private readonly string gameDirectory;
    private readonly string toolsDirectory;
    private readonly Dictionary<NativeMovieSource, MovieInformation> movies = [];

    public MovieClock(GameSession session, string gameDirectory, string? toolsDirectory = null)
    {
        this.session = session;
        this.gameDirectory = gameDirectory;
        this.toolsDirectory = toolsDirectory ?? Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS") ?? FindCachedTools();
    }

    public MoviePlaybackState State => session.State.MoviePlayback ?? new MoviePlaybackState(0, 0, session.IsMoviePlaying, false, false);
    public string ToolsDirectory => toolsDirectory;
    public byte SourceDiscIndex => RequireSource(session.State).DiscIndex;
    public string SourcePath => GetSourcePath(gameDirectory, SourceDiscIndex, RequireSource(session.State).MovieIndex);

    public MovieInformation Prepare()
    {
        if (!session.IsMoviePlaying)
        {
            throw new NotSupportedException("Movie decoding requires an active native playback request.");
        }
        NativeMovieSource source = RequireSource(session.State);
        session.SetReplayMovieSourceHash(GetSourceHash(gameDirectory, source.DiscIndex, source.MovieIndex));
        if (!movies.TryGetValue(source, out MovieInformation? information))
        {
            information = FfmpegMovieDecoder.Probe(toolsDirectory, SourcePath);
            movies.Add(source, information);
        }
        if (session.State.MoviePlayback is null)
        {
            int frame = AbsoluteFrame(session.State.MovieSeconds, information);
            session.SetMoviePlayback(new MoviePlaybackState(frame, information.FrameCount - 1, session.IsMoviePlaying, false, false));
        }
        else if (session.State.MoviePlayback.TerminalFrame != information.FrameCount - 1)
        {
            throw new InvalidDataException("The saved movie frame count does not match the installed clip.");
        }
        return information;
    }

    internal static NativeMovieSource RequireSource(GameState state)
    {
        NativeMovieSource source = state.MovieSource ?? throw new NotSupportedException("Movie media requires its executed MOVIEREADY disc/resource binding; recreate authentic history.");
        source.Validate();
        if (state.Scene == SceneKind.Movie && source.MovieIndex != state.MovieIndex)
        {
            throw new InvalidDataException("The active movie differs from its prepared resource index.");
        }
        return source;
    }

    internal static string GetSourcePath(string gameDirectory, int discIndex, int movieIndex)
    {
        ValidateResource(discIndex, movieIndex);
        return Path.Combine(gameDirectory, "Data", "movies", $"disc{discIndex:00}_{movieIndex:00}h.avi");
    }

    internal static string GetCameraPath(string gameDirectory, int discIndex, int movieIndex)
    {
        ValidateResource(discIndex, movieIndex);
        return Path.Combine(gameDirectory, "Data", "movies", $"disc{discIndex:00}_{movieIndex:00}.cam");
    }

    private static void ValidateResource(int discIndex, int movieIndex)
    {
        if ((uint)discIndex > 3 || (uint)movieIndex > byte.MaxValue)
        {
            throw new NotSupportedException("Normal field movie resource selection requires its known native disc0..3 and original clip0..255.");
        }
    }

    internal static string GetSourceHash(string gameDirectory, GameState state)
    {
        NativeMovieSource source = RequireSource(state);
        return GetSourceHash(gameDirectory, source.DiscIndex, source.MovieIndex);
    }

    internal static string GetSourceHash(string gameDirectory, int discIndex, int movieIndex)
    {
        var file = new FileInfo(GetSourcePath(gameDirectory, discIndex, movieIndex));
        return SourceHashes.GetOrAdd((file.FullName, file.Length, file.LastWriteTimeUtc.Ticks), identity =>
        {
            using FileStream stream = File.OpenRead(identity.Path);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
        });
    }

    public void Skip()
    {
        Prepare();
        session.CompleteMovie(true);
    }

    public static int AbsoluteFrame(double startSeconds, MovieInformation information)
    {
        return Math.Clamp((int)Math.Ceiling(startSeconds * information.FramesPerSecond - 1e-9), 0, information.FrameCount - 1);
    }

    private static string FindCachedTools()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string cache = Path.Combine(directory.FullName, "tools", "ffmpeg");
            if (Directory.Exists(cache))
            {
                string? tool = Directory.EnumerateFiles(cache, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (tool is not null)
                {
                    return Path.GetDirectoryName(tool)!;
                }
            }
            directory = directory.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, "media-tools");
    }
}
