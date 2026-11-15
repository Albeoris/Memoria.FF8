using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicActorMeshMask8D
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<BattleModelOwner, uint> readMask, Action<BattleModelOwner, uint> publishMask)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) != 0x8D)
        {
            throw new NotSupportedException("Native actor mesh mask requires its selected instruction.");
        }
        int operand = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        BattleModelOwner actor = NativeBattleCinematicActorAccess.ReadSlotActor(frame);
        uint mask = readMask(actor);
        uint selected = unchecked((uint)operand);
        publishMask(actor, (instruction & 0x8000) != 0 ? mask & ~selected : mask | selected);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }
}
