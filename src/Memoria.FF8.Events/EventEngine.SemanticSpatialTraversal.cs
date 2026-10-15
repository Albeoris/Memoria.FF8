using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private PlayerActionResult ApplySemanticSpatialTraversal(ReplayNativeTraversal traversal)
    {
        if (!IsSemanticSpatialReplay)
        {
            throw new InvalidOperationException("Semantic traversal completion requires its completed-visit spatial replay scope.");
        }
        if (!CanApplySemanticTraversal(traversal))
        {
            return new PlayerActionResult(false, NextTrigger, 0, "The current native traversal producer is not ready.");
        }
        ActorState actor = actors[traversal.Producer.ObjectId];
        ReplayNativeTraversal current = CreateNativeTraversal(actor, traversal.Producer.ScriptId, traversal.Producer.Operation, traversal.Mode, traversal.Forward);
        return RunReplay(ReplayBoundary(ReplayEntryKind.NativeTraversal) with { NativeTraversal = current }, () =>
        {
            if (current.Mode == 3)
            {
                CompleteSemanticSpatialMode3(actor, actor.NativeLadderMode3!, current.Forward);
            }
            else
            {
                CompleteSemanticSpatialMode4(actor, actor.NativeLadder!, current.Forward, false);
            }
            RecordNativeTraversalCompletion(current, ButtonInput?.PhysicalHeld ?? 0);
            return new PlayerActionResult(true, NextTrigger, 0, "The current semantic traversal endpoint is complete.");
        });
    }

    private void StepSemanticSpatialTraversals()
    {
        if (!IsSemanticSpatialReplay)
        {
            throw new InvalidOperationException("Semantic traversal callbacks require their completed-visit spatial replay scope.");
        }
        foreach (ActorState actor in actors)
        {
            if (actor.NativeMovementCompletion542 == 2)
            {
                continue;
            }
            if (actor.NativeLadderMode3 is { Active: true } mode3 && !IsControlledLadderMode3(actor))
            {
                CompleteSemanticSpatialMode3(actor, mode3, true);
            }
            else if (actor.NativeLadder is { Active: true } mode4)
            {
                if (!userControlEnabled)
                {
                    CompleteSemanticSpatialMode4(actor, mode4, true, true);
                }
                else if (actor.NativeMovementCompletion542 == 0)
                {
                    //The semantic policy skips the entry animation and approach, retaining both issued endpoints for the eventual input decision.
                    short duration = NativeLadderDuration(mode4.Approach, mode4.ClimbingEnd, Scene.NativeLadderSpeed490E ?? throw new NotSupportedException("Current semantic mode4 has no issued ladder speed."));
                    actor.NativeLadder = mode4 with { Duration = duration };
                    actor.NativeTurnAccumulator474 = 0;
                    actor.NativeMovementCompletion542 = 1;
                }
            }
        }
    }

    private void CompleteSemanticSpatialMode3(ActorState actor, NativeLadderMode3State current, bool forward)
    {
        FieldFixedPosition endpoint = forward ? current.Target : current.Entry;
        int triangle = forward ? unchecked((short)(actor.NativeLandingTriangle508 ?? throw new NotSupportedException("Current semantic mode3 has no issued landing triangle."))) : current.Triangle;
        short duration = NativeLadderDuration(current.Entry, current.Target, current.InitializationSpeed, true);
        TeleportSemanticActor(actor, endpoint, triangle);
        actor.NativeTurnAccumulator474 = forward ? duration : (short)0;
        actor.NativeMovementCompletion542 = 2;
        actor.NativeLadderMode3 = current with { FixedX = endpoint.X, FixedY = endpoint.Y, FixedZ = endpoint.Z, Triangle = triangle, Duration = duration };
    }

    private void CompleteSemanticSpatialMode4(ActorState actor, NativeLadderState current, bool forward, bool automatic)
    {
        FieldFixedPosition endpoint = automatic ? current.ClimbingEnd : forward ? current.Exit : current.Entry;
        int triangle = forward && !automatic ? unchecked((short)(actor.NativeLandingTriangle508 ?? throw new NotSupportedException("Current semantic mode4 has no issued landing triangle."))) : current.Triangle;
        //These counters retain the existing terminal owner's invariant; no animation, follower or cadence callbacks execute.
        short duration = automatic
            ? NativeLadderDuration(current.Approach, current.ClimbingEnd, Scene.NativeLadderSpeed490E ?? throw new NotSupportedException("Current semantic mode4 has no issued ladder speed."))
            : ResolvePartyActor(2) is not null ? (short)32 : ResolvePartyActor(1) is not null ? (short)16 : (short)1;
        TeleportSemanticActor(actor, endpoint, triangle);
        actor.NativeTurnAccumulator474 = duration;
        actor.NativeMovementCompletion542 = 2;
        actor.NativeMovementGroundContact600 = automatic ? (byte)0 : (byte)1;
        actor.NativeLadder = current with { FixedX = endpoint.X, FixedY = endpoint.Y, FixedZ = endpoint.Z, Triangle = triangle, Duration = duration, AutomaticCompletion = automatic, ReplayCompletion = null, ReplayPhysicalHeld = null };
    }
}
