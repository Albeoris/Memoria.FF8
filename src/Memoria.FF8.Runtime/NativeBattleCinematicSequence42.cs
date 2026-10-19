using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicSequenceServices(Action<byte, short> Select, Func<byte, bool> PoseComplete);

public static class NativeBattleCinematicSequence42
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleCinematicSequenceServices services)
    {
        int mode = frame.ReadTemporaryWord(0x14A) >> 9;
        if (mode is not (0 or 2))
        {
            return frame;
        }
        byte actor = ReadSlot(frame, 0x1B, 1)[0];
        if (mode == 0)
        {
            short sequence = Operand();
            services.Select(actor, sequence);
            return NativeBattleScriptedCommandReads.Advance(frame, 4);
        }
        return NativeBattleScriptedCommandReads.Advance(frame, services.PoseComplete(actor) ? unchecked((uint)(int)Operand()) : 4u);

        short Operand() => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
    }
}
