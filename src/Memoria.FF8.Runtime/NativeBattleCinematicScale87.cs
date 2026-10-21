using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicScale87
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        if (CoreDword(frame, 0x27973B8) != frame.TemporaryFrame.Address + 0x100)
        {
            throw new NotSupportedException("Native cinematic scale requires its current callback control binding.");
        }
        ushort mask = frame.ReadTemporaryWord(0x14A);
        int scale = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        int divisor = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        for (int lane = 0; lane < 6; lane++)
        {
            if ((mask & (1 << (15 - lane))) == 0)
            {
                continue;
            }
            int source = BinaryPrimitives.ReadInt32LittleEndian(ReadSlot(frame, 0x50 + lane * 4, 4)) >> 16;
            int numerator = unchecked((scale - 256) * source);
            numerator = unchecked(numerator << 8);
            if (divisor == 0 || numerator == int.MinValue && divisor == -1)
            {
                throw new NotSupportedException("Native selected scalar division would fault on its actual signed operands.");
            }
            int value = numerator / divisor;
            frame = WriteSlotDword(frame, 0x68 + lane * 4, unchecked((uint)value));
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 6);
    }
}
