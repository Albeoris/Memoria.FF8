namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void ExecuteFollowOn(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        int slot = Scene.Party.IndexOf(unchecked((byte)actor.CharacterId));
        if (slot < 0)
        {
            return;
        }
        int nativeActor = Scene.NativeActorObjectIds.IndexOf(actor.ObjectId);
        if (NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || Scene.FieldPartyNativeActorIds.Count != 3 || nativeActor < 0 || nativeActor >= byte.MaxValue)
        {
            throw new NotSupportedException("FOLLOWON requires its original retained actor and three runtime follow slots.");
        }
        // Native51ECF0 matches the caller's character597 against saved party
        // membership, clears its detached-follow flag40 and restores only
        // runtime1CE4772[slot] from the compact actor BYTE598. B194..196,
        // trace positions, animation, movement and control bytes survive.
        Scene.FieldPartyNativeActorIds[slot] = nativeActor;
    }
}
