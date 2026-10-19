using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicDisplayServices(Action<uint, byte, byte> WriteParameters, Action<short> ReverseBlend, Action<short> BeginBlend, Action SetUiModeZero);

public static class NativeBattleCinematicDisplay8A
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleCinematicDisplayServices services)
    {
        int mode = frame.ReadTemporaryWord(0x14A) >> 12;
        switch (mode)
        {
            case 1:
                services.ReverseBlend(Word(2));
                return NativeBattleScriptedCommandReads.Advance(frame, 4);
            case 2:
                services.BeginBlend(Word(2));
                return NativeBattleScriptedCommandReads.Advance(frame, 4);
            case 3:
                services.SetUiModeZero();
                return NativeBattleScriptedCommandReads.Advance(frame, 2);
            default:
                uint manager = CoreDword(frame, 0x27973EC);
                uint parameters = CoreDword(frame, manager + 0xC0);
                byte parameter83 = unchecked((byte)(Byte(2) + 1));
                byte parameter81 = Byte(4);
                services.WriteParameters(parameters, parameter83, parameter81);
                return NativeBattleScriptedCommandReads.Advance(frame, 6);
        }

        short Word(uint delta) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, delta, 2));
        byte Byte(uint delta) => NativeBattleScriptedCommandReads.Program(frame, program, delta, 1)[0];
    }
}
