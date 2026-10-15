using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedDirectedLinearTurn(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode is not (Jsm.Opcode.LTURNL or Jsm.Opcode.LTURNR))
        {
            return null;
        }
        ActorState actor = actors[fiber.ObjectId];
        if (NativeRuntimeProfile is not { IsVerified: true } || !program!.NativeObjects.Take(program.NativeActorCount).Contains(actor.ObjectId) || fiber.Stack.Count < 2 || !float.IsFinite(actor.Direction) || actor.Direction != (byte)actor.Direction)
        {
            return "Native LTURNL/LTURNR requires its authenticated physical actor, exact current BYTE577 and two original DWORD operands";
        }
        return null;
    }

    private int[] ExecuteDirectedLinearTurn(Fiber fiber)
    {
        int[] operands = Pop(fiber, 2);
        ActorState actor = actors[fiber.ObjectId];
        int start = (byte)actor.Direction;
        short target = unchecked((short)operands[0]);
        if (Math.Abs(target - start) > 128)
        {
            target = unchecked((short)(start >= target ? target + 256 : target - 256));
        }
        // Native527250/527320 are identical: signed WORD target, BYTE
        // duration, linear mode1, elapsed0 through the inclusive endpoint.
        // Equal endpoints still return1 on issue and execute the full phase.
        actor.RotationStart = start;
        actor.RotationTarget = target;
        actor.RotationTotalFrames = unchecked((byte)operands[1]);
        actor.RotationFrames = actor.RotationTotalFrames + 1;
        actor.RotationUsesNativeCosine = false;
        actor.RotationUsesNativeLinear = true;
        fiber.WaitReason = WaitReason.Rotation;
        return operands;
    }
}
