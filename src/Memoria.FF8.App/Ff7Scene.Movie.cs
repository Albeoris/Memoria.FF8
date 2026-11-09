using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF7.Resources;
using Memoria.FF8.Media;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private MoviePresentation? movie;
    private Ff7MovieSource? movieSource;
    private int movieId = -1;

    public static Ff7MovieSource ResolveMovie(Ff7Installation installation, string mediaToolsDirectory, int movieId)
    {
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(installation);
        if ((uint)movieId >= native.MovieFiles.Count)
        {
            throw new InvalidDataException($"FFVII movie index {movieId} is outside the admitted native table.");
        }
        string name = native.MovieFiles[movieId];
        string path = Path.Combine(installation.GetRequired(Ff7ResourceRole.Movies).Path, name);
        MovieInformation information = FfmpegMovieDecoder.Probe(mediaToolsDirectory, path);
        if (information.FramesPerSecond != 15)
        {
            throw new NotSupportedException("FFVII movie presentation requires the admitted 15 fps original-resource timeline.");
        }
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MovieCameraArchive).Path);
        IReadOnlyList<LgpEntry> members = archive.Find(Path.ChangeExtension(name, ".cam"));
        IReadOnlyList<PcMovieCamera>? cameras = members.Count switch
        {
            0 => null,
            1 => PcMovieCameras.Read(archive.ReadEntry(members[0])),
            _ => throw new InvalidDataException($"FFVII movie '{name}' has ambiguous original camera members.")
        };
        Ff7MovieCameraTrack? cameraTrack = cameras is null ? null : new Ff7MovieCameraTrack(cameras);
        return new Ff7MovieSource(path, information, information.FrameCount, 15, frame => cameraTrack?.CameraAt(frame), frame => cameraTrack?.Publish(frame));
    }

    private void UpdateMovie()
    {
        Ff7Movie? state = Session?.Host.Process.Movie;
        if (state is not { Playing: true })
        {
            if (movie is not null)
            {
                StopMovie();
            }
            return;
        }
        if (movie is null || movieId != state.PreparedId)
        {
            StopMovie();
            Ff7MovieSource source = resolveMovie(state.PreparedId);
            if (source.NativeFrameCount < 1 || source.LogicalFramesPerSecond != 15 || source.Information.FrameCount < 1)
            {
                throw new NotSupportedException("FFVII movie presentation requires an admitted original frame count and 15 Hz movie timeline.");
            }
            movieSource = source;
            movieId = state.PreparedId;
            source.PublishCameraFrame?.Invoke(state.Frame);
            double startSeconds = state.Frame / source.LogicalFramesPerSecond;
            MovieInformation native = new(source.Information.Width, source.Information.Height, source.LogicalFramesPerSecond, source.NativeFrameCount / source.LogicalFramesPerSecond, source.NativeFrameCount);
            movie = new MoviePresentation(device, mediaToolsDirectory, source.Path, startSeconds, source.Information, audioEnabled: audioEnabled, nativeInformation: native);
        }
        movie.Update((playback, _) =>
        {
            movieSource!.PublishCameraFrame?.Invoke(playback.FrameIndex);
            Session!.Host.PublishMovieFrame(playback.FrameIndex, false);
            return true;
        });
        if (movie.Error is string movieError)
        {
            throw new InvalidDataException($"FFVII movie decoder failed: {movieError}");
        }
        if (movie.Finished)
        {
            Session!.Host.PublishMovieFrame(movieSource!.NativeFrameCount - 1, true);
            StopMovie();
        }
    }

    private PcFieldCamera? CurrentMovieCamera()
    {
        Ff7Movie? state = Session?.Host.Process.Movie;
        if (state is not { Playing: true } || state.CameraDisabled || Session!.Host.Process.MovieCameraDisabled)
        {
            return null;
        }
        return movieSource?.CameraAtFrame?.Invoke(state.Frame)?.Camera;
    }

    private void DrawMovie(SpriteBatch batch, Rectangle bounds)
    {
        if (movie is { HasFrame: true })
        {
            movie.Draw(batch, bounds);
        }
    }

    private void StopMovie()
    {
        movie?.Dispose();
        movie = null;
        movieSource = null;
        movieId = -1;
    }
}

internal sealed class Ff7MovieCameraTrack(IReadOnlyList<PcMovieCamera> cameras)
{
    private static readonly PcMovieCamera empty = new(new PcFieldCamera(default, default, default, 0, default, 0, 0), 0);
    private int publishedFrame = -1;
    private long consumedWords;

    public void Publish(int frame)
    {
        if (frame < 0 || frame < publishedFrame)
        {
            throw new InvalidDataException("FFVII movie camera timeline must advance monotonically.");
        }
        if (frame == publishedFrame)
        {
            return;
        }
        // Each distinct logical movie frame consumes the original ten-DWORD camera record.
        // Recovering at a saved frame restores its preceding consumption without replaying scripts.
        consumedWords = checked((long)frame * 10);
        publishedFrame = frame;
    }

    public PcMovieCamera CameraAt(int frame)
    {
        if (frame != publishedFrame)
        {
            throw new InvalidOperationException("FFVII camera lookup requires its published logical movie frame.");
        }
        long lastReadableWords = cameras.Count * 10L - 2;
        return consumedWords > lastReadableWords || (uint)frame >= cameras.Count ? empty : cameras[frame];
    }
}
