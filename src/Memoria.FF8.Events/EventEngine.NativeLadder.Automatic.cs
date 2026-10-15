namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void StepAutomaticNativeLadder(ActorState actor, NativeLadderState current, short elapsed)
    {
        //47BE80's raw UCOFF branch completes directly, retaining triangle506 and the climb duration.
        if (elapsed == current.Duration)
        {
            actor.NativeMovementCompletion542 = 2;
            actor.NativeLadder = current with { AutomaticCompletion = true };
            return;
        }
        short nextElapsed = unchecked((short)(elapsed + 1));
        actor.NativeTurnAccumulator474 = nextElapsed;
        //This writes only the current model WORD82. It neither selects595 nor changes body WORD518.
        ushort frame = unchecked((ushort)((int)(actor.AnimationFrame * 16) + unchecked((ushort)actor.AnimationSpeed)));
        int total = checked(AnimationFrameCount!(actor.RenderModelId, actor.AnimationId) * 16);
        actor.AnimationFrame = (frame > total - 1 ? 0 : frame) / 16f;
        current = current with { FixedX = NativeJumpMath.Interpolate(current.Approach.X, current.ClimbingEnd.X, current.Duration, nextElapsed), FixedY = NativeJumpMath.Interpolate(current.Approach.Y, current.ClimbingEnd.Y, current.Duration, nextElapsed), FixedZ = NativeJumpMath.Interpolate(current.Approach.Z, current.ClimbingEnd.Z, current.Duration, nextElapsed) };
        actor.NativeLadder = current;
        actor.X = current.FixedX / 4096f;
        actor.Y = current.FixedY / 4096f;
        actor.Z = current.FixedZ / 4096f;
        Scene.NativePlayerPosition = new FieldFixedPosition(current.FixedX, current.FixedY, current.FixedZ);
        //No trace record/follower pass, exit animation, triangle copy or stage6 drain occurs here.
    }
}
