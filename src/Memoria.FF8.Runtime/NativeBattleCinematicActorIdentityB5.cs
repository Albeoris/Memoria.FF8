using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicActorIdentityB5
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        if ((frame.ReadTemporaryWord(0x14A) & 0x1FF) != 0xB5)
        {
            throw new NotSupportedException("Native actor identity branch requires instruction B5.");
        }
        byte actor = ReadSlot(frame, 0x1B, 1)[0];
        ushort expected = BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        if (actor != expected)
        {
            return NativeBattleScriptedCommandReads.Advance(frame, 6);
        }
        short displacement = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        return NativeBattleScriptedCommandReads.Advance(frame, unchecked((uint)(int)displacement));
    }
}
