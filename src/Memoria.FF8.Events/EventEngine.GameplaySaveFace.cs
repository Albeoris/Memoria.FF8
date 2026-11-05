using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void ValidateGameplayFaceActor(ActorState actor)
    {
        bool active = actor.FaceReturning || actor.FaceProgress < actor.FaceDuration;
        if (actor.FaceDuration is < 0 or > ushort.MaxValue || actor.FaceProgress < 0 || actor.FaceProgress > actor.FaceDuration || actor.FaceReturning && actor.FaceTargetPoint is not null || active && (actor.FaceStateVersion != 1 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || !actor.FaceReturning && actor.FaceTargetPoint is null))
        {
            throw new InvalidDataException("Gameplay face continuation requires allocated logical targets and a bounded native phase.");
        }
        if (actor.FaceTargetPoint is not null && actor.FaceProgress == 0 && (ReadFaceAnchorHeight is null || ResolveNativeActorFacing is null))
        {
            throw new NotSupportedException("Gameplay face continuation requires current native point-facing providers before its first phase.");
        }
    }

    private void ValidateGameplayFaceWait(Fiber fiber)
    {
        if (fiber.WaitReason is not (WaitReason.FaceDirection or WaitReason.NativeFaceSync))
        {
            return;
        }
        if (program is null || fiber.Complete || fiber.Initialization || (uint)fiber.ObjectId >= actors.Count || actors[fiber.ObjectId].FaceStateVersion != 1 || !Scene.NativeActorObjectIds.Contains(fiber.ObjectId))
        {
            throw new InvalidDataException("Gameplay face wait requires a live allocated actor and completed initialization.");
        }
        ValidateGameplayFaceActor(actors[fiber.ObjectId]);
        int position = fiber.WaitReason == WaitReason.NativeFaceSync ? fiber.Position : fiber.Position - 1;
        if (!fiber.Script.ContainsOperation(position))
        {
            throw new InvalidDataException("Gameplay face wait lost its retained instruction.");
        }
        Jsm.Opcode opcode = program.Operations[position].Opcode;
        bool blocking = opcode is Jsm.Opcode.FACEDIR or Jsm.Opcode.FACEDIRI or Jsm.Opcode.FACEDIRA or Jsm.Opcode.FACEDIRP or Jsm.Opcode.FACEDIROFF;
        if (fiber.WaitReason == WaitReason.NativeFaceSync ? opcode != Jsm.Opcode.FACEDIRSYNC : !blocking)
        {
            throw new InvalidDataException("Gameplay face wait does not match its consumed or retried instruction.");
        }
    }
}
