using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void PublishFf8CompletionAfterCallback()
    {
        if (FieldId != 76 || programHash != Ff8CompletionState.OriginalEndingProgramHash || services is not GameSession session || session.State.Ff8Completion is not null || NativeRuntimeProfile is not { IsVerified: true } || LastFault is not null || transitionRequested || NativeGameOver is not null)
        {
            return;
        }
        foreach (Fiber fiber in fibers)
        {
            if (session.TryCompleteFf8Ending(programHash, Frame, fiber.ObjectId, fiber.Script.Id, fiber.Position, fiber.WaitReason == WaitReason.NativeHalt))
            {
                return;
            }
        }
    }
}
