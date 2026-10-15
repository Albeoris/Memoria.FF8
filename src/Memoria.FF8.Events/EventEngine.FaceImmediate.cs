using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeFaceImmediate(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (fiber.Stack.Count < 4 || NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || actor.FaceStateVersion != 1 || actor.RenderModelId < 0)
        {
            return "Native immediate facing requires four consumed WORDs and the retained allocated face/model owner";
        }
        if (fiber.Stack.Capture().Take(4).Any(value => value.ActorInfoOrigin is not null || !value.ToWidth(2).IsExact))
        {
            return "Native immediate facing requires exact consumed low WORDs; no representative is selected";
        }
        return null;
    }

    private int[] ExecuteNativeFaceImmediate(Fiber fiber, bool asynchronous, out NativeScalarValue[]? scalarArguments)
    {
        int[] args = PopNativeFaceWords(fiber, out scalarArguments);
        ActorState actor = actors[fiber.ObjectId];
        //527AF0/527F10 ignore the inline parameter and write554/558/562/564.
        //BYTE571=0 shares FACEDIROFF's nontracking draw/override-release mode.
        actor.FacePitchTarget = unchecked((short)args[0]);
        actor.FacePendingBaseline = unchecked((short)args[1]);
        actor.FaceYawTarget = unchecked((short)args[2]);
        actor.FaceDuration = unchecked((ushort)args[3]);
        actor.FaceProgress = 0;
        actor.FaceTargetPoint = null;
        actor.FaceReturning = true;
        fiber.WaitReason = asynchronous || actor.FaceDuration == 0 ? WaitReason.None : WaitReason.FaceDirection;
        return args;
    }
}
