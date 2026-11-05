using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private NativeGlobalJumpFrame RestoreGameplayGlobalCall(GameplaySaveGlobalCall saved)
    {
        if (program is null || (uint)saved.CallOperation >= program.Operations.Count || program.Operations[saved.CallOperation] is not { Opcode: Jsm.Opcode.GJMP } call || (uint)call.Parameter >= program.NativeScriptPointers.Count)
        {
            throw new InvalidDataException("Gameplay global call is absent from the matching installed instruction table.");
        }
        NativeScriptPointer pointer = program.NativeScriptPointers[call.Parameter];
        if ((pointer.RawPointer & 0x8000) == 0 && !Scene.NativeActorObjectIds.Any(id => call.Parameter >= program.Objects[id].Label && call.Parameter <= program.Objects[id].Label + program.Objects[id].Scripts.Count))
        {
            throw new NotSupportedException("Gameplay global call requires its current copied instruction allocation.");
        }
        return new(saved.CallOperation, pointer.Script.Start, unchecked((ushort)(saved.CallOperation + 1)), saved.OperandBase, saved.Entered);
    }

    private GameplaySaveGlobalCall[]? CaptureGameplayGlobalCalls(Fiber fiber)
    {
        if (fiber.NativeGlobalJumps.Count == 0)
        {
            return null;
        }
        ValidateNativeGlobalJumpFrames(fiber.Position, fiber.Complete, fiber.NativeGlobalJumps.ToArray(), fiber.Stack.Capture());
        GameplaySaveGlobalCall[] result = fiber.NativeGlobalJumps.Select(frame => new GameplaySaveGlobalCall(frame.CallOperation, frame.OperandBase, frame.Entered)).ToArray();
        foreach (GameplaySaveGlobalCall frame in result)
        {
            _ = RestoreGameplayGlobalCall(frame);
            if (!fiber.Script.ContainsOperation(frame.CallOperation))
            {
                throw new InvalidDataException("Gameplay global call is outside the caller's current reachable instruction graph.");
            }
        }
        return result;
    }

    private void RestoreGameplayGlobalCalls(Fiber fiber, GameplaySaveGlobalCall[]? saved)
    {
        fiber.NativeGlobalJumps = saved?.Select(RestoreGameplayGlobalCall).ToList() ?? [];
        if (fiber.NativeGlobalJumps.Any(frame => !fiber.Script.ContainsOperation(frame.CallOperation)))
        {
            throw new InvalidDataException("Gameplay global call is outside the caller's current reachable instruction graph.");
        }
        ValidateNativeGlobalJumpFrames(fiber.Position, fiber.Complete, fiber.NativeGlobalJumps.ToArray(), fiber.Stack.Capture());
    }
}
