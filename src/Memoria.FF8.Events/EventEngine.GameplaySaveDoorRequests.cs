using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void RestoreGameplayDoorRequestWaits(IReadOnlyList<Fiber> restored)
    {
        foreach (Fiber caller in restored.Where(fiber => fiber.WaitReason == WaitReason.NativeDoorRequest))
        {
            if (program is null || (uint)caller.Position >= program.Operations.Count || program.Operations[caller.Position] is not { Opcode: Jsm.Opcode.REQSW or Jsm.Opcode.REQEW } operation || (uint)operation.Parameter >= program.NativeObjects.Count || caller.Stack.Count < 2 || caller.Request is not { } requested)
            {
                throw new InvalidDataException("Gameplay door wait requires its held request instruction, operands and retained target request.");
            }
            int targetObject = program.NativeObjects[operation.Parameter];
            NativeDoorSchedulerState scheduler = actors[targetObject].NativeDoorScheduler ?? throw new InvalidDataException("Gameplay door wait requires its saved target scheduler.");
            // These fields already have canonical owners. Reconstruct the issued wait without queueing or polling it.
            caller.NativeDoorRequestWait = new(2, caller.Position, operation.Parameter, targetObject, caller.Stack.Skip(1).First(), caller.Stack.Peek(), requested.Sequence, scheduler.DefaultSequence)
            {
                Origin = NativeDoorRequestWaitOrigin.GameplayContinuation
            };
        }
        ValidateNativeDoorRequestWaits(actors, Scene, restored);
    }
}
