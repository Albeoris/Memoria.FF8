using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF7.Resources;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private MoviePresentation? movie;
    private Ff7MovieSource? movieSource;
    private Ff7MovieSource? preparedMovieSource;
    private (long Generation, long? Request, int Id)? preparedMovieIdentity;
    private (long Generation, long? Request, int Id)? movieSourceIdentity;
    private readonly MovieSkipGesture movieSkipGesture = new();
    private (long Generation, long? Request)? movieSkipIdentity;

    private void UpdateMovieSkip(TimeSpan elapsed)
    {
        Ff7Movie? state = Session?.Host.Process.Movie;
        var identity = state is { Playing: true } ? (Session!.FieldGeneration, state.RequestId) : ((long, long?)?)null;
        if (movieSkipIdentity != identity)
        {
            movieSkipGesture.Reset();
            movieSkipIdentity = identity;
        }
        if (identity is null || !movieSkipGesture.Update(elapsed, sharedInput.CancelHeld))
        {
            return;
        }
        Ff7MovieSource source = movieSource ?? throw new InvalidOperationException("Movie skipping requires its prepared presentation source.");
        // Skipping pixels and PCM still admits every native movie frame and both
        // 30 Hz field callbacks. Frame-dependent scripts retain their ordinary PCs.
        movie?.Suspend();
        Session!.SetInput(0, 0);
        Session.MovePlayer(0, 0, false);
        for (int frame = state!.Frame + 1; frame < source.NativeFrameCount; frame++)
        {
            if (Session.FieldGeneration != identity.Value.Item1 || Session.Host.Process.Movie is not { Playing: true } active || active.RequestId != state.RequestId)
            {
                break;
            }
            Session.Host.PublishMovieFrame(frame, false);
            PublishMovieCameraFrame(Session.Host, source, frame);
            for (int tick = 0; tick < 2; tick++)
            {
                Session.AdvanceTick(advanceClock: false);
                if (Session.FieldGeneration != identity.Value.Item1 || Session.Host.Process.Movie is not { Playing: true } current || current.RequestId != state.RequestId)
                {
                    break;
                }
            }
        }
        if (Session.FieldGeneration == identity.Value.Item1 && Session.Host.Process.Movie is { Playing: true } remaining && remaining.RequestId == state.RequestId)
        {
            Session.Host.PublishMovieFrame(source.NativeFrameCount - 1, true);
        }
        StopMovie();
        tickRemainder = 0;
        previousActorGeneration = -1;
        UpdateAudio();
    }

    private void DrawMovieSkipHint(Rectangle bounds)
    {
        if (dialogueCanvas is not null && dialoguePreferences?.Invoke() is { } preferences)
        {
            MovieSkipHint.Draw(dialogueCanvas, new UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height), Session?.Host.Process.Movie is { Playing: true } ? SceneKind.Movie : SceneKind.Field, movieSkipGesture, sharedInput.BindingPrompt(ControlAction.Cancel), preferences.UiFontFamily);
        }
    }

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
        return new Ff7MovieSource(path, information, information.FrameCount, 15, frame => cameraTrack?.CameraAt(frame), (frame, words) => cameraTrack?.Publish(frame, words)) { CameraRecordCount = cameras?.Count ?? 0, SnapshotCameraFrame = (frame, words) => cameraTrack?.SnapshotAt(frame, words) };
    }

    private (long Generation, long? Request, int Id) MovieIdentity(Ff7Movie movie)
    {
        return (Session!.FieldGeneration, movie.RequestId, movie.PreparedId);
    }

    private Ff7MovieSource PreparedMovieSource(Ff7Movie movie)
    {
        var identity = MovieIdentity(movie);
        if (preparedMovieSource is null || preparedMovieIdentity != identity)
        {
            preparedMovieSource = resolveMovie(movie.PreparedId);
            preparedMovieIdentity = identity;
        }
        return preparedMovieSource;
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
        if (movie is null || movieSourceIdentity != MovieIdentity(state))
        {
            StopMovie();
            Ff7MovieSource source = PreparedMovieSource(state);
            if (source.NativeFrameCount < 1 || source.LogicalFramesPerSecond != 15 || source.Information.FrameCount < 1)
            {
                throw new NotSupportedException("FFVII movie presentation requires an admitted original frame count and 15 Hz movie timeline.");
            }
            movieSource = source;
            movieSourceIdentity = MovieIdentity(state);
            PublishMovieCameraFrame(Session!.Host, source, state.Frame);
            double startSeconds = state.Frame / source.LogicalFramesPerSecond;
            MovieInformation native = new(source.Information.Width, source.Information.Height, source.LogicalFramesPerSecond, source.NativeFrameCount / source.LogicalFramesPerSecond, source.NativeFrameCount);
            movie = new MoviePresentation(device, mediaToolsDirectory, source.Path, startSeconds, source.Information, audioEnabled: audioEnabled, nativeInformation: native);
        }
        movie.Update((playback, _) =>
        {
            Session!.Host.PublishMovieFrame(playback.FrameIndex, false);
            PublishMovieCameraFrame(Session.Host, movieSource!, playback.FrameIndex);
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

    private void PublishCurrentMovieCameraFrame()
    {
        if (error is null && Session?.Host.Process.Movie is { Playing: true } state)
        {
            var identity = MovieIdentity(state);
            Ff7MovieSource? source = movieSourceIdentity == identity ? movieSource : preparedMovieIdentity == identity ? preparedMovieSource : null;
            if (source is not null)
            {
                PublishMovieCameraFrame(Session.Host, source, state.Frame);
            }
        }
    }

    internal static void PublishMovieCameraFrame(Ff7FieldHost host, Ff7MovieSource source, int frame)
    {
        if (host.PublishMovieCameraFrame(frame, source.CameraRecordCount) is long words)
        {
            source.PublishCameraFrame?.Invoke(frame, words);
        }
    }

    private PcFieldCamera? CurrentMovieCamera()
    {
        if (Session?.Host.Process.Movie is not { Playing: true } state)
        {
            return null;
        }
        var identity = MovieIdentity(state);
        Ff7MovieSource? source = movieSourceIdentity == identity ? movieSource : preparedMovieIdentity == identity ? preparedMovieSource : null;
        return ResolveMovieCamera(Session.Host, source);
    }

    private PcFieldCamera? BlinkCamera()
    {
        Ff7Session session = Session ?? throw new InvalidOperationException("Native blink presentation requires an attached session.");
        Ff7MovieSource? source = session.Host.Process.Movie is { Playing: true, CameraDisabled: false } state && !session.Host.Process.MovieCameraDisabled ? PreparedMovieSource(state) : null;
        return ResolveBlinkCamera(session.Host, source, session.Field.Cameras[0]);
    }

    internal static PcFieldCamera? ResolveBlinkCamera(Ff7FieldHost host, Ff7MovieSource? source, PcFieldCamera fieldCamera)
    {
        if (host.Process.Movie is not { Playing: true } movie || movie.CameraDisabled || host.Process.MovieCameraDisabled)
        {
            return fieldCamera;
        }
        Ff7MovieCameraConsumption consumption = movie.CameraConsumption ?? throw new InvalidOperationException("Native blink presentation requires the saved movie camera owner.");
        if (movie.Frame < consumption.Frame)
        {
            throw new InvalidDataException("Native blink presentation cannot read a movie frame before its saved camera owner.");
        }
        long words = consumption.Frame == movie.Frame ? consumption.WordsBeforeRead : consumption.WordsAfterRead;
        Func<int, long, PcMovieCamera?> snapshot = source?.SnapshotCameraFrame ?? throw new NotSupportedException("Native blink presentation requires a read-only original movie camera source.");
        return snapshot(movie.Frame, words)?.Camera;
    }

    internal static PcFieldCamera? ResolveMovieCamera(Ff7FieldHost host, Ff7MovieSource? source)
    {
        Ff7Movie? state = host.Process.Movie;
        if (state is not { Playing: true } || state.CameraDisabled || host.Process.MovieCameraDisabled)
        {
            return null;
        }
        return source?.CameraAtFrame?.Invoke(state.Frame)?.Camera;
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
        movieSourceIdentity = null;
    }
}

internal sealed class Ff7MovieCameraTrack(IReadOnlyList<PcMovieCamera> cameras)
{
    private static readonly PcMovieCamera empty = new(new PcFieldCamera(default, default, default, 0, default, 0, 0), 0);
    private int publishedFrame = -1;
    private long consumedWords;

    public void Publish(int frame, long wordsBeforeRead)
    {
        if (frame < 0 || frame < publishedFrame || wordsBeforeRead < 0 || wordsBeforeRead % 10 != 0)
        {
            throw new InvalidDataException("FFVII movie camera timeline must advance monotonically.");
        }
        if (frame == publishedFrame && wordsBeforeRead == consumedWords)
        {
            return;
        }
        // Native camera ownership supplies the saved read budget independently of the absolute movie frame.
        consumedWords = wordsBeforeRead;
        publishedFrame = frame;
    }

    public PcMovieCamera CameraAt(int frame)
    {
        if (frame != publishedFrame)
        {
            throw new InvalidOperationException("FFVII camera lookup requires its published logical movie frame.");
        }
        return SnapshotAt(frame, consumedWords);
    }

    public PcMovieCamera SnapshotAt(int frame, long wordsBeforeRead)
    {
        if (frame < 0 || wordsBeforeRead < 0 || wordsBeforeRead % 10 != 0)
        {
            throw new InvalidDataException("A movie camera snapshot requires its native frame and whole-record read budget.");
        }
        long lastReadableWords = cameras.Count * 10L - 2;
        return wordsBeforeRead > lastReadableWords || (uint)frame >= cameras.Count ? empty : cameras[frame];
    }
}
