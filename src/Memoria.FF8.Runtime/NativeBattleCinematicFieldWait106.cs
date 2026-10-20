using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Waits for a signed current-node field to fall below the selected node's field.</summary>
public static class NativeBattleCinematicFieldWait106
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, short, uint> resolveNode)
    {
        if ((frame.ReadTemporaryWord(0x14A) & 0x1FF) != 0x106)
        {
            throw new NotSupportedException("Native cinematic field wait requires command106.");
        }
        short displacement = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        uint selected = resolveNode(frame, selector);
        int offset = 0x8C + displacement;
        short currentValue = BinaryPrimitives.ReadInt16LittleEndian(ReadSlot(frame, offset, 2));
        int selectedSlot = ArenaOffset(selected, 0x100);
        short selectedValue = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(checked(selectedSlot + offset), 2));
        if (currentValue < selectedValue)
        {
            return NativeBattleScriptedCommandReads.Advance(frame, 6);
        }
        return frame.WriteTemporaryWord(0x13E, SlotWord(frame, 0xC8));
    }
}
