using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicActorIdentityB5
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleModelAliases aliases)
    {
        if ((frame.ReadTemporaryWord(0x14A) & 0x1FF) != 0xB5)
        {
            throw new NotSupportedException("Native actor identity branch requires instruction B5.");
        }
        BattleModelOwner actor = NativeBattleCinematicActorAccess.ReadSlotActor(frame);
        ushort expected = BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        if (!aliases.TryGetAuthoredOperand(actor, out byte original) || original != expected)
        {
            return NativeBattleScriptedCommandReads.Advance(frame, 6);
        }
        short displacement = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        return NativeBattleScriptedCommandReads.Advance(frame, unchecked((uint)(int)displacement));
    }
}
