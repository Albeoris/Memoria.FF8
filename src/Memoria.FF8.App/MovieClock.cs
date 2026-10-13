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
    private readonly Dictionary<int, MovieInformation> movies = [];

    public MovieClock(GameSession session, string gameDirectory, string? toolsDirectory = null)
    {
        this.session = session;
        this.gameDirectory = gameDirectory;
        this.toolsDirectory = toolsDirectory ?? Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS") ?? FindCachedTools();
    }

    public MoviePlaybackState State => session.State.MoviePlayback ?? new MoviePlaybackState(0, 0, session.IsMoviePlaying, false, false);
    public string ToolsDirectory => toolsDirectory;

    public MovieInformation Prepare()
    {
        int index = session.State.MovieIndex;
        session.SetReplayMovieSourceHash(GetSourceHash(gameDirectory, index));
        if (!movies.TryGetValue(index, out MovieInformation? information))
        {
            string path = Path.Combine(gameDirectory, "Data", "movies", $"disc00_{index:00}h.avi");
            information = FfmpegMovieDecoder.Probe(toolsDirectory, path);
            movies.Add(index, information);
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

    internal static string GetSourceHash(string gameDirectory, int index)
    {
        var file = new FileInfo(Path.Combine(gameDirectory, "Data", "movies", $"disc00_{index:00}h.avi"));
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
