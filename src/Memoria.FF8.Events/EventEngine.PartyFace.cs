using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private Vector3 PrepareNativePartyFacePoint(Fiber fiber)
    {
        NativeScalarValue[] operands = fiber.Stack.Capture().Take(2).ToArray();
        int slot = operands[1].RequireExact();
        ActorState target = ResolveNativeIndexedPartyBody(slot, out _, out _) ?? throw new NotSupportedException("Native party-facing target has no retained current body.");
        FieldFixedPosition raw = ReadNativeScriptPosition(target) ?? throw new NotSupportedException("Native party-facing requires the target's actual raw body position.");
        float height = ReadFaceAnchorHeight?.Invoke(target) ?? throw new NotSupportedException("Native party-facing requires the target's declared query30 joint height.");
        if (target.ModelId < 0 || !float.IsFinite(height) || height < int.MinValue || (double)height > int.MaxValue)
        {
            throw new NotSupportedException("Native party-facing target has no bounded original model joint height.");
        }
        //527D30 divides signed fixed coordinates toward zero, then narrows the final target words.
        return new Vector3(unchecked((short)(raw.X / 4096)), unchecked((short)(raw.Y / 4096)), unchecked((short)(raw.Z / 4096 + (int)height)));
    }

    private string? UnsupportedNativePartyFace(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (fiber.Stack.Count < 2 || NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || actor.FaceStateVersion != 1 || actor.ModelId < 0 || ResolveNativeActorFacing is null)
        {
            return "Native party-facing requires its allocated face owner, two operands and actual native joint/facing providers";
        }
        NativeScalarValue[] operands = fiber.Stack.Capture().Take(2).ToArray();
        if (!operands[0].ToWidth(2).IsExact || !operands[1].IsExact)
        {
            return "Native party-facing requires an exact duration WORD and full DWORD slot; no representative is selected";
        }
        try
        {
            _ = PrepareNativeFaceTargets(actor, PrepareNativePartyFacePoint(fiber));
            return null;
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
    }

    private int[] ExecuteNativePartyFace(Fiber fiber, out NativeScalarValue[]? scalarArguments)
    {
        Vector3 point = PrepareNativePartyFacePoint(fiber);
        NativeScalarValue duration = fiber.Stack.PeekValue();
        int time = duration.IsExact ? duration.RequireExact() : duration.ToWidth(2).RequireExact();
        int slot = fiber.Stack.Capture().Skip(1).First().RequireExact();
        scalarArguments = duration.IsExact ? null : [NativeScalarValue.Exact(slot), duration];
        string[] expressions = fiber.StackExpressions.Take(2).Reverse().ToArray();
        PopNativeScalar(fiber);
        PopNativeScalar(fiber);
        fiber.LastExpressions = expressions;
        ActorState actor = actors[fiber.ObjectId];
        actor.FaceTargetPoint = point;
        actor.FaceDuration = unchecked((ushort)time);
        actor.FaceProgress = 0;
        actor.FaceReturning = false;
        fiber.WaitReason = actor.FaceDuration == 0 ? WaitReason.None : WaitReason.FaceDirection;
        return [slot, time];
    }
}
