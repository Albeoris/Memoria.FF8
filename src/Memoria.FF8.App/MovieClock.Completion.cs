using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class MovieClock
{
    public MovieCompletionFrame? ReadSkippedCompletionFrame()
    {
        if (session.IsMoviePlaying || session.State.MoviePlayback is not { Completed: true, Skipped: true } playback || session.State.MovieSource is not NativeMovieSource source || !movies.TryGetValue(source, out MovieInformation? native))
        {
            return null;
        }
        string path = SourcePath;
        MovieInformation presentation = string.Equals(path, GetSourcePath(gameDirectory, source.DiscIndex, source.MovieIndex), StringComparison.OrdinalIgnoreCase) ? native : presentationMovies.TryGetValue(path, out MovieInformation? replacement) ? replacement : FfmpegMovieDecoder.Probe(toolsDirectory, path);
        // Retain the decoded surface at the actual completion callback, including early CAM completion.
        // This is presentation only: seeking must not advance the VM or replace its saved playback.
        return MovieCompletionFrame.Read(toolsDirectory, path, presentation, playback.FrameIndex / native.FramesPerSecond);
    }
}
