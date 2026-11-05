using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private short NativeLadderTransitionDuration(ActorState actor, NativeLadderState ladder, int animation)
    {
        if (ladder.GameplayTiming is { } timing)
        {
            if (animation == timing.StartSelector)
            {
                return timing.StartDuration;
            }
            if (animation == timing.EndSelector)
            {
                return timing.EndDuration;
            }
        }
        return unchecked((short)(AnimationFrameCount!(actor.RenderModelId, animation) - 2));
    }

    private bool IsGameplayLadderPartyBody(ActorState actor)
    {
        return actors.Any(body => body.NativeLadder is { Active: true }) && Enumerable.Range(0, 3).Any(slot => NativeFieldPartyBinding.ResolveActor(Scene, actors, slot) == actor);
    }

    private GameplaySaveActorLadder? CaptureGameplayLadder(ActorState actor)
    {
        if (actor.NativeLadder is not { Active: true } ladder)
        {
            return null;
        }
        NativeLadderAnimationState animation = actor.NativeLadderAnimation ?? throw new InvalidDataException("Gameplay mode4 ladder lost its animation selectors.");
        GameplaySaveActorLadder result = new(ladder.FiberSequence, ladder.ScriptId, ladder.Operation, ladder.Up, new(ladder.Entry.X, ladder.Entry.Y, ladder.Entry.Z), new(ladder.Approach.X, ladder.Approach.Y, ladder.Approach.Z), new(ladder.ClimbingEnd.X, ladder.ClimbingEnd.Y, ladder.ClimbingEnd.Z), new(ladder.Exit.X, ladder.Exit.Y, ladder.Exit.Z), ladder.Duration, ladder.AutomaticCompletion, animation.Start594, NativeLadderTransitionDuration(actor, ladder, animation.Start594), animation.End596, NativeLadderTransitionDuration(actor, ladder, animation.End596));
        result.Validate();
        return result;
    }

    private static void RestoreGameplayLadder(ActorState actor, GameplaySaveFieldActor saved)
    {
        if (saved.Ladder is not { } ladder)
        {
            return;
        }
        ladder.Validate();
        if (actor.NativeLadderAnimation is not { } animation || animation.Start594 != ladder.StartSelector || animation.End596 != ladder.EndSelector)
        {
            throw new InvalidDataException("Gameplay mode4 logical timing differs from its current consumed animation selectors.");
        }
        GameplaySaveActorPosition raw = saved.Pose.FixedPosition ?? throw new InvalidDataException("Gameplay mode4 requires its canonical exact raw actor pose.");
        actor.NativeLadder = new(1, true, ladder.FiberSequence, ladder.ScriptId, ladder.Operation, ladder.Up, raw.X, raw.Y, raw.Z, actor.WalkmeshTriangle, new(ladder.Entry.X, ladder.Entry.Y, ladder.Entry.Z), new(ladder.Approach.X, ladder.Approach.Y, ladder.Approach.Z), new(ladder.ClimbingEnd.X, ladder.ClimbingEnd.Y, ladder.ClimbingEnd.Z), new(ladder.Exit.X, ladder.Exit.Y, ladder.Exit.Z), ladder.Duration, ladder.AutomaticCompletion)
        {
            GameplayTiming = new(ladder.StartSelector, ladder.StartDuration, ladder.EndSelector, ladder.EndDuration)
        };
    }

    private void ValidateGameplayLadderTiming(ActorState actor, SceneEventState scene, NativeLadderState ladder)
    {
        if (ladder.GameplayTiming is not { } timing)
        {
            return;
        }
        if (scene.NativeFieldInitialization is not { Origin: NativeFieldInitializationOrigin.GameplayContinuation } || actor.GameplayReconstruction is not { Version: 1 } origin || origin.FieldId != FieldId || origin.ProgramHash != programHash || origin.ObjectId != actor.ObjectId || timing.StartDuration is < -1 or > 4093 || timing.EndDuration is < -1 or > 4093 || timing.StartSelector == timing.EndSelector && timing.StartDuration != timing.EndDuration)
        {
            throw new InvalidDataException("Gameplay mode4 logical timing lacks its explicit reconstruction origin or valid transition domain.");
        }
    }

    private void ValidateGameplayLadders(IReadOnlyList<Fiber> current, bool controlEnabled)
    {
        ValidateNativeLadders(actors, Scene, current, controlEnabled, services.FieldButtonInput);
    }
}
