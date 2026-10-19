using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Publishes a current inline script payload and skips its signed WORD extent.</summary>
public static class NativeBattleCinematicInlineData4E
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        uint pc = CoreDword(frame, 0x2797450);
        short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint destination = unchecked(CoreDword(frame, 0x27973E8) + (uint)(0xB8 + selector * 4));
        frame = frame with { Arena = frame.Arena.Write(ArenaOffset(destination, 4), BitConverter.GetBytes(unchecked(pc + 6))) };
        short words = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        return WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + (uint)(words * 2 + 6)));
    }
}
