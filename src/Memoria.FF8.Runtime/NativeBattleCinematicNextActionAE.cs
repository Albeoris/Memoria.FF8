using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Advances the unsigned relative action index and refreshes its actual actor context.</summary>
public static class NativeBattleCinematicNextActionAE
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> captureCurrent)
    {
        if ((frame.ReadTemporaryWord(0x14A) & 0x1FF) != 0xAE)
        {
            throw new NotSupportedException("Native cinematic next action requires commandAE.");
        }
        uint manager = CoreDword(frame, 0x27973EC);
        byte current = frame.Core.Read(manager + 0xD0, 1)[0];
        byte limit = frame.Core.Read(manager + 0xD1, 1)[0];
        if (current >= limit)
        {
            short relative = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
            return NativeBattleScriptedCommandReads.Advance(frame, unchecked((uint)(int)relative));
        }
        frame = frame with { Core = frame.Core.Write(manager + 0xD0, new[] { unchecked((byte)(current + 1)) }) };
        frame = captureCurrent(frame);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    public static NativeBattleWaterExecutionFrame CaptureCurrent(NativeBattleWaterExecutionFrame frame, int resourceLength, Func<byte, NativeBattleWaterResetCaptureInputs> readCaptureInputs)
    {
        uint manager = CoreDword(frame, 0x27973EC);
        byte relative = frame.Core.Read(manager + 0xD0, 1)[0];
        NativeBattleWaterResetCaptureInputs inputs = readCaptureInputs(relative);
        if (CoreDword(frame, 0x279744C) != NativeBattleScriptedEffectMemory.Address)
        {
            throw new NotSupportedException("Native cinematic action capture requires its actual shared header.");
        }
        frame = inputs.OriginalHeaderAddress is uint originalHeader
            ? WriteCoreDword(frame, manager + 0xCC, originalHeader)
            : frame with { Core = frame.Core.MarkUnknown(manager + 0xCC, 4) };
        NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        int indexInBank = NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank);
        NativeBattleWaterHeaderCaptureResult capture = NativeBattleWaterHeaderCapture.CaptureAA4E90(frame.Core.ReadHeader(), frame.Arena, bank, indexInBank, inputs.Caster, inputs.FirstHeaderFirstActor, inputs.Contacts, inputs.Models, inputs.Boundary);
        return frame with { Core = frame.Core.WithHeader(capture.Header), Arena = capture.Arena };
    }
}
