using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Flag modification, relative branches and scheduler waits on the current slot.</summary>
public static class NativeBattleCinematicFlagControl10C
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        ushort flags = SlotWord(frame, 0x4A);
        uint pc = CoreDword(frame, 0x2797450);
        short operand = ReadWord(2);
        bool intersects = (flags & operand) != 0;
        switch (instruction >> 12)
        {
            case 1:
                frame = WriteSlotWord(frame, 0x4A, unchecked((ushort)(flags & ~operand)));
                break;
            case 2:
            case 3:
                bool jump = instruction >> 12 == 2 ? intersects : !intersects;
                return WriteCoreDword(frame, 0x2797450, jump ? unchecked(pc + (uint)ReadWord(4)) : unchecked(pc + 6));
            case 4:
            case 5:
                bool wait = instruction >> 12 == 4 ? intersects : !intersects;
                if (wait)
                {
                    return frame.WriteTemporaryWord(0x13E, SlotWord(frame, 0xC8));
                }
                break;
            default:
                frame = WriteSlotWord(frame, 0x4A, unchecked((ushort)(flags | unchecked((ushort)operand))));
                break;
        }
        return WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + 4));

        short ReadWord(uint offset)
        {
            uint relative = unchecked(pc + offset - program.Address);
            if (program.Length < 2 || program.ArenaOffset < 0 || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - 2))
            {
                throw new InvalidDataException("Native flag-control operand leaves its actual current programme.");
            }
            return BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(checked(program.ArenaOffset + (int)relative), 2));
        }
    }
}
