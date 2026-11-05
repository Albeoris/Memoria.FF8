using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void ValidateGameplayBackgroundAnimationWait(Fiber fiber)
    {
        if (fiber.WaitReason != WaitReason.BackgroundAnimation)
        {
            return;
        }
        if (program is null || fiber.Complete || fiber.Initialization || (uint)fiber.ObjectId >= program.Objects.Count || program.Objects[fiber.ObjectId].BackgroundModuleIndex != fiber.BackgroundId || !fiber.Script.ContainsOperation(fiber.Position - 1))
        {
            throw new InvalidDataException("Gameplay background animation wait requires its current module and consumed instruction.");
        }
        Jsm.Opcode opcode = program.Operations[fiber.Position - 1].Opcode;
        if (opcode is not (Jsm.Opcode.BGANIME or Jsm.Opcode.BGANIMESYNC) || opcode == Jsm.Opcode.BGANIME && !backgroundAnimations.ContainsKey(fiber.BackgroundId))
        {
            throw new InvalidDataException("Gameplay background animation wait lost its consumed instruction or issued animation owner.");
        }
        // BGANIMESYNC without a module animation is ready on the next real VM callback.
    }
}
