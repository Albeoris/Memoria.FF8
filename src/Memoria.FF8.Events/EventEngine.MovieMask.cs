using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public sealed record NativeMovieMaskPlayback([property: JsonRequired] int MovieIndex, [property: JsonRequired] string CameraSha256, [property: JsonRequired] int AppliedRecord, [property: JsonRequired] int? DrawRecord, [property: JsonRequired] byte? DrawValue);
public sealed record NativeMovieMaskState([property: JsonRequired] int Version, [property: JsonRequired] byte Value, [property: JsonRequired] NativeMovieMaskPlayback? Playback);

public sealed partial class EventEngine
{
    public Func<byte, int, NativeMovieCameraFlags>? ReadMovieCameraFlags { get; set; }

    private NativeMovieCameraFlags RequireMovieCameraFlags(byte disc, int movie)
    {
        NativeMovieCameraFlags flags = ReadMovieCameraFlags?.Invoke(disc, movie) ?? throw new NotSupportedException("Native movie mask control requires the actual original CAM resource.");
        flags.Validate();
        return flags;
    }

    private string? UnsupportedMovieMask(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.SETDCAMERA && operation.Opcode != Jsm.Opcode.MOVIEREADY)
        {
            return null;
        }
        try
        {
            if (Scene.NativeMovieMask is not { Version: 1 })
            {
                throw new NotSupportedException("Native movie mask control requires its executed normal field-loader owner.");
            }
            if (operation.Opcode == Jsm.Opcode.SETDCAMERA)
            {
                if (fiber.Stack.Count < 1 || !fiber.Stack.PeekValue().ToWidth(1).IsExact)
                {
                    throw new NotSupportedException("SETDCAMERA requires one known native low BYTE; its operands are preserved.");
                }
            }
            else
            {
                if (fiber.Stack.Count < 2 || !fiber.Stack.Capture()[1].IsExact)
                {
                    throw new NotSupportedException("MOVIEREADY requires its known native clip index.");
                }
                if (!services.CanStoreMovieSource || services.DiscProcess is not NativeDiscProcessState disc || UnsupportedNativeDiscAliases() is not null)
                {
                    throw new NotSupportedException("MOVIEREADY requires its actual canonical disc and persistent prepared resource owner.");
                }
                disc.Validate();
                NativeMovieSource source = new(disc.CurrentDiscIndex, fiber.Stack.Skip(1).First());
                source.Validate();
                _ = RequireMovieCameraFlags(source.DiscIndex, source.MovieIndex);
            }
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or IOException or ArgumentException)
        {
            return exception.Message;
        }
    }

    private void PrepareNativeMovieMask(Fiber fiber, int movie)
    {
        NativeDiscProcessState disc = services.DiscProcess ?? throw new NotSupportedException("Native movie preparation has no actual disc owner.");
        NativeMovieSource source = new(disc.CurrentDiscIndex, movie);
        source.Validate();
        NativeMovieCameraFlags flags = RequireMovieCameraFlags(source.DiscIndex, movie);
        try
        {
            services.SetMovieSource(source);
        }
        catch (NotSupportedException exception)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native movie resource service rejected preparation; PC and operands preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, Jsm.Opcode.MOVIEREADY.ToString(), message);
            throw new EventScriptException(message, exception);
        }
        Scene.NativeMovieMask = new NativeMovieMaskState(1, 1, new NativeMovieMaskPlayback(movie, flags.SourceSha256, -1, null, null));
    }

    private NativeMovieSource RequirePreparedMovieSource(int movie)
    {
        NativeMovieSource source = services.MovieSource ?? throw new NotSupportedException("Native movie requires its executed MOVIEREADY resource binding.");
        source.Validate();
        if (source.MovieIndex != movie)
        {
            throw new InvalidDataException("Native movie differs from its prepared resource index.");
        }
        return source;
    }

    private NativeMovieMaskState PrepareNativeMovieMaskStart()
    {
        NativeMovieMaskState state = Scene.NativeMovieMask ?? throw new NotSupportedException("Native movie start has no mask-control owner.");
        NativeMovieMaskPlayback playback = state.Playback ?? throw new NotSupportedException("Native movie start has no prepared CAM binding.");
        NativeMovieCameraFlags flags = RequireMovieCameraFlags(RequirePreparedMovieSource(playback.MovieIndex).DiscIndex, playback.MovieIndex);
        if (flags.Flags.Length == 2)
        {
            throw new NotSupportedException("Native CAM terminal inside movie startup requires its unimplemented initial decoded resource clock.");
        }
        if (playback.AppliedRecord != -1 || flags.SourceSha256 != playback.CameraSha256)
        {
            throw new NotSupportedException("Native movie start differs from its prepared CAM source.");
        }
        //51F390 ->55A530 presents record0 inside the MOVIE handler, before remaining VM operations.
        return state with { Value = flags.Apply(0, state.Value), Playback = playback with { AppliedRecord = 0 } };
    }

    private void StepNativeMovieMask()
    {
        if (Scene.MovieActors?.Active != true)
        {
            return;
        }
        NativeMovieMaskState state = Scene.NativeMovieMask ?? throw new NotSupportedException("Active native movie has no mask-control owner.");
        NativeMovieMaskPlayback playback = state.Playback ?? throw new NotSupportedException("Active native movie has no CAM binding.");
        int record = playback.AppliedRecord == 0 ? 1 : CurrentMoviePlayback?.FrameIndex ?? throw new NotSupportedException("Native movie callback has no decoded frame owner.");
        if (record != playback.AppliedRecord + 1)
        {
            throw new NotSupportedException("Native movie mask callbacks require consecutive decoded CAM records.");
        }
        NativeMovieCameraFlags flags = RequireMovieCameraFlags(RequirePreparedMovieSource(playback.MovieIndex).DiscIndex, playback.MovieIndex);
        if (flags.SourceSha256 != playback.CameraSha256)
        {
            throw new InvalidDataException("Native movie CAM source changed during playback.");
        }
        if ((uint)record >= flags.Flags.Length)
        {
            throw new NotSupportedException($"Native movie CAM record is outside its source bank: movie={playback.MovieIndex}, field={FieldId}, callback={Frame}, applied={playback.AppliedRecord}, requested={record}, camRecords={flags.Flags.Length}, decoded={CurrentMoviePlayback}, rootDecoded={services.CurrentMoviePlayback}.");
        }
        //4767B0 draws the MSK before55A620 applies this record's flags. Keep both phases.
        Scene.NativeMovieMask = state with { Value = flags.Apply(record, state.Value), Playback = playback with { AppliedRecord = record, DrawRecord = record, DrawValue = state.Value } };
        if (record == flags.Flags.Length - 1)
        {
            //55A620 compares the one-based Bink frame to the CAM terminal after consuming its flags.
            //5304B0 closes the ordinary movie now; an AVI tail is not another native callback.
            if (services is not GameSession session)
            {
                throw new NotSupportedException("Native CAM-terminal cleanup requires the current movie session owner.");
            }
            session.CompleteMovie(false);
        }
    }

    private void ValidateNativeMovieMask(SceneEventState scene, int movie, long frame, MoviePlaybackState? decoded, NativeMovieSource? source)
    {
        NativeMovieMaskState? state = scene.NativeMovieMask;
        if (state is null)
        {
            if (scene.NativeFieldInitialization is not null)
            {
                throw new InvalidDataException("Native movie mask BYTE4907 has no executed loader history; load a snapshot containing its native allocation state.");
            }
            return;
        }
        if (state.Version != 1)
        {
            throw new InvalidDataException("Unknown native movie mask owner version.");
        }
        if (state.Playback is not NativeMovieMaskPlayback playback)
        {
            if (scene.MovieActors is not null)
            {
                throw new InvalidDataException("Native movie lifecycle has no corresponding CAM binding.");
            }
            return;
        }
        if (source is null || source.MovieIndex != playback.MovieIndex || !services.CanStoreMovieSource)
        {
            throw new InvalidDataException("Saved movie CAM has no corresponding prepared disc/resource binding.");
        }
        source.Validate();
        NativeMovieCameraFlags flags = RequireMovieCameraFlags(source.DiscIndex, playback.MovieIndex);
        if (playback.MovieIndex != movie || flags.SourceSha256 != playback.CameraSha256 || playback.AppliedRecord < -1 || playback.AppliedRecord >= flags.Flags.Length || playback.DrawRecord != (playback.AppliedRecord >= 1 ? playback.AppliedRecord : null) || playback.DrawValue.HasValue != playback.DrawRecord.HasValue || scene.MovieActors is not NativeMovieActorLifecycle lifecycle || lifecycle.ReadyFrame > frame || lifecycle.StartFrame is null && playback.AppliedRecord != -1 || lifecycle.StartFrame is not null && playback.AppliedRecord < 1)
        {
            throw new InvalidDataException("Saved native movie mask phases disagree with their actual CAM/lifecycle binding.");
        }
        if (lifecycle.Active && playback.DrawValue is byte draw && state.Value != flags.Apply(playback.AppliedRecord, draw))
        {
            throw new InvalidDataException("Saved native movie control byte differs from its late-CAM or ordinary cleanup writer.");
        }
        if (lifecycle.Active && (lifecycle.StartFrame == frame ? playback.AppliedRecord != 1 : decoded is null || decoded.FrameIndex != playback.AppliedRecord))
        {
            throw new InvalidDataException("Saved native movie mask record differs from its initial or decoded callback owner.");
        }
        if (lifecycle.Active)
        {
            if (services is not GameSession session || !session.IsMoviePlaying || session.State.MovieIndex != playback.MovieIndex)
            {
                throw new InvalidDataException("Saved native movie CAM binding differs from the current physical movie owner.");
            }
            MoviePlaybackState? current = session.CurrentMoviePlayback;
            if (lifecycle.StartFrame == frame)
            {
                //The native start callback presents records0/1 before the first subsequent VM callback.
                if (current is not null && (current.FrameIndex is < 0 or > 1 || !current.IsPlaying || current.Completed || current.Skipped))
                {
                    throw new InvalidDataException("Saved native movie startup differs from its initial decoded frame phase.");
                }
            }
            else if (current is null || current != decoded)
            {
                throw new InvalidDataException("Saved native movie CAM record differs from the root decoded playback clock.");
            }
        }
    }

    public bool RequireSupportedMovieMaskRender(byte nativeInf14, int decodedFrame)
    {
        if (!services.IsMoviePlaying || nativeInf14 != 1)
        {
            return false;
        }
        NativeMovieMaskPlayback playback = Scene.NativeMovieMask?.Playback ?? throw new NotSupportedException("Native movie rendering requires its current mask-control phase.");
        if (playback.DrawRecord != Math.Max(1, decodedFrame) || playback.DrawValue is not byte draw)
        {
            throw new NotSupportedException("Native movie rendering has no matching pre-CAM draw-control receipt.");
        }
        //The renderer consumes the retained pre-CAM draw phase; the live byte already belongs to the following callback.
        return draw == 0;
    }
}
