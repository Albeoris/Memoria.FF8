using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicActorResourceAB
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleWaterExecutionFrame> bind, Func<NativeBattleWaterExecutionFrame, uint, short, NativeBattleWaterExecutionFrame> select)
    {
        byte actor = frame.Arena.Read(SlotOffset(frame, 0x1B, 1), 1)[0];
        uint owner = CoreDword(frame, 0x279744C);
        uint model = CoreDword(frame, unchecked(owner + 0x60 + 4u * actor));
        uint row;
        (frame, row) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 32);
        frame = bind(frame, model, row);
        short sequence = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        frame = select(frame, model, sequence);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }
}
