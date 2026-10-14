namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNegativeCosineTurn(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (NativeRuntimeProfile is not { IsVerified: true } || fiber.Stack.Count < 2 || !float.IsFinite(actor.Direction) || actor.Direction != (byte)actor.Direction)
        {
            return "Unknown8 requires its verified native handler, two DWORD operands and an exact current BYTE577 direction";
        }
        return null;
    }

    private int[] ExecuteNegativeCosineTurn(Fiber fiber)
    {
        int[] args = Pop(fiber, 2);
        ActorState actor = actors[fiber.ObjectId];
        int start = (byte)actor.Direction;
        short target = unchecked((short)args[0]);
        if (start < target)
        {
            target = unchecked((short)(target - 256));
        }
        // Original527110 writes WORD476/478 and BYTE578/579/580 without
        // changing movement, animation, facing-hold576 or request ownership.
        // Mode2 in4789A0 samples475CA0 at elapsed0..duration. The next
        // opcode poll observes BYTE580=3 and returns2 without another pop.
        actor.RotationStart = start;
        actor.RotationTarget = target;
        actor.RotationTotalFrames = unchecked((byte)args[1]);
        actor.RotationFrames = actor.RotationTotalFrames + 1;
        actor.RotationUsesNativeCosine = true;
        actor.RotationUsesNativeLinear = false;
        fiber.WaitReason = WaitReason.Rotation;
        return args;
    }
}
