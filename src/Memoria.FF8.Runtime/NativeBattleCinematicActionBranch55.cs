using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>The cinematic parameter-mode and action-index conditional jump.</summary>
public static class NativeBattleCinematicActionBranch55
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, uint, int, byte[]> readParameters)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) != 0x55)
        {
            throw new NotSupportedException("Native cinematic action branch requires command55.");
        }
        uint manager = CoreDword(frame, 0x27973EC);
        uint parameters = CoreDword(frame, manager + 0xC0);
        bool branch;
        switch (instruction >> 9)
        {
            case 1:
                branch = frame.Core.Read(manager + 0xD0, 1)[0] != frame.Core.Read(manager + 0xD1, 1)[0];
                break;
            case 2:
            case 3:
                byte[] raw = readParameters(frame, unchecked(parameters + 1), 1);
                if (raw is null || raw.Length != 1)
                {
                    throw new InvalidDataException("Native cinematic action branch requires the actual parameter BYTE1.");
                }
                bool bit2 = (raw[0] & 2) != 0;
                branch = (raw[0] & 3) != 0 && (instruction >> 9 == 3 ? bit2 : !bit2);
                break;
            default:
                branch = frame.Core.Read(manager + 0xD0, 1)[0] >= 1;
                break;
        }
        uint delta = branch ? unchecked((uint)(int)BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2))) : 4u;
        return NativeBattleScriptedCommandReads.Advance(frame, delta);
    }
}
