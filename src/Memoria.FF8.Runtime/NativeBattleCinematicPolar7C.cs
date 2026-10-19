using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>The cinematic random polar offsets applied to two retained motion channels.</summary>
public static class NativeBattleCinematicPolar7C
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        if ((frame.ReadTemporaryWord(0x14A) & 0x1FF) != 0x7C)
        {
            throw new NotSupportedException("Native polar motion requires its selected command.");
        }
        int angle = Random(4096);
        int cosineRadius = Random(Operand(4));
        cosineRadius = unchecked(cosineRadius + Operand(2));
        uint cosineMotion = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0x64, 4));
        uint cosineDelta = unchecked((uint)((NativeBattleCameraMath.Cosine(angle) * cosineRadius) << 4));
        frame = WriteSlotDword(frame, 0x64, unchecked(cosineMotion + cosineDelta));
        int sineRadius = Random(Operand(8));
        sineRadius = unchecked(sineRadius + Operand(6));
        uint sineMotion = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0x5C, 4));
        uint sineDelta = unchecked((uint)((NativeBattleCameraMath.Sine(angle) * sineRadius) << 4));
        frame = WriteSlotDword(frame, 0x5C, unchecked(sineMotion + sineDelta));
        return NativeBattleScriptedCommandReads.Advance(frame, 10);

        short Operand(uint offset) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, offset, 2));

        int Random(short operand)
        {
            NativeBattleWaterVectorRandom result = NativeBattleWaterVectorCommands.RandomB65740(operand, frame.Random);
            frame = frame with { Random = result.Random };
            return result.Value;
        }
    }
}
