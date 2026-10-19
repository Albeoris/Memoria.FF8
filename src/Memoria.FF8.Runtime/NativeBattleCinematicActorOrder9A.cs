using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicActorOrder9A
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        int count = frame.ReadTemporaryWord(0x14A) >> 12;
        uint attributes = CoreDword(frame, 0x279744C);
        frame = frame with { Core = frame.Core.Write(unchecked(attributes + 0x41), [unchecked((byte)count)]) };
        frame = NativeBattleScriptedCommandReads.Advance(frame, 2);
        for (int index = 0; index < count; index++)
        {
            byte value = NativeBattleScriptedCommandReads.Program(frame, program, 0, 1)[0];
            frame = frame with { Core = frame.Core.Write(unchecked(attributes + 0x48u + (uint)index), [value]) };
            frame = NativeBattleScriptedCommandReads.Advance(frame, 2);
        }
        return frame;
    }
}
