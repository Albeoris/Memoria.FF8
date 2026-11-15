using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicActorResourceAB
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, BattleModelOwner, uint, NativeBattleWaterExecutionFrame> bind, Func<NativeBattleWaterExecutionFrame, BattleModelOwner, short, NativeBattleWaterExecutionFrame> select)
    {
        BattleModelOwner model = NativeBattleCinematicActorAccess.ReadSlotActor(frame);
        uint row;
        (frame, row) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 32);
        frame = bind(frame, model, row);
        short sequence = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        frame = select(frame, model, sequence);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }
}
