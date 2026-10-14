namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private static bool NativeMovementOwnsFacing(ActorState actor) => actor.NativeMovementCompletion542 == 1 && actor.NativeScriptMovement is { Active: true, Suspended: false, HoldsFacing: false } movement && actor.NativeMotionAuthority is { Version: 1, FacingHold: 0 } authority && actor.Direction == movement.FacingDirection && movement.FacingDirection == movement.MovementDirection && authority.MovementDirection == movement.MovementDirection && actor.X == movement.FixedX / 4096f && actor.Y == movement.FixedY / 4096f && actor.Z == movement.FixedZ / 4096f && actor.WalkmeshTriangle == movement.Triangle;

    private void StepCosineRotation(ActorState actor)
    {
        if ((!actor.RotationUsesNativeCosine && !actor.RotationUsesNativeLinear) || actor.RotationTarget is not float target)
        {
            return;
        }
        // Native PCTURN samples elapsed0 before incrementing, and includes the duration endpoint.
        int elapsed = actor.RotationTotalFrames - actor.RotationFrames + 1;
        actor.Direction = unchecked((byte)(actor.RotationUsesNativeLinear ? NativeJumpMath.Interpolate((int)actor.RotationStart, (int)target, (short)actor.RotationTotalFrames, (short)elapsed) : FaceCosineInterpolate((int)actor.RotationStart, (int)target, actor.RotationTotalFrames, elapsed)));
        actor.RotationFrames--;
        if (actor.RotationFrames == 0)
        {
            actor.RotationTarget = null;
            actor.RotationUsesNativeCosine = false;
            actor.RotationUsesNativeLinear = false;
        }
    }
}
