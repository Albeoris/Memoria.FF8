namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void ValidatePendingNativePushContacts()
    {
        if (!IsUserControlEnabled)
        {
            return;
        }
        foreach (ActorState actor in actors.Where(actor => actor.NativePushContactPending))
        {
            ValidateNativePushDispatch(actor);
        }
    }

    private void ValidateNativePushDispatch(ActorState actor)
    {
        if (program is null || Scene.NativeActorLayoutVersion != 1 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || program.Objects[actor.ObjectId].Scripts.Count < 4)
        {
            throw new NotSupportedException("Native push contact requires a retained actor and its original local script3; original state preserved.");
        }
        Fiber[] active = fibers.Where(fiber => fiber.ObjectId == actor.ObjectId && !fiber.Complete).ToArray();
        if (active.Any(fiber => fiber.NativeActorSlot == 6 || fiber.NativeActorSlot is null && fiber.Priority == 6))
        {
            return;
        }
        EventScript push = program.Objects[actor.ObjectId].Scripts[3];
        bool emptyPush = push.End == push.Start + 2 && program.Operations[push.Start + 1].Opcode == Albeoris.Games.FF8.Jsm.Jsm.Opcode.RET && program.Operations[push.Start + 1].Parameter == 8;
        if (actor.MovementActorId is not null || actor.MovementTarget is not null && actor.NativeScriptMovement is not { Active: true } || actor.NativeScriptMovement is { Active: true, Completion: < 2 } && !emptyPush)
        {
            throw new NotSupportedException("Native push contact requires competing-priority movement/operand suspension evidence; original state preserved.");
        }
    }

    private void DispatchNativePushContact(ActorState actor)
    {
        if (!actor.NativePushContactPending)
        {
            return;
        }
        if (IsUserControlEnabled)
        {
            ValidateNativePushDispatch(actor);
            if (!fibers.Any(fiber => fiber.ObjectId == actor.ObjectId && (fiber.NativeActorSlot == 6 || fiber.NativeActorSlot is null && fiber.Priority == 6) && !fiber.Complete))
            {
                Fiber requested = Queue(actor.ObjectId, program!.Objects[actor.ObjectId].Scripts[3].Id, 6, false);
                requested.NativeActorSlot = 6;
                if (actor.NativeScriptMovement is { Active: true, Completion: < 2 } movement)
                {
                    // 52B0D0 preserves436..444/538/510 and writes mode572=0.
                    // An empty local3 returns in this same atomic VM callback.
                    actor.NativeScriptMovement = movement with { Suspended = true };
                }
            }
        }
        actor.NativePushContactPending = false;
    }

    private bool RunNativeActorContactScheduler(int objectId)
    {
        if (!fibers.Any(fiber => fiber.ObjectId == objectId && fiber.NativeActorSlot is not null))
        {
            return false;
        }
        int remaining = 16;
        while (remaining > 0)
        {
            Fiber? fiber = fibers.Where(item => item.ObjectId == objectId && !item.Complete).OrderByDescending(item => item.NativeActorSlot ?? (item.Priority == 7 ? 0 : item.Priority)).ThenBy(item => item.Sequence).FirstOrDefault();
            if (fiber is null)
            {
                break;
            }
            remaining -= Run(fiber, remaining);
            if (fiber.Complete && fiber.NativeActorSlot == 6 && actors[objectId].NativeScriptMovement is { Suspended: true } movement)
            {
                // 51C5C0 RET restores mode1, completion0 and the saved target/speed;
                // automatic locomotion resets turn474 and selects walk/run if needed.
                ActorState actor = actors[objectId];
                bool automatic = movement.ResetAnimationOnCompletion ?? movement.AutomaticAnimation;
                actor.NativeScriptMovement = movement with { Suspended = false, Completion = 0, TurnAccumulator = automatic ? (short)0 : movement.TurnAccumulator };
                if (automatic)
                {
                    int animation = movement.Speed < 2696 ? actor.WalkAnimationId : actor.RunAnimationId;
                    if (actor.AnimationId != animation)
                    {
                        SelectFullNativeAnimation(actor, animation, true);
                    }
                }
            }
            if (!fiber.Complete || transitionRequested)
            {
                break;
            }
        }
        return true;
    }

    private void ValidateNativeActorContactCheckpoint(Checkpoint saved)
    {
        foreach (FiberCheckpoint fiber in saved.Fibers.Where(fiber => fiber.NativeActorSlot is not null))
        {
            bool movementRequest = saved.Actors[fiber.ObjectId].NativeMovementRequest is { } request && request.RequestSequence == fiber.Sequence && request.RequestSlot == fiber.NativeActorSlot && request.RequestScriptId == fiber.ScriptId;
            bool haltedRequest = fiber.NativeActorSlot == fiber.Priority && !fiber.Initialization && program!.NativeObjects.Contains(fiber.ObjectId) && !program.Objects[fiber.ObjectId].IsDoor && (saved.Scene.NativeActorObjectIds.Contains(fiber.ObjectId) || program.Objects[fiber.ObjectId].BackgroundModuleIndex is not null || program.Objects[fiber.ObjectId].IsArea) && program.Objects[fiber.ObjectId].Scripts.Any(script => script.Id == fiber.ScriptId);
            if ((movementRequest || haltedRequest) && (fiber.NativeActorSlot > 7 || fiber.Priority != fiber.NativeActorSlot || fiber.Initialization))
            {
                throw new InvalidDataException("Saved native actor request lost its original slot or retained body.");
            }
            if (!movementRequest && !haltedRequest && (fiber.NativeActorSlot != 6 || fiber.Priority != 6 || fiber.Initialization || !saved.Scene.NativeActorObjectIds.Contains(fiber.ObjectId) || program!.Objects[fiber.ObjectId].Scripts.Count < 4 || program.Objects[fiber.ObjectId].Scripts[3].Id != fiber.ScriptId))
            {
                throw new InvalidDataException("Saved native actor contact lost its original local3/slot6 owner.");
            }
        }
        foreach (ActorState actor in saved.Actors.Where(actor => actor.NativeScriptMovement is { Suspended: true }))
        {
            if (actor.NativeMovementRequest is not null)
            {
                continue;
            }
            EventScript push = program!.Objects[actor.ObjectId].Scripts[3];
            if (actor.NativeScriptMovement is not { Active: true } || push.End != push.Start + 2 || program.Operations[push.Start + 1].Opcode != Albeoris.Games.FF8.Jsm.Jsm.Opcode.RET || program.Operations[push.Start + 1].Parameter != 8 || !saved.Fibers.Any(fiber => fiber.ObjectId == actor.ObjectId && fiber.NativeActorSlot == 6 && !fiber.Complete))
            {
                throw new InvalidDataException("Saved native contact suspension lacks its original empty local3 request.");
            }
        }
    }
}
