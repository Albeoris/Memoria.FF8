using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterScriptProgram(int ArenaOffset, int Length, uint Address);
public sealed record NativeBattleWaterScriptControlResult(NativeBattleEffectArena Arena, uint NextInstructionPointer, ushort HeaderFlagsWord2, ushort? YieldWait);

/// <summary>Finite slot control operations over their current arena operands and explicit caller-owned header flags.</summary>
public static class NativeBattleWaterScriptControl
{
    public static NativeBattleWaterScriptControlResult Execute(NativeBattleEffectArena arena, NativeBattleWaterSlotBank bank, int index, NativeBattleWaterScriptProgram program, uint instructionPointer, int streamIndex, ushort headerFlagsWord2)
    {
        arena.Validate();
        if (streamIndex is < 0 or > 2)
        {
            throw new InvalidDataException("Native script control requires its actual bounded slot and stream index.");
        }
        if (program.ArenaOffset < 0 || program.Length < 0 || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length)
        {
            throw new InvalidDataException("Native script control requires its bounded current resource and operand owner.");
        }
        int slot = bank.SlotOffset(arena, index);
        ushort instruction = Operand(0);
        uint next = instructionPointer;
        ushort? wait = null;
        switch (instruction & 0x1FF)
        {
            case 0x02:
                next = Relative((short)Operand(2));
                break;
            case 0x03:
                sbyte oldDepth = unchecked((sbyte)Read(0x44 + streamIndex, 1)[0]);
                WriteByte(0x44 + streamIndex, unchecked((byte)(oldDepth + 1)));
                short callRelative = unchecked((short)Operand(2));
                WriteDword(0x24 + 4 * (oldDepth + 2 * streamIndex), unchecked(instructionPointer + 4));
                next = Relative(callRelative);
                break;
            case 0x04:
                byte depth = unchecked((byte)(Read(0x44 + streamIndex, 1)[0] - 1));
                WriteByte(0x44 + streamIndex, depth);
                next = BinaryPrimitives.ReadUInt32LittleEndian(Read(0x24 + 4 * (unchecked((sbyte)depth) + 2 * streamIndex), 4));
                break;
            case 0x05:
                ushort mask = Operand(2);
                bool matches = (headerFlagsWord2 & mask) != 0;
                switch (instruction >> 12)
                {
                    case 1:
                        headerFlagsWord2 = unchecked((ushort)(headerFlagsWord2 & ~mask));
                        next = Relative(4);
                        break;
                    case 2:
                    case 3:
                        bool take = instruction >> 12 == 2 ? matches : !matches;
                        next = take ? Relative(unchecked((short)Operand(4))) : Relative(6);
                        break;
                    case 4:
                    case 5:
                        bool advance = instruction >> 12 == 4 ? !matches : matches;
                        if (advance)
                        {
                            next = Relative(4);
                        }
                        else
                        {
                            wait = Word(0xC8);
                        }
                        break;
                    default:
                        headerFlagsWord2 |= mask;
                        next = Relative(4);
                        break;
                }
                break;
            case 0x09:
                next = Relative(2);
                wait = unchecked((ushort)((instruction >> 9) << 7));
                break;
            case 0x1E:
                WriteByte(0x20 + (instruction >> 14), unchecked((byte)OperandByte(2)));
                next = Relative(4);
                break;
            case 0x1F:
                int counterOffset = 0x20 + (instruction >> 14);
                byte counter = unchecked((byte)(Read(counterOffset, 1)[0] - 1));
                WriteByte(counterOffset, counter);
                next = counter == 0 ? Relative(4) : Relative(unchecked((short)Operand(2)));
                break;
            case 0x33:
                WriteWord(0x12, Operand(2));
                next = Relative(4);
                break;
            case 0x74:
                ushort slotMask = Operand(2);
                if ((instruction & 0x8000) != 0)
                {
                    slotMask = unchecked((ushort)~slotMask);
                    WriteWord(0x4C, unchecked((ushort)(Word(0x4C) & slotMask)));
                }
                WriteWord(0x4C, unchecked((ushort)(Word(0x4C) | slotMask)));
                next = Relative(4);
                break;
            case 0xBC:
                WriteWord(0xCA, Operand(2));
                next = Relative(4);
                break;
            case 0xE7:
                int offset = unchecked((short)Operand(2));
                short threshold = unchecked((short)Operand(4));
                if (unchecked((short)Word(0x8C + offset)) > threshold)
                {
                    next = Relative(6);
                }
                else
                {
                    wait = Word(0xC8);
                }
                break;
            case 0x79:
                int value = BinaryPrimitives.ReadInt32LittleEndian(Read(0x50 + ((instruction >> 10) & 0x1C), 4)) >> 16;
                short limit = unchecked((short)Operand(2));
                bool passes = (instruction & 0x8000) != 0 ? value > limit : value < limit;
                next = passes ? Relative(6) : Relative(unchecked((short)Operand(4)));
                break;
            default:
                throw new NotSupportedException("Native script control admits only its twelve finite slot operations.");
        }
        return new(arena, next, headerFlagsWord2, wait);

        uint Relative(int delta) => unchecked(instructionPointer + (uint)delta);

        byte OperandByte(int delta)
        {
            uint position = unchecked(Relative(delta) - program.Address);
            if (position >= (uint)program.Length)
            {
                throw new InvalidDataException("Native script operand leaves its explicit current arena program owner.");
            }
            return arena.Read(checked(program.ArenaOffset + (int)position), 1)[0];
        }

        ushort Operand(int delta)
        {
            uint position = unchecked(Relative(delta) - program.Address);
            if (program.Length < 2 || position > (uint)(program.Length - 2))
            {
                throw new InvalidDataException("Native script operand leaves its explicit current arena program owner.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(checked(program.ArenaOffset + (int)position), 2));
        }

        byte[] Read(int offset, int length)
        {
            RequireSlot(offset, length);
            return arena.Read(slot + offset, length);
        }

        ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Read(offset, 2));

        void RequireSlot(int offset, int length)
        {
            if (offset < 0 || offset > 0x100 - length)
            {
                throw new NotSupportedException("Native script stack or field access leaves the physical slot owner.");
            }
        }

        void WriteByte(int offset, byte value)
        {
            RequireSlot(offset, 1);
            arena = arena.Write(slot + offset, new byte[] { value });
        }

        void WriteWord(int offset, ushort value)
        {
            RequireSlot(offset, 2);
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            arena = arena.Write(slot + offset, bytes);
        }

        void WriteDword(int offset, uint value)
        {
            RequireSlot(offset, 4);
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            arena = arena.Write(slot + offset, bytes);
        }
    }
}
