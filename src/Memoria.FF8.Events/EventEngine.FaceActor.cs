using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private Vector3 PrepareNativeActorFacePoint(Fiber fiber)
    {
        int objectId = fiber.Stack.Capture().Skip(1).First().RequireExact();
        if (program is null || (uint)objectId >= program.NativeObjects.Count)
        {
            throw new NotSupportedException("Native actor-facing targets an unknown full DWORD native object ID.");
        }
        ActorState target = actors[program.NativeObjects[objectId]];
        if (!Scene.NativeActorObjectIds.Contains(target.ObjectId))
        {
            throw new NotSupportedException("Native actor-facing target lacks its retained physical body.");
        }
        if (target.RenderModelId < 0)
        {
            throw new NotSupportedException("Native actor-facing target has no bound original model resource.");
        }
        FieldFixedPosition raw = ReadNativeScriptPosition(target) ?? throw new NotSupportedException("Native actor-facing requires the target's actual raw body position.");
        float height = ReadFaceAnchorHeight?.Invoke(target) ?? throw new NotSupportedException("Native actor-facing requires the target's declared query30 joint height.");
        if (!float.IsFinite(height) || height < int.MinValue || (double)height > int.MaxValue)
        {
            throw new NotSupportedException("Native actor-facing target has no bounded original bound-resource joint height.");
        }
        //527C30/528030 use signed division toward zero, then narrow each target WORD.
        return new Vector3(unchecked((short)(raw.X / 4096)), unchecked((short)(raw.Y / 4096)), unchecked((short)(raw.Z / 4096 + (int)height)));
    }

    private string? UnsupportedNativeActorFace(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (fiber.Stack.Count < 2 || NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || actor.FaceStateVersion != 1 || actor.RenderModelId < 0 || ResolveNativeActorFacing is null || actor.FaceElapsed is < short.MinValue or > short.MaxValue || actor.FaceSecondAngle is < short.MinValue or > short.MaxValue || actor.FacePendingBaseline is < short.MinValue or > short.MaxValue)
        {
            return "Native actor-facing requires its allocated face words, two operands and actual native joint/facing providers";
        }
        try
        {
            NativeScalarValue[] operands = fiber.Stack.Capture().Take(2).ToArray();
            if (!operands[0].ToWidth(2).IsExact || !operands[1].IsExact)
            {
                return "Native actor-facing requires an exact duration WORD and full DWORD target; no representative is selected";
            }
            _ = PrepareNativeFaceTargets(actor, PrepareNativeActorFacePoint(fiber));
            return null;
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
    }

    private int[] ExecuteNativeActorFace(Fiber fiber, bool asynchronous, out NativeScalarValue[]? scalarArguments)
    {
        Vector3 point = PrepareNativeActorFacePoint(fiber);
        NativeScalarValue duration = fiber.Stack.PeekValue();
        int time = duration.IsExact ? duration.RequireExact() : duration.ToWidth(2).RequireExact();
        int objectId = fiber.Stack.Capture().Skip(1).First().RequireExact();
        scalarArguments = duration.IsExact ? null : [NativeScalarValue.Exact(objectId), duration];
        string[] expressions = fiber.StackExpressions.Take(2).Reverse().ToArray();
        PopNativeScalar(fiber);
        PopNativeScalar(fiber);
        fiber.LastExpressions = expressions;
        ActorState actor = actors[fiber.ObjectId];
        actor.FaceTargetPoint = point;
        actor.FaceDuration = unchecked((ushort)time);
        actor.FaceProgress = 0;
        actor.FaceReturning = false;
        fiber.WaitReason = asynchronous || actor.FaceDuration == 0 ? WaitReason.None : WaitReason.FaceDirection;
        return [objectId, time];
    }
}
