using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicEncounterBranchC5
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, ushort encounter)
    {
        uint pc = CoreDword(frame, 0x2797450);
        short expected = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        int displacement = encounter == expected ? BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2)) : 6;
        return WriteCoreDword(frame, 0x2797450, unchecked(pc + (uint)displacement));
    }
}
