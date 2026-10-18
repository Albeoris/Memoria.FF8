using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterHeaderNextResult(NativeBattleWaterExecutionFrame Frame, NativeBattleWaterSlotBank? SlotBank, int? CurrentHeaderIndex);

/// <summary>The selected header-next/reset command and its true completion branch.</summary>
public static class NativeBattleWaterHeaderNextCommand
{
    public static NativeBattleWaterHeaderNextResult Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterInitializationResource resource1, Func<NativeBattleScriptedEffectMemory, NativeBattleEffectArena, uint, uint> readHeaderBase, Func<NativeBattleScriptedEffectMemory, NativeBattleEffectArena, uint, byte, NativeBattleWaterResetCaptureInputs> readCaptureInputs)
    {
        return Execute(frame, _ => program, resource1, readHeaderBase, readCaptureInputs);
    }

    public static NativeBattleWaterHeaderNextResult Execute(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterScriptProgram> resolveProgram, NativeBattleWaterInitializationResource resource1, Func<NativeBattleScriptedEffectMemory, NativeBattleEffectArena, uint, uint> readHeaderBase, Func<NativeBattleScriptedEffectMemory, NativeBattleEffectArena, uint, byte, NativeBattleWaterResetCaptureInputs> readCaptureInputs)
    {
        return Execute(frame, resolveProgram, _ => resource1, readHeaderBase, readCaptureInputs);
    }

    public static NativeBattleWaterHeaderNextResult Execute(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterScriptProgram> resolveProgram, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterInitializationResource> resolveResource1, Func<NativeBattleScriptedEffectMemory, NativeBattleEffectArena, uint, uint> readHeaderBase, Func<NativeBattleScriptedEffectMemory, NativeBattleEffectArena, uint, byte, NativeBattleWaterResetCaptureInputs> readCaptureInputs)
    {
        if ((frame.ReadTemporaryWord(0x14A) & 0x1FF) != 1)
        {
            throw new NotSupportedException("Native header-next command requires its selected opcode.");
        }
        uint manager = CoreDword(frame, 0x27973EC);
        byte index = frame.Core.Read(manager + 0xD0, 1)[0];
        byte count = frame.Core.Read(manager + 0xD1, 1)[0];
        if (index >= count)
        {
            frame = frame with { Core = frame.Core.Write(0x2797454, Enumerable.Repeat((byte)0xFF, 0x80).ToArray()) };
            uint command = CoreDword(frame, 0x27973C0);
            frame = WriteCoreDword(frame, command + 8, 0);
            frame = WriteSlotDword(frame, 8, 0);
            frame = WriteSlotDword(frame, 4, 0);
            frame = WriteSlotDword(frame, 0, 0);
            frame = frame.WriteTemporaryWord(0x13E, 0xFFFF);
            uint header = CoreDword(frame, 0x279744C);
            frame = WriteCoreWord(frame, header + 0x18, 0);
            frame = WriteCoreDword(frame, 0x2797450, 0);
            frame = WriteCoreWord(frame, header + 0x1A, 4);
            return new(frame, null, null);
        }
        uint currentSlot = CoreDword(frame, 0x27973E8);
        uint pc = CoreDword(frame, 0x2797450);
        frame = frame with { Core = frame.Core.Write(manager + 0xD0, new byte[] { unchecked((byte)(index + 1)) }) };
        NativeBattleWaterInitializationResource resource1 = resolveResource1(frame);
        NativeBattleWaterResetResult reset = NativeBattleWaterInitialization.ResetAA4B60(frame.Core, frame.Arena, frame.TemporaryFrame, resource1, readHeaderBase, readCaptureInputs);
        frame = frame with { Core = reset.Core, Arena = reset.Arena, TemporaryFrame = reset.TemporaryFrame };
        frame = WriteCoreDword(frame, 0x2797450, pc);
        frame = WriteCoreDword(frame, 0x27973E8, currentSlot);
        NativeBattleWaterScriptProgram program = resolveProgram(frame);
        uint relative = unchecked(pc + 2 - program.Address);
        if (program.ArenaOffset < 0 || program.Length < 2 || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - 2))
        {
            throw new InvalidDataException("Native header-next operand leaves its actual current program owner.");
        }
        short branch = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(checked(program.ArenaOffset + (int)relative), 2));
        frame = WriteCoreDword(frame, 0x2797450, unchecked(pc + (uint)(int)branch));
        frame = frame.WriteTemporaryByte(0x144, 0).WriteTemporaryWord(0x13E, 0);
        return new(frame, reset.SlotBank, reset.CurrentHeaderIndex);
    }
}
