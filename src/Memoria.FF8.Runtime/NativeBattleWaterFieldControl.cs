using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Nine selected slot and command-state operations, with no general field-offset dispatch.</summary>
public static class NativeBattleWaterFieldControl
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, int currentIndex)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        uint pc = NativeBattleWaterExecutionFields.CoreDword(frame, 0x2797450);
        int advance;
        switch (instruction & 0x1FF)
        {
            case 0x23:
                int kind = instruction >> 9;
                uint color = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleWaterExecutionFields.ReadSlot(frame, 0xCC, 4));
                ushort flags = NativeBattleWaterExecutionFields.SlotWord(frame, 0x92);
                if (kind <= 3)
                {
                    frame = NativeBattleWaterExecutionFields.WriteSlotDword(frame, 0xCC, color | 0x02000000u);
                    frame = NativeBattleWaterExecutionFields.WriteSlotWord(frame, 0x92, (ushort)((flags & 0x019F) | (kind << 5)));
                }
                else
                {
                    frame = NativeBattleWaterExecutionFields.WriteSlotDword(frame, 0xCC, color & 0xFDFFFFFFu);
                    frame = NativeBattleWaterExecutionFields.WriteSlotWord(frame, 0x92, (ushort)((flags & 0xFFBF) | 0x0020));
                }
                advance = 2;
                break;
            case 0x24:
                int value = instruction >> 9;
                byte next = (value & 3) != 0 ? (byte)value : (byte)(NativeBattleWaterExecutionFields.ReadSlot(frame, 0xDE, 1)[0] | value);
                frame = WriteSlotByte(frame, 0xDE, next);
                advance = 2;
                break;
            case 0x2A:
                int stream = instruction >> 14;
                if (stream == 0)
                {
                    frame = NativeBattleWaterExecutionFields.WriteSlotDword(frame, 8, 0);
                    frame = NativeBattleWaterExecutionFields.WriteSlotDword(frame, 4, 0);
                }
                else
                {
                    frame = NativeBattleWaterExecutionFields.WriteSlotDword(frame, 4 * stream, 0);
                }
                advance = 2;
                break;
            case 0x3B:
                ushort operand = OperandWord();
                uint command = NativeBattleWaterExecutionFields.CoreDword(frame, 0x27973C0);
                ushort previous = NativeBattleWaterExecutionFields.CoreWord(frame, checked(command + 8));
                ushort mask = (ushort)(operand & 0x7FFF);
                ushort updated = (operand & 0x8000) != 0 ? (ushort)(previous & ~mask) : (ushort)(previous | mask);
                frame = NativeBattleWaterExecutionFields.WriteCoreWord(frame, checked(command + 8), updated);
                advance = 4;
                break;
            case 0x3E:
                frame = NativeBattleWaterExecutionFields.WriteSlotDword(frame, 0xBC, unchecked(pc + 2));
                advance = 8;
                break;
            case 0x40:
                short selector = unchecked((short)OperandWord());
                int currentOffset = bank.SlotOffset(frame.Arena, currentIndex);
                if (NativeBattleWaterExecutionFields.CoreDword(frame, 0x27973E8) != NativeBattleEffectNodeMemory.ArenaAddress + (uint)currentOffset)
                {
                    throw new InvalidDataException("Native field lookup requires the actual current slot in its cached bank.");
                }
                int reference = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, currentIndex, ActiveOrder(frame), selector);
                int referenceOffset = bank.SlotOffset(frame.Arena, reference);
                uint packed = 0;
                for (int lane = 0; lane < 3; lane++)
                {
                    short channel = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(referenceOffset + 0x94 + 2 * lane, 2));
                    packed |= (uint)Math.Clamp((int)channel, 0, 255) << (8 * lane);
                }
                packed |= (uint)NativeBattleWaterExecutionFields.ReadSlot(frame, 0xCF, 1)[0] << 24;
                frame = NativeBattleWaterExecutionFields.WriteSlotDword(frame, 0xCC, packed);
                advance = 4;
                break;
            case 0x47:
                if ((instruction & 0x8000) != 0)
                {
                    frame = frame.WriteTemporaryByte(0x145, 0xFF);
                }
                if ((instruction & 0x4000) != 0)
                {
                    frame = frame.WriteTemporaryByte(0x145, 0);
                }
                int cursor = frame.ReadTemporaryByte(0x144);
                if (cursor >= 128)
                {
                    throw new NotSupportedException("Native field control left its bounded active-order owner.");
                }
                uint entryAddress = 0x2797454 + (uint)cursor;
                frame = frame with { Core = frame.Core.Write(entryAddress, new byte[] { (byte)(frame.Core.Read(entryAddress, 1)[0] | 0x80) }) };
                advance = 2;
                break;
            case 0x68:
                frame = NativeBattleWaterExecutionFields.WriteSlotWord(frame, 0x9C, OperandWord());
                advance = 4;
                break;
            case 0xB1:
                byte scalar = Program(2, 1)[0];
                uint state = NativeBattleWaterExecutionFields.CoreDword(frame, 0x27973C0);
                frame = frame with { Core = frame.Core.Write(checked(state + 6), new byte[] { scalar }) };
                advance = 4;
                break;
            default:
                throw new NotSupportedException("Native field control admits only its nine selected operations.");
        }
        return NativeBattleWaterExecutionFields.WriteCoreDword(frame, 0x2797450, unchecked(pc + (uint)advance));

        ushort OperandWord() => BinaryPrimitives.ReadUInt16LittleEndian(Program(2, 2));

        byte[] Program(int delta, int length)
        {
            uint relative = unchecked(pc + (uint)delta - program.Address);
            if (program.ArenaOffset < 0 || program.Length < length || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - length))
            {
                throw new InvalidDataException("Native field operand leaves its actual current arena program owner.");
            }
            return frame.Arena.Read(checked(program.ArenaOffset + (int)relative), length);
        }
    }

    private static NativeBattleWaterExecutionFrame WriteSlotByte(NativeBattleWaterExecutionFrame frame, int offset, byte value) => frame with { Arena = frame.Arena.Write(NativeBattleWaterExecutionFields.SlotOffset(frame, offset, 1), new byte[] { value }) };

    private static ImmutableArray<byte> ActiveOrder(NativeBattleWaterExecutionFrame frame)
    {
        //Only the prefix through the actual terminator is consumed. Remaining entries are transient unused placeholders.
        byte[] order = Enumerable.Repeat((byte)0xFF, 128).ToArray();
        for (int index = 0; index < order.Length; index++)
        {
            order[index] = frame.Core.Read(0x2797454 + (uint)index, 1)[0];
            if (order[index] == 0xFF)
            {
                return order.ToImmutableArray();
            }
        }
        throw new NotSupportedException("Native field lookup requires a terminated active-order owner.");
    }
}
