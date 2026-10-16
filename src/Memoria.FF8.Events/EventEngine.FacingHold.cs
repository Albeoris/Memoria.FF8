using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeFacingHold(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (!IsAllocatedPhysicalRequestActor(actor.ObjectId) || actor.NativeMotionAuthority is not { Version: 1 } || fiber.Stack.Count < 1)
        {
            return "Native opcode361 requires its retained physical body, known BYTE575/576 owner and one DWORD operand";
        }
        return null;
    }

    private int[] ExecuteNativeFacingHold(Fiber fiber)
    {
        int[] operands = Pop(fiber, 1);
        ActorState actor = actors[fiber.ObjectId];
        byte held = unchecked((byte)operands[0]);
        // Original526210 writes only the caller's BYTE576 and returns2.
        actor.NativeMotionAuthority = actor.NativeMotionAuthority! with { FacingHold = held };
        if (actor.NativeScriptMovement is { } movement && (movement.Active || movement.Cancelled))
        {
            actor.NativeScriptMovement = movement with { FacingHold576 = held };
        }
        return operands;
    }
}
