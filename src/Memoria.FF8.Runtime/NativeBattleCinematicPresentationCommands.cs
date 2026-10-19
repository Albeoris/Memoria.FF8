using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicPresentationCommands
{
    public static NativeBattleWaterExecutionFrame SetGraphicsFlag2D(NativeBattleWaterExecutionFrame frame, uint flags, Action<uint> publish)
    {
        publish(flags | 0x8000);
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }

    public static NativeBattleWaterExecutionFrame FadeMusic53(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Action<short, bool> fade)
    {
        short duration = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint runtime = CoreDword(frame, 0x27973B8);
        if (runtime != frame.TemporaryFrame.Address + 0x100)
        {
            throw new NotSupportedException("Native cinematic music fade requires its actual current callback control binding.");
        }
        bool direct = (frame.ReadTemporaryByte(0x14B) & 0x80) != 0;
        fade(duration, direct);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }
}
