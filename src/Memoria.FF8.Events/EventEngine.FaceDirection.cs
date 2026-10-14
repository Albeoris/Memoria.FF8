using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private FaceDirectionBaseline PrepareNativeFaceReturn(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        FaceDirectionBaseline baseline = ReadFaceBaseline?.Invoke(actor) ?? throw new NotSupportedException("Native face return requires the declared first-frame query32 control-node rotation.");
        if (baseline.FirstAngle is < 0 or > 255 || baseline.SecondAngle is < 0 or > 255 || baseline.XAngle is < 0 or > 255)
        {
            throw new NotSupportedException("Native query32 face return targets exceed the unpacked byte-angle range.");
        }
        return baseline;
    }

    private string? UnsupportedNativeFaceReturn(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (fiber.Stack.Count == 0 || NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || actor.FaceStateVersion != 1 || actor.ModelId < 0)
        {
            return "Native face return requires its consumed duration WORD, allocated face owner and current declared model";
        }
        try
        {
            if (!fiber.Stack.PeekValue().ToWidth(2).IsExact)
            {
                return "Native face return duration WORD remains uncertain; no representative is selected";
            }
            _ = PrepareNativeFaceReturn(fiber);
            return null;
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
    }

    private int[] ExecuteNativeFaceReturn(Fiber fiber, bool asynchronous, out NativeScalarValue[]? scalarArguments)
    {
        FaceDirectionBaseline baseline = PrepareNativeFaceReturn(fiber);
        NativeScalarValue original = fiber.Stack.PeekValue();
        int argument = original.IsExact ? original.RequireExact() : original.ToWidth(2).RequireExact();
        scalarArguments = original.IsExact ? null : [original];
        PopNativeScalar(fiber);
        ActorState actor = actors[fiber.ObjectId];
        // Native527E40/528240 share query32 target stores;528240 always returns2.
        actor.FaceDuration = unchecked((ushort)argument);
        actor.FaceProgress = 0;
        actor.FacePitchTarget = baseline.XAngle;
        actor.FacePendingBaseline = baseline.FirstAngle;
        actor.FaceYawTarget = baseline.SecondAngle;
        actor.FaceTargetPoint = null;
        actor.FaceReturning = true;
        fiber.WaitReason = asynchronous || actor.FaceDuration == 0 ? WaitReason.None : WaitReason.FaceDirection;
        return [argument];
    }

    private string? UnsupportedNativeFaceSync(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        return NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || actor.FaceStateVersion != 1 || actor.FaceDuration is < 0 or > ushort.MaxValue || actor.FaceProgress is < 0 or > ushort.MaxValue ? "FACEDIRSYNC requires its actual allocated face WORD564/566 owner; no completion is inferred" : null;
    }

    private void ExecuteNativeFaceSync(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (actor.FaceProgress != actor.FaceDuration)
        {
            // Native527AD0 returns1 with unchanged operands and retries this PC.
            fiber.Position--;
            fiber.WaitReason = WaitReason.NativeFaceSync;
        }
    }

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
            (int pitch, int yaw) = PrepareNativeFaceTargets(actor, target);
            actor.FacePitchTarget = pitch;
            actor.FaceYawTarget = yaw;
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
