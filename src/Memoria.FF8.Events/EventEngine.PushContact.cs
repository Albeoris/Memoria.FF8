using Memoria.FF8.Runtime;

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
        if (actor.MovementActorId is not null || actor.MovementTarget is not null && actor.NativeScriptMovement is not { Active: true } || actor.NativeJump is { Active: true })
        {
            throw new NotSupportedException("Native push contact requires competing-priority movement/operand suspension evidence; original state preserved.");
        }
        string? suspension = UnsupportedNativeMovementRequest(actor, 6, push.Id);
        if (suspension is not null)
        {
            throw new NotSupportedException($"{suspension}; field={FieldId}, actor={actor.ObjectId}, frame={Frame}, physical={actor.NativeScriptMovement?.ScriptId}/{actor.NativeScriptMovement?.Operation}, backup={actor.NativeMovementRequest?.SavedMovement.ScriptId}/{actor.NativeMovementRequest?.SavedMovement.Operation}, requestSlot={actor.NativeMovementRequest?.RequestSlot}");
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
            AdmitNativePushContact(actor);
        }
        actor.NativePushContactPending = false;
    }

    private bool AdmitNativePushContact(ActorState actor)
    {
        ValidateNativePushDispatch(actor);
        if (fibers.Any(fiber => fiber.ObjectId == actor.ObjectId && (fiber.NativeActorSlot == 6 || fiber.NativeActorSlot is null && fiber.Priority == 6) && !fiber.Complete))
        {
            return false;
        }
        Fiber requested = Queue(actor.ObjectId, program!.Objects[actor.ObjectId].Scripts[3].Id, 6, false);
        requested.NativeActorSlot = 6;
        SelectNativeActorRequest(requested);
        SuspendNativeMovementForRequest(actor, requested);
        return true;
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
            Fiber? fiber = SelectScheduledFiber(objectId);
            if (fiber is null)
            {
                break;
            }
            remaining -= Run(fiber, remaining);
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
        if (saved.Actors.Any(actor => actor.NativeScriptMovement is { Suspended: true } && actor.NativeMovementRequest is null))
        {
            throw new InvalidDataException("Saved suspended native movement lacks its executed single physical backup.");
        }
    }
}
