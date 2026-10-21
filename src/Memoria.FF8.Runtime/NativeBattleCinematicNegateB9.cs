using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicNegateB9
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, int index, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> updateSlot)
    {
        short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        if (selector > 0)
        {
            uint address = unchecked(CoreDword(frame, 0x27973E8) + 0x50u + (uint)(4 * selector));
            int offset = ArenaOffset(address, 4);
            int value = BinaryPrimitives.ReadInt32LittleEndian(frame.Arena.Read(offset, 4));
            frame = frame with { Arena = frame.Arena.Write(offset, BitConverter.GetBytes(unchecked(-value))) };
            if (selector <= 2)
            {
                frame = frame with { Arena = NativeBattleWaterSlots.CachePositionAA17D0(frame.Arena, bank, index) };
            }
            else if (selector <= 5)
            {
                frame = updateSlot(frame, ReadSlot(frame, 0x18, 1)[0]);
            }
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }
}
