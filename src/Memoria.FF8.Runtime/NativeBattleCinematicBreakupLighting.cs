using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicBreakupLighting
{
    public static NativeBattleVector FaceNormal(NativeBattleVector first, NativeBattleVector second, NativeBattleVector third)
    {
        var cross = NativeBattleRayBomb10Geometry.Cross56BBF0((second.X - first.X, second.Y - first.Y, second.Z - first.Z), (third.X - first.X, third.Y - first.Y, third.Z - first.Z));
        return NativeBattlePoseMatrices.NormalizeLong56BD20(cross.X, cross.Y, cross.Z);
    }

    public static NativeBattleScriptedColorRegisters Shade(NativeBattleScriptedLightingMemory lighting, ReadOnlySpan<byte> packedNormal, uint material, NativeBattleScriptedColorRegisters registers)
    {
        if (packedNormal.Length != 8)
        {
            throw new InvalidDataException("Native breakup lighting requires its complete packed face normal.");
        }
        NativeBattleVector normal = new(BinaryPrimitives.ReadInt16LittleEndian(packedNormal), BinaryPrimitives.ReadInt16LittleEndian(packedNormal[2..]), BinaryPrimitives.ReadInt16LittleEndian(packedNormal[4..]));
        registers = registers with { NormalInput = System.Collections.Immutable.ImmutableArray.CreateRange(packedNormal.ToArray()) };
        return NativeBattleScriptedNormalColor.Transform4601B0(lighting, normal, material, registers) with { Ir0 = registers.Ir0 };
    }
}
