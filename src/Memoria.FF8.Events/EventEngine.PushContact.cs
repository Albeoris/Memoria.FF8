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
        if (active.Any(fiber => fiber.Priority == 6))
        {
            return;
        }
        if (active.Length != 0 || actor.MovementTarget is not null || actor.MovementActorId is not null)
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
            if (!fibers.Any(fiber => fiber.ObjectId == actor.ObjectId && fiber.Priority == 6 && !fiber.Complete))
            {
                Queue(actor.ObjectId, program!.Objects[actor.ObjectId].Scripts[3].Id, 6, false);
            }
        }
        actor.NativePushContactPending = false;
    }
}
