using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicRangeBranch94
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterSlotBank bank, NativeBattleWaterScriptProgram program)
    {
        short selector = Operand(2);
        int selected = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank), frame.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
        int selectedOffset = bank.SlotOffset(frame.Arena, selected);
        int selectedX = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(selectedOffset + 0x94, 2));
        int toleranceX = Operand(4);
        int toleranceZ = Operand(6);
        int currentX = unchecked((short)SlotWord(frame, 0x94));
        if (selectedX < currentX - toleranceX || selectedX > currentX + toleranceX)
        {
            return NativeBattleScriptedCommandReads.Advance(frame, 10);
        }
        int selectedZ = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(selectedOffset + 0x98, 2));
        int currentZ = unchecked((short)SlotWord(frame, 0x98));
        if (selectedZ < currentZ - toleranceZ || selectedZ > currentZ + toleranceZ)
        {
            return NativeBattleScriptedCommandReads.Advance(frame, 10);
        }
        return NativeBattleScriptedCommandReads.Advance(frame, unchecked((uint)(int)Operand(8)));

        short Operand(uint offset) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, offset, 2));
    }
}
