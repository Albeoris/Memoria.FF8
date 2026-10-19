namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeMovementCancellation(Fiber fiber)
    {
        if (program is null || fiber.Stack.Count == 0 || (uint)(int)fiber.Stack.Peek() >= program.NativeObjects.Count)
        {
            return "MOVECANCEL requires its original native target operand";
        }
        ActorState target = actors[program.NativeObjects[fiber.Stack.Peek()]];
        return UnsupportedNativeMovementCancellationTarget(target);
    }

    private string? UnsupportedNativeMovementCancellationTarget(ActorState target)
    {
        if (target.NativeScriptMovement is not { Active: true, Suspended: false } movement)
        {
            return null;
        }
        if (target.RenderModelId < 0 || ReadNativeScriptPosition(target) is null || target.NativeMotionAuthority is not { Version: 1 } || !movement.Asynchronous && !fibers.Any(owner => owner.Sequence == movement.FiberSequence && owner.ObjectId == target.ObjectId && !owner.Complete && owner.NativeMoveOperation == movement.Operation && owner.WaitReason == WaitReason.NativeScriptMovement) || AnimationFrameCount?.Invoke(target.RenderModelId, target.BaseAnimationId) is not > 0)
        {
            return "MOVECANCEL requires its actual mode1 body, issued synchronous consumer and full idle animation";
        }
        return null;
    }

    private bool CancelNativeScriptMovement(ActorState actor)
    {
        if (actor.NativeScriptMovement is not { } movement)
        {
            return false;
        }
        if (!movement.Active || movement.Suspended)
        {
            return true;
        }
        // Native524620 writes572=0 and selects full idle. It does not write542,
        // 373,474,575/576,20000/40000 or the single movement backup10000.
        bool animationFlag = movement.AnimationFlag40000 ?? movement.ResetAnimationOnCompletion ?? movement.AutomaticAnimation;
        actor.NativeScriptMovement = movement with { Active = false, Cancelled = true, AnimationFlag40000 = animationFlag };
        actor.MovementTarget = null;
        SelectFullNativeAnimation(actor, actor.BaseAnimationId, true);
        return true;
    }
}
