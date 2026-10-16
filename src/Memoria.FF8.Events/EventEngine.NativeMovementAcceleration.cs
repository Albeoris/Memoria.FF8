using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>Original524840 signed WORD speed update at the current field clock20.</summary>
public static class NativeMovementAcceleration
{
    public static short CalculateSpeed(short speed, short targetSpeed, int fixedX, int fixedY, int targetX, int targetY)
    {
        int dx = unchecked(targetX - fixedX) / 4096;
        int dy = unchecked(targetY - fixedY) / 4096;
        long squared = (long)dx * dx + (long)dy * dy;
        if (squared > int.MaxValue)
        {
            throw new NotSupportedException("Native MACCEL distance overflow has no proved square-root result.");
        }
        int distance = (int)Math.Sqrt(squared);
        int numerator = 20 * (targetSpeed - speed);
        return unchecked((short)(speed + (distance > 0 ? numerator / distance : numerator)));
    }
}

public sealed partial class EventEngine
{
    private NativeScriptMovementState CreateNativeAccelerationMove(Fiber fiber, FieldFixedPosition raw, byte movementDirection, short baseSpeed, int operation)
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("MACCEL requires the original native field clock and retained mode1 body.");
        }
        int[] args = fiber.Stack.Take(5).Reverse().ToArray();
        int x = unchecked(args[0] << 12);
        int y = unchecked(args[1] << 12);
        int z = unchecked(args[2] << 12);
        if (new[] { x, y, z }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue))
        {
            throw new NotSupportedException("MACCEL shifted target is outside the current native body coordinate domain.");
        }
        short speed = NativeMovementAcceleration.CalculateSpeed(baseSpeed, unchecked((short)args[4]), raw.X, raw.Y, x, y);
        ActorState actor = actors[fiber.ObjectId];
        return new NativeScriptMovementState(1, true, fiber.Sequence, fiber.Script.Id, operation, raw.X, raw.Y, raw.Z, actor.WalkmeshTriangle, x, y, z, speed, unchecked((short)args[3]), movementDirection, (byte)actor.Direction, HoldsFacing: true, Accelerated: true);
    }

    private NativeScriptMovementState PrepareNativeAccelerationStep(ActorState actor, NativeScriptMovementState state)
    {
        short target = actor.NativeMovementAccelerationTarget ?? throw new NotSupportedException("MACCEL retry has no executed shared actor target WORD516.");
        short speed = NativeMovementAcceleration.CalculateSpeed(state.Speed, target, state.FixedX, state.FixedY, state.TargetX, state.TargetY);
        return state with { Speed = speed };
    }
}
