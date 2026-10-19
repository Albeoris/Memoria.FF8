using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>The selected particle command derives signed acceleration from its three velocity words.</summary>
public static class NativeBattleGuestParticle22
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        if ((byte)frame.ReadTemporaryWord(0x14A) != 22)
        {
            throw new NotSupportedException("Native particle deceleration requires its selected command.");
        }
        short divisor = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint particle = frame.ReadTemporaryDword(0x44);
        for (int lane = 0; lane < 3; lane++)
        {
            short velocity = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(particle + 0x22u + (uint)lane * 2), 2), 2));
            byte[] value = new byte[2];
            BinaryPrimitives.WriteInt16LittleEndian(value, unchecked((short)-(velocity / divisor)));
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(particle + 0x2Au + (uint)lane * 2), 2), value) };
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }
}
