using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScriptedVibrationStep(NativeBattleWaterExecutionFrame Frame, NativeVibrationProcessState Vibration);

public static class NativeBattleScriptedVibrationCommands
{
    public static NativeBattleScriptedVibrationStep Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeVibrationProcessState vibration, Func<NativeBattleWaterExecutionFrame, uint, NativeVibrationBank> resolveBank)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) != 0x39 || instruction >> 12 is not (2 or 3))
        {
            throw new NotSupportedException("Native cinematic vibration commands require their selected start or stop mode.");
        }
        uint scene = CoreDword(frame, 0x279744C);
        if (instruction >> 12 == 2)
        {
            byte[] operands = NativeBattleScriptedCommandReads.Program(frame, program, 2, 2);
            uint file = CoreDword(frame, 0x2798A68);
            uint relative = BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(file + 0x24), 4), 4));
            uint address = unchecked(file + relative);
            NativeVibrationEnqueue result = vibration.Enqueue(resolveBank(frame, address), operands[1] + 1, 0x80);
            vibration = result.State;
            frame = WriteCoreDword(frame, unchecked(scene + 0x168u + (uint)(4 * operands[0])), unchecked((uint)result.Handle));
        }
        else
        {
            for (int index = 0; index < 3; index++)
            {
                int handle = unchecked((int)CoreDword(frame, scene + 0x168u + (uint)(4 * index)));
                if (handle != 0)
                {
                    vibration = vibration.Stop4A2940(handle);
                }
            }
        }
        return new(NativeBattleScriptedCommandReads.Advance(frame, 4), vibration);
    }
}
