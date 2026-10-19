using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Construction and selection of the shared retained cinematic light sets.</summary>
public static class NativeBattleScriptedLightCommands
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, int currentIndex)
    {
        int opcode = frame.ReadTemporaryWord(0x14A) & 0x1FF;
        short selected = Operand(2);
        if (opcode == 0xAA)
        {
            frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0xE1, 1), [selected == -1 ? (byte)0 : unchecked((byte)selected)]) };
            return NativeBattleScriptedCommandReads.Advance(frame, 4);
        }
        if (opcode != 0x93)
        {
            throw new NotSupportedException("Native light commands require their selected construction or selection instruction.");
        }
        uint set = unchecked(0x27977A4u + (uint)(0x50 * selected));
        for (int column = 0; column < 3; column++)
        {
            short selector = Operand(4 + 2 * column);
            if (selector == -1)
            {
                for (int row = 0; row < 3; row++)
                {
                    frame = frame.WriteTemporaryWord(0x20 + 6 * column + 2 * row, 0);
                }
                continue;
            }
            int referenced = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, currentIndex, frame.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            int offset = bank.SlotOffset(frame.Arena, referenced);
            for (int row = 0; row < 3; row++)
            {
                ushort position = BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(offset + 0x94 + 2 * row, 2));
                ushort angle = BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(offset + 0x8C + 2 * row, 2));
                frame = frame.WriteTemporaryWord(0x20 + 6 * column + 2 * row, unchecked((ushort)(position << 4)));
                frame = WriteCoreWord(frame, unchecked(set + 0x20u + (uint)(6 * row + 2 * column)), unchecked((ushort)(angle << 4)));
            }
        }
        for (int lane = 0; lane < 3; lane++)
        {
            frame = frame.WriteTemporaryWord(0x50 + 2 * lane, unchecked((ushort)(ReadSlot(frame, 0x94 + 2 * lane, 1)[0] << 4)));
        }
        NativeBattleVector angles = new(unchecked((short)frame.ReadTemporaryWord(0x50)), unchecked((short)frame.ReadTemporaryWord(0x52)), unchecked((short)frame.ReadTemporaryWord(0x54)));
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(angles);
        frame = WriteRotation(frame, set, rotation);
        NativeBattleActionMatrix rows = new(Enumerable.Range(0, 9).Select(lane => unchecked((short)frame.ReadTemporaryWord(0x20 + lane * 2))).ToImmutableArray(), 0, 0, 0, 4096);
        frame = WriteRotation(frame, set, NativeBattleGfMatrices.Multiply56C090(rows, rotation));
        for (int lane = 0; lane < 3; lane++)
        {
            frame = WriteCoreDword(frame, unchecked(set + 0x40u + (uint)(4 * lane)), unchecked((uint)((short)SlotWord(frame, 0x8C + 2 * lane) << 4)));
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 10);

        short Operand(int relative) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, (uint)relative, 2));
    }

    private static NativeBattleWaterExecutionFrame WriteRotation(NativeBattleWaterExecutionFrame frame, uint address, NativeBattleActionMatrix matrix)
    {
        for (int lane = 0; lane < 9; lane++)
        {
            frame = WriteCoreWord(frame, unchecked(address + (uint)(lane * 2)), unchecked((ushort)matrix.Rotation[lane]));
        }
        return frame with { Core = frame.Core.MarkUnknown(unchecked(address + 18), 2) };
    }
}
