using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void ValidateGameplayAnimationWait(Fiber fiber)
    {
        if (fiber.WaitReason is not (WaitReason.Animation or WaitReason.NativeAnimationBlocking or WaitReason.NativeAnimationSync))
        {
            return;
        }
        if (program is null || fiber.Complete || fiber.Initialization || (uint)fiber.ObjectId >= actors.Count || !fiber.Script.ContainsOperation(fiber.Position - 1))
        {
            throw new InvalidDataException("Gameplay animation wait requires a live actor fiber after its consumed animation instruction.");
        }
        Jsm.Opcode opcode = program.Operations[fiber.Position - 1].Opcode;
        bool selection = opcode is Jsm.Opcode.ANIME or Jsm.Opcode.ANIMEKEEP or Jsm.Opcode.CANIME or Jsm.Opcode.CANIMEKEEP;
        if (fiber.WaitReason == WaitReason.NativeAnimationBlocking && !selection || fiber.WaitReason == WaitReason.NativeAnimationSync && opcode != Jsm.Opcode.ANIMESYNC || fiber.WaitReason == WaitReason.Animation && !selection && opcode != Jsm.Opcode.ANIMESYNC)
        {
            throw new InvalidDataException("Gameplay animation wait does not match its consumed instruction.");
        }
        ActorState actor = actors[fiber.ObjectId];
        if (fiber.WaitReason == WaitReason.Animation)
        {
            if (!float.IsFinite(actor.AnimationFrame) || actor.AnimationFirst < 0 || actor.AnimationLast < actor.AnimationFirst)
            {
                throw new InvalidDataException("Gameplay animation wait requires represented logical playback state.");
            }
            return;
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || !program.NativeObjects.Take(program.NativeActorCount).Contains(fiber.ObjectId) || actor.NativeAnimationHeader is not { Version: 1, BoundModelId: >= -1, Frame: not null, Terminal: not null, Flags: ushort flags } header || (flags & 0x07FF) != 0)
        {
            throw new NotSupportedException("Gameplay native animation wait requires its known logical phase and completion state.");
        }
        if (header.BoundModelId is int model && model >= 0 && AnimationFrameCount?.Invoke(model, header.AnimationId) is not > 0)
        {
            throw new NotSupportedException("Gameplay animation wait requires the selected current clip definition.");
        }
    }
}
