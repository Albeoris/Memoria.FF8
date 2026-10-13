using System.Numerics;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void InitializeFaceDirection(ActorState actor, Fiber fiber)
    {
        FaceDirectionBaseline baseline = ReadFaceBaseline?.Invoke(actor) ?? throw Fault(fiber, "FACEDIRINIT requires the model's declared first control-node rotation.");
        if (baseline.FirstAngle is < 0 or > 255 || baseline.SecondAngle is < 0 or > 255)
        {
            throw Fault(fiber, "Model face baseline is outside native byte-angle bounds.");
        }
        actor.FaceElapsed = 0; // Native +0x228 is the starting pitch, despite the legacy name.
        actor.FaceFirstAngle = baseline.FirstAngle;
        actor.FaceSecondAngle = baseline.SecondAngle;
        actor.FaceStateVersion = 1;
    }

    private void StepFaceDirection(ActorState actor)
    {
        if (actor.FaceTargetPoint is null && !actor.FaceReturning)
        {
            return;
        }
        if (actor.FaceProgress == 0 && actor.FaceTargetPoint is Vector3 target)
        {
            float height = ReadFaceAnchorHeight?.Invoke(actor) ?? throw new InvalidDataException("Active face transition requires its model control-node height provider.");
            if (!float.IsFinite(height))
            {
                throw new InvalidDataException("Model face anchor height is not finite.");
            }
            float x = MathF.Floor(actor.X) + actor.DisplayOffset.X;
            float y = MathF.Floor(actor.Y) + actor.DisplayOffset.Y;
            float z = MathF.Floor(actor.Z) + actor.DisplayOffset.Z + (int)height;
            int yaw = (sbyte)(byte)(NativeDirection(x, y, target.X, target.Y) - (byte)actor.Direction);
            actor.FaceYawTarget = Math.Clamp(yaw, -(int)actor.FaceLimits.Z, (int)actor.FaceLimits.Z);
            long dx = (long)target.X - (long)x;
            long dy = (long)target.Y - (long)y;
            int distance = (int)Math.Sqrt(dx * dx + dy * dy);
            int pitch = (sbyte)(byte)NativeDirection(z, distance, target.Z, 0);
            actor.FacePitchTarget = Math.Clamp(pitch, -(int)actor.FaceLimits.X, (int)actor.FaceLimits.X);
        }
        if (actor.FaceProgress >= actor.FaceDuration)
        {
            if (actor.FaceReturning)
            {
                actor.FirstControlRotation = null;
                actor.FaceReturning = false;
            }
            return;
        }
        actor.FacePitchTarget = NearestFaceAngle(actor.FaceElapsed, actor.FacePitchTarget);
        actor.FaceYawTarget = NearestFaceAngle(actor.FaceSecondAngle, actor.FaceYawTarget);
        actor.FaceProgress++;
        byte pitchAngle = (byte)FaceCosineInterpolate(actor.FaceElapsed, actor.FacePitchTarget, actor.FaceDuration, actor.FaceProgress);
        byte yawAngle = (byte)FaceCosineInterpolate(actor.FaceSecondAngle, actor.FaceYawTarget, actor.FaceDuration, actor.FaceProgress);
        actor.FirstControlRotation = new Vector3(0, pitchAngle * MathF.Tau / 256, -yawAngle * MathF.Tau / 256);
        if (actor.FaceProgress == actor.FaceDuration)
        {
            actor.FaceElapsed = actor.FacePitchTarget;
            actor.FaceSecondAngle = actor.FaceYawTarget;
            actor.FaceFirstAngle = actor.FacePendingBaseline;
        }
    }

    private static int NearestFaceAngle(int start, int target)
    {
        if (target - start > 128)
        {
            return target - 256;
        }
        if (start - target > 128)
        {
            return target + 256;
        }
        return target;
    }

    private static int FaceCosineInterpolate(int start, int target, int duration, int elapsed)
    {
        return NativeCosineInterpolation.Interpolate(start, target, duration, elapsed);
    }
}
