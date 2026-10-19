using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScriptedMaterialStep(NativeBattleWaterExecutionFrame Frame, NativeBattleScriptedLightingMemory Lighting, NativeBattleScriptedColorRegisters Registers);

/// <summary>B29450's actual packed rank-one controls and fixed normal input for tinted mesh materials.</summary>
public static class NativeBattleScriptedMaterial
{
    public static NativeBattleScriptedMaterialStep Prepare(NativeBattleWaterExecutionFrame frame, NativeBattleScriptedColorRegisters registers)
    {
        uint material = frame.ReadTemporaryDword(0x84);
        frame = NativeBattleWaterMeshColor.PrepareA9E8A0(frame).Frame;
        NativeBattleScriptedLightingMatrix light = Matrix([0x1000, 0x10000000, 0, 0x1000, 0]);
        NativeBattleScriptedLightingMatrix color = Matrix([(material & 0xFF) << 5, (material & 0xFF00) << 13, 0, (material >> 11) & 0x1FE0, 0]);
        byte[] normal = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(normal, 0x10001000);
        BinaryPrimitives.WriteUInt32LittleEndian(normal.AsSpan(4), 0x1000);
        return new(frame, new(light, color, new(0, 0, 0)), registers with { NormalInput = normal.ToImmutableArray() });
    }

    private static NativeBattleScriptedLightingMatrix Matrix(uint[] words)
    {
        byte[] bytes = new byte[20];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        return new(bytes.ToImmutableArray(), Enumerable.Repeat(true, 20).ToImmutableArray());
    }
}
