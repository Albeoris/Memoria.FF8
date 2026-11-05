using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private static GameplaySaveActorJump? CaptureGameplayJump(ActorState actor)
    {
        if (actor.NativeJump is not { } jump || !jump.Active && jump.AsyncFlag20000 != true && actor.NativeMovementRequest is null)
        {
            return null;
        }
        if (jump.LandingPlaneTriangle is not (>= 0 and <= ushort.MaxValue))
        {
            throw new NotSupportedException("Gameplay native jump requires its initialized landing plane.");
        }
        GameplaySaveActorJump result = new(jump.Active, jump.FiberSequence, jump.ScriptId, jump.Operation, new(jump.StartX, jump.StartY, jump.StartZ), new(jump.TargetX, jump.TargetY, jump.TargetZ), jump.Duration, jump.Velocity, jump.AsyncFlag20000, jump.AnimationFlag40000, (ushort)jump.LandingPlaneTriangle.Value);
        result.Validate();
        return result;
    }

    private static void RestoreGameplayJump(ActorState actor, GameplaySaveFieldActor saved)
    {
        if (saved.Jump is not { } jump)
        {
            return;
        }
        jump.Validate();
        GameplaySaveActorPosition raw = saved.Pose.FixedPosition ?? throw new InvalidDataException("Gameplay native jump requires its canonical exact raw actor placement.");
        actor.NativeJump = new(1, jump.Active, jump.FiberSequence, jump.ScriptId, jump.Operation, raw.X, raw.Y, raw.Z, actor.WalkmeshTriangle, jump.Start.X, jump.Start.Y, jump.Start.Z, jump.Target.X, jump.Target.Y, jump.Target.Z, jump.Duration, jump.Velocity, jump.AsyncFlag, jump.AnimationFlag, jump.LandingPlane);
    }
}
