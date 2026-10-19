namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativePartyMovementCancellation(Fiber fiber)
    {
        if (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact || (uint)fiber.Stack.Peek() >= 3 || ReadNativePartyMoveTarget(fiber.Stack.Peek()) is not { } target)
        {
            return "PMOVECANCEL requires its original full DWORD saved-party slot and retained allocated body";
        }
        return UnsupportedNativeMovementCancellationTarget(actors[program!.NativeObjects[target.NativeObjectId]]);
    }

    private int[] ExecuteNativePartyMovementCancellation(Fiber fiber)
    {
        //5246F0 samples B194+slot directly and returns2. Its mode1 cancellation
        //and full-idle selection match524620 after the raw-object lookup.
        int[] args = Pop(fiber, 1);
        ActorState target = ResolveNativeIndexedPartyBody(args[0], out _, out _) ?? throw new NotSupportedException("PMOVECANCEL lost its retained saved-party body.");
        CancelNativeScriptMovement(target);
        return args;
    }
}
