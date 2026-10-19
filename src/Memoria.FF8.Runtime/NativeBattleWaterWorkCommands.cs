using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>Selected current-resource base commands and raw slot-head expansion.</summary>
public static class NativeBattleWaterWorkCommands
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, uint? magicTextureBufferAddress = null)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        uint pc = NativeBattleWaterExecutionFields.CoreDword(frame, 0x2797450);
        int advance;
        switch (instruction & 0x1FF)
        {
            case 0x21:
                int mode = (instruction >> 12) & 7;
                uint high = Operand(4);
                uint immediate = (high << 16) | Operand(2);
                uint baseAddress;
                if ((instruction & 0x8000) != 0)
                {
                    uint resource = NativeBattleWaterExecutionFields.CoreDword(frame, 0x2798A6C);
                    uint header = unchecked(resource + 0x1C);
                    uint relative = BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(header, 4), 4));
                    baseAddress = unchecked(resource + relative);
                }
                else
                {
                    baseAddress = magicTextureBufferAddress ?? throw new NotSupportedException("Native work bases require their actual published magic texture buffer.");
                }
                uint target = unchecked(baseAddress + immediate);
                uint manager = Manager();
                switch (mode)
                {
                    default:
                        frame = NativeBattleWaterExecutionFields.WriteCoreDword(frame, checked(manager + 0x74), target);
                        frame = NativeBattleWaterExecutionFields.WriteCoreDword(frame, checked(manager + 0x70), target);
                        break;
                    case 1:
                        frame = NativeBattleWaterExecutionFields.WriteCoreDword(frame, checked(manager + 0xB8), target);
                        break;
                    case 2:
                        frame = NativeBattleWaterExecutionFields.WriteCoreDword(frame, checked(manager + 0xD4), target);
                        int size = unchecked((short)Operand(6)) << 7;
                        frame = NativeBattleWaterExecutionFields.WriteCoreDword(frame, checked(manager + 0xDC), unchecked((uint)size));
                        int parity = unchecked((sbyte)frame.ReadTemporaryByte(0x141));
                        uint cursor = unchecked(target + (uint)(parity * size));
                        frame = NativeBattleWaterExecutionFields.WriteCoreDword(frame, checked(manager + 0xD8), cursor);
                        break;
                }
                advance = 8;
                break;
            case 0x7D:
                short requested = unchecked((short)Operand(2));
                uint currentHeader = NativeBattleWaterExecutionFields.CoreDword(frame, 0x279744C);
                ushort old = NativeBattleWaterExecutionFields.CoreWord(frame, checked(currentHeader + 0x38));
                if (requested > old)
                {
                    uint cached = NativeBattleWaterExecutionFields.CoreDword(frame, checked(Manager() + 0x90));
                    for (int index = old; index < requested; index++)
                    {
                        uint address = unchecked(cached + (uint)(0x100 * index));
                        int offset = NativeBattleWaterExecutionFields.ArenaOffset(address, 4);
                        frame = frame with { Arena = frame.Arena.Write(offset, new byte[4]) };
                    }
                    frame = NativeBattleWaterExecutionFields.WriteCoreWord(frame, checked(currentHeader + 0x38), unchecked((ushort)requested));
                }
                advance = 4;
                break;
            default:
                throw new NotSupportedException("Native work commands admit only selected resource bases and slot expansion.");
        }
        return NativeBattleWaterExecutionFields.WriteCoreDword(frame, 0x2797450, unchecked(pc + (uint)advance));

        uint Manager() => NativeBattleWaterExecutionFields.CoreDword(frame, 0x27973EC);

        ushort Operand(int delta)
        {
            uint relative = unchecked(pc + (uint)delta - program.Address);
            if (program.ArenaOffset < 0 || program.Length < 2 || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - 2))
            {
                throw new InvalidDataException("Native work operand leaves its actual current arena program owner.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(checked(program.ArenaOffset + (int)relative), 2));
        }
    }
}
