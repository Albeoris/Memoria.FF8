using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicControlCommands
{
    public static NativeBattleWaterExecutionFrame Wait10D(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, short, uint> resolveSlot)
    {
        short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint selected = resolveSlot(frame, selector);
        ushort selectedFlags = BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(selected + 0x4A), 2), 2));
        ushort mask = BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        bool matched = (selectedFlags & mask & 0x3FFF) != 0;
        bool inverted = (frame.ReadTemporaryWord(0x14A) & 0x1000) != 0;
        return matched != inverted ? NativeBattleScriptedCommandReads.Advance(frame, 6) : frame.WriteTemporaryWord(0x13E, SlotWord(frame, 0xC8));
    }

    public static NativeBattleWaterExecutionFrame Execute51(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        byte flags = unchecked((byte)(frame.ReadTemporaryWord(0x14A) >> 9));
        byte current = ReadSlot(frame, 0xDE, 1)[0];
        frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0xDE, 1), new byte[] { (byte)(current | flags) }) };
        ushort operand = BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        frame = WriteSlotWord(frame, 0xC0, operand);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    public static NativeBattleWaterExecutionFrame BranchD1(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, byte stageResourceIndex)
    {
        ushort expected = BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        if (stageResourceIndex != expected)
        {
            return NativeBattleScriptedCommandReads.Advance(frame, 6);
        }
        short displacement = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        return WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + (uint)displacement));
    }
}
