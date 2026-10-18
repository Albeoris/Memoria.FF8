using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Selected current-arena particle bank allocation and ring overwrite commands.</summary>
public static class NativeBattleWaterParticleCommands
{
    public static (NativeBattleWaterExecutionFrame Frame, uint Address) AllocateB656E0(NativeBattleWaterExecutionFrame frame, uint size)
    {
        uint manager = CoreDword(frame, 0x27973EC);
        uint address = CoreDword(frame, manager + 0x74);
        uint remainder = size & 3;
        uint aligned = remainder == 0 ? size : unchecked(size + 4 - remainder);
        frame = WriteCoreDword(frame, manager + 0x74, unchecked(address + aligned));
        return (frame, address);
    }

    public static NativeBattleWaterExecutionFrame RegisterDrawB65320(NativeBattleWaterExecutionFrame frame, byte kind)
    {
        frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0x1C, 1), new byte[] { kind }) };
        byte current = frame.ReadTemporaryByte(0x142);
        for (int index = 0; index < 128; index++)
        {
            uint address = 0x2797554 + (uint)index;
            byte entry = frame.Core.Read(address, 1)[0];
            if (entry == 0xFF)
            {
                return frame with { Core = frame.Core.Write(address, new byte[] { current }) };
            }
            if ((entry & 0x7F) == current)
            {
                return frame;
            }
        }
        throw new NotSupportedException("Native draw registration left its actual bounded draw-order owner.");
    }

    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        uint pc = CoreDword(frame, 0x2797450);
        switch (instruction & 0x1FF)
        {
            case 0x58:
                short count = unchecked((short)Operand());
                if (count < 0)
                {
                    throw new NotSupportedException("Native selected particle allocation requires a nonnegative record count.");
                }
                uint bank;
                (frame, bank) = AllocateB656E0(frame, unchecked((uint)(0x10 + 0x50 * count)));
                frame = WriteSlotDword(frame, 0xB8, bank);
                frame = WriteWord(frame, bank, unchecked((ushort)count));
                frame = WriteWord(frame, unchecked(bank + 2), 0);
                frame = WriteWord(frame, unchecked(bank + 4), 0);
                frame = WriteWord(frame, unchecked(bank + 6), 0);
                for (int index = 0; index < count; index++)
                {
                    uint record = unchecked(bank + 0x10 + (uint)(0x50 * index));
                    frame = WriteWord(frame, record, 0);
                    frame = WriteDword(frame, unchecked(record + 8), 0);
                    frame = WriteDword(frame, unchecked(record + 0x30), 0);
                    frame = WriteDword(frame, unchecked(record + 4), 0);
                }
                frame = RegisterDrawB65320(frame, 6);
                break;
            case 0x59:
                if ((instruction & 0x1000) != 0)
                {
                    throw new NotSupportedException("Native selected particle ring spawn does not admit its alternate lookup modifier.");
                }
                uint ownerBank = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
                ushort capacity = ReadWord(frame, ownerBank);
                ushort cursor = ReadWord(frame, unchecked(ownerBank + 2));
                frame = WriteWord(frame, unchecked(ownerBank + 2), unchecked((ushort)(cursor + 1)));
                if (capacity == 0)
                {
                    throw new NotSupportedException("Native particle ring division requires its actual nonzero record count.");
                }
                uint target = unchecked(ownerBank + 0x10 + (uint)(0x50 * (cursor % capacity)));
                frame = WriteWord(frame, unchecked(ownerBank + 8), ReadSlot(frame, 0x18, 1)[0]);
                frame = WriteWord(frame, target, 1);
                frame = WriteWord(frame, unchecked(target + 2), 0);
                frame = WriteDword(frame, unchecked(target + 4), unchecked(pc + (uint)(int)(short)Operand()));
                frame = frame.WriteTemporaryDword(0x38, 0).WriteTemporaryDword(0x3C, 0);
                for (int offset = 0x20; offset <= 0x2C; offset += 4)
                {
                    frame = WriteDword(frame, unchecked(target + (uint)offset), 0);
                }
                for (int lane = 0; lane < 3; lane++)
                {
                    int position = unchecked((short)SlotWord(frame, 0x94 + lane * 2));
                    frame = WriteDword(frame, unchecked(target + 0x14 + (uint)(lane * 4)), unchecked((uint)(position << 8)));
                }
                frame = WriteWord(frame, unchecked(target + 0x40), SlotWord(frame, 0x92));
                frame = WriteWord(frame, unchecked(target + 0x48), SlotWord(frame, 0x9A));
                uint color = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4)) & 0x02FFFFFF;
                frame = WriteDword(frame, unchecked(target + 0x3C), 0);
                frame = WriteDword(frame, unchecked(target + 0x38), color);
                frame = frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(target + 0x37), 1), new byte[] { 1 }) };
                break;
            default:
                throw new NotSupportedException("Native particle bank commands admit only selected operations 58 and 59.");
        }
        return WriteCoreDword(frame, 0x2797450, unchecked(pc + 4));

        ushort Operand()
        {
            uint relative = unchecked(pc + 2 - program.Address);
            if (program.ArenaOffset < 0 || program.Length < 2 || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - 2))
            {
                throw new InvalidDataException("Native particle command operand leaves its current program owner.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(checked(program.ArenaOffset + (int)relative), 2));
        }
    }

    private static ushort ReadWord(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));

    private static NativeBattleWaterExecutionFrame WriteWord(NativeBattleWaterExecutionFrame frame, uint address, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), bytes) };
    }

    private static NativeBattleWaterExecutionFrame WriteDword(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), bytes) };
    }
}
