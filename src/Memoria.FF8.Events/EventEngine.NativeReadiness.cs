namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>The same admission predicate used by an ordinary Move; it does not promise displacement.</summary>
    public bool CanAcceptMovementInput => CanAcceptPlayerMovement;

    private bool CanAcceptFieldMovement => !services.IsMoviePlaying && CanAcceptPlayerMovement;

    private bool CanAcceptPlayerMovement => Scene.NativeTutorial is null && IsUserControlEnabled && !IsMovementBlockedByDialogue && PlayerActor is { Active: true, Visible: true };

    private Fiber? SelectScheduledFiber(int objectId)
    {
        IEnumerable<Fiber> pending = fibers.Where(item => item.ObjectId == objectId && !item.Complete);
        bool doorScheduler = Scene.NativeDoorDriverVersion == 2 && program!.Objects[objectId].IsDoor;
        if (doorScheduler)
        {
            return SelectNativeDoorScheduledFiber(objectId);
        }
        if (IsNativeActorRequest(objectId))
        {
            // Native51CD60 selects the highest occupied actor slot; slot0 retains the default.
            return pending.OrderByDescending(NativeActorRequestSlot).ThenBy(item => item.Sequence).FirstOrDefault();
        }
        return pending.OrderBy(item => item.Priority).ThenBy(item => item.Sequence).FirstOrDefault();
    }

    private bool AreSelectedFieldScriptsStarted()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            return !fibers.Any(item => !item.Complete && !item.Started);
        }
        // Native529FF0 runs only the selected owner for each callback entity.
        // A pending lower default is not a startup barrier beneath an executed request.
        return NativeCallbackActors().All(actor => SelectScheduledFiber(actor.ObjectId) is not { Started: false });
    }
}
