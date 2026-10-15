namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedPositiveCosineTurn(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (NativeRuntimeProfile is not { IsVerified: true } || fiber.Stack.Count < 2 || !float.IsFinite(actor.Direction) || actor.Direction != (byte)actor.Direction)
        {
            return "Unknown9 requires its verified native handler, two DWORD operands and an exact current BYTE577 direction";
        }
        return null;
    }

    private int[] ExecutePositiveCosineTurn(Fiber fiber)
    {
        int[] args = Pop(fiber, 2);
        ActorState actor = actors[fiber.ObjectId];
        int start = (byte)actor.Direction;
        short target = unchecked((short)args[0]);
        if (start > target)
        {
            target = unchecked((short)(target + 256));
        }
        // 5271B0 writes WORD476/478 and BYTE578/579/580, then returns1.
        // 4789A0 samples mode2 through475CA0 at elapsed0..duration;
        // only the subsequent opcode poll observes completion580=3/return2.
        actor.NativeTurnMode580 = 2;
        actor.RotationStart = start;
        actor.RotationTarget = target;
        actor.RotationTotalFrames = unchecked((byte)args[1]);
        actor.RotationFrames = actor.RotationTotalFrames + 1;
        actor.RotationUsesNativeCosine = true;
        fiber.WaitReason = WaitReason.Rotation;
        return args;
    }
}
