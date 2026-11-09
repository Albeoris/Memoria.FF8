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
        PcMovieCamera? Camera(int frame)
        {
            if (cameras is null)
            {
                return null;
            }
            if ((uint)frame >= cameras.Count)
            {
                throw new NotSupportedException($"FFVII movie camera {name} frame {frame} exceeds its original track; native camera release is required.");
            }
            return cameras[frame];
        }
        return new Ff7MovieSource(path, information, information.FrameCount, 15, Camera);
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
            double startSeconds = state.Frame / source.LogicalFramesPerSecond;
            MovieInformation native = new(source.Information.Width, source.Information.Height, source.LogicalFramesPerSecond, source.NativeFrameCount / source.LogicalFramesPerSecond, source.NativeFrameCount);
            movie = new MoviePresentation(device, mediaToolsDirectory, source.Path, startSeconds, source.Information, audioEnabled: audioEnabled, nativeInformation: native);
        }
        movie.Update((playback, _) =>
        {
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
