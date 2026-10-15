using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public sealed record NativeMovieActorLifecycle([property: JsonRequired] int Version, [property: JsonRequired] int MovieIndex, [property: JsonRequired] int Mode, [property: JsonRequired] long ReadyFrame, [property: JsonRequired] long? StartFrame, [property: JsonRequired] long? EndFrame)
{
    public bool Active => StartFrame is not null && EndFrame is null;

    public void Validate(long frame)
    {
        if (Version != 1 || MovieIndex is < 0 or > 999 || ReadyFrame < 0 || ReadyFrame > frame || StartFrame < ReadyFrame || StartFrame > frame || EndFrame < StartFrame || EndFrame > frame || EndFrame is not null && StartFrame is null)
        {
            throw new InvalidDataException("Native movie actor lifecycle has inconsistent preparation/start/cleanup history.");
        }
    }
}

public sealed partial class EventEngine
{
    private string? UnsupportedMovieActors(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode == Jsm.Opcode.MOVIEREADY)
        {
            return fiber.Stack.Count < 2 || fiber.Stack.Skip(1).First() is < 0 or > 999 || Scene.MovieActors?.Active == true ? "MOVIEREADY requires two operands, a supported original clip index and no active original movie" : null;
        }
        if (operation.Opcode != Jsm.Opcode.MOVIE)
        {
            return null;
        }
        if (services is not GameSession || Scene.MovieActors is not { StartFrame: null } lifecycle || lifecycle.MovieIndex != preparedMovie)
        {
            return "MOVIE requires its actual preparation and supported start/cleanup service";
        }
        try
        {
            _ = PrepareMovieActorWords(start: true);
            _ = NeedsNativeSalaryTransitionReset();
            return null;
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            return exception.Message;
        }
    }

    private (ActorState Actor, short Speed, short BaseSpeed, short AnimationSpeed)[] PrepareMovieActorWords(bool start)
    {
        if (program is null)
        {
            throw new NotSupportedException("Native movie actor writes require the allocated field program.");
        }
        IEnumerable<int> nativeObjects = Scene.NativeActorLayoutVersion == 1 ? Scene.NativeActorObjectIds : program.NativeObjects.Take(program.NativeActorCount);
        List<(ActorState, short, short, short)> result = [];
        foreach (int objectId in nativeObjects)
        {
            ActorState actor = actors[objectId];
            if (actor.NativeMovementBaseSpeed is not short baseSpeed || actor.MovementSpeed is < short.MinValue or > short.MaxValue || actor.AnimationSpeed is < short.MinValue or > short.MaxValue)
            {
                throw new NotSupportedException("Native movie WORD510/512/520 writes require their actual allocated widths and base-speed authority.");
            }
            short speed = start ? unchecked((short)(actor.MovementSpeed * 2)) : (short)(actor.MovementSpeed / 2);
            short baseline = start ? unchecked((short)(baseSpeed * 2)) : (short)(baseSpeed / 2);
            short animation = start ? unchecked((short)(actor.AnimationSpeed * 2)) : (short)(actor.AnimationSpeed / 2);
            if (actor.NativeScriptMovement is not null && speed <= 0)
            {
                throw new NotSupportedException("Native movie word wrapping creates an unsupported nonpositive active scripted speed.");
            }
            result.Add((actor, speed, baseline, animation));
        }
        return result.ToArray();
    }

    private static void CommitMovieActorWords((ActorState Actor, short Speed, short BaseSpeed, short AnimationSpeed)[] writes)
    {
        foreach (var write in writes)
        {
            write.Actor.MovementSpeed = write.Speed;
            write.Actor.NativeMovementBaseSpeed = write.BaseSpeed;
            write.Actor.AnimationSpeed = write.AnimationSpeed;
            if (write.Actor.NativeAnimationHeader is { } animation)
            {
                write.Actor.NativeAnimationHeader = animation with { Increment = unchecked((ushort)write.AnimationSpeed) };
            }
            if (write.Actor.NativeScriptMovement is NativeScriptMovementState movement)
            {
                write.Actor.NativeScriptMovement = movement with { Speed = write.Speed };
            }
        }
    }

    private void StartNativeMovieActors(Fiber fiber)
    {
        var writes = PrepareMovieActorWords(start: true);
        ResetNativeSalaryForTransition(fiber);
        services.RequestMovie(preparedMovie);
        CommitMovieActorWords(writes);
        Scene.MovieActors = Scene.MovieActors! with { StartFrame = Frame };
    }

    private void CompleteNativeMovieActors()
    {
        if (Scene.MovieActors?.Active != true)
        {
            return;
        }
        var writes = PrepareMovieActorWords(start: false);
        CommitMovieActorWords(writes);
        Scene.MovieActors = Scene.MovieActors with { EndFrame = Frame };
    }
}
