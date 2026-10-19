using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleScriptedSchedulingCommands
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        switch (instruction & 0x1FF)
        {
            case 7:
                if ((instruction & 0x8000) != 0)
                {
                    frame = frame.WriteTemporaryByte(0x145, 0xFF);
                }
                if ((instruction & 0x4000) != 0)
                {
                    frame = frame.WriteTemporaryByte(0x145, 0);
                }
                uint entry = 0x2797454u + frame.ReadTemporaryByte(0x144);
                byte current = frame.Core.Read(entry, 1)[0];
                frame = frame with { Core = frame.Core.Write(entry, [(byte)(current | 0x80)]) };
                return NativeBattleScriptedCommandReads.Advance(frame, 2);
            case 0xA0:
                uint manager = CoreDword(frame, 0x27973EC);
                if ((CoreWord(frame, manager) & 0x2000) != 0)
                {
                    return NativeBattleScriptedCommandReads.Advance(frame, 4);
                }
                short displacement = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
                return WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + (uint)displacement));
            default:
                throw new NotSupportedException("Native cinematic scheduling command requires its selected filter or conditional jump instruction.");
        }
    }
}
