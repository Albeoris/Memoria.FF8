using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleLightBackTransform
{
    public static NativeBattleEffectMemory LoadBone(NativeBattleEffectMemory memory, NativeBattlePackedMatrix32 bone)
    {
        bone.Validate();
        NativeBattleScriptedLightingMemory lighting = memory.ScriptedLighting ?? NativeBattleScriptedLightingMemory.Unknown();
        NativeBattleScriptedBackColor back = lighting.BackColor ?? NativeBattleScriptedBackColor.Unknown();
        for (int row = 0; row < 3; row++)
        {
            int offset = 20 + row * 4;
            if (!bone.Known.AsSpan().Slice(offset, 4).ToArray().All(value => value))
            {
                back = back with { Known = (back.Known ?? [true, true, true]).SetItem(row, false) };
                continue;
            }
            back = back.WriteLane(row, BinaryPrimitives.ReadInt32LittleEndian(bone.Bytes.AsSpan().Slice(offset, 4)));
        }
        return memory with { ScriptedLighting = lighting with { LightMatrix = new(bone.Bytes.Take(20).ToImmutableArray(), bone.Known.Take(20).ToImmutableArray()), BackColor = back } };
    }

    public static NativeBattleEffectMemory Transform(NativeBattleEffectMemory memory, int slot)
    {
        if ((uint)slot >= 3)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        NativeBattleScriptedLightingMemory lighting = memory.ScriptedLighting ?? NativeBattleScriptedLightingMemory.Unknown();
        NativeBattleGeometryRegisters geometry = memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown();
        NativeBattleScriptedColorRegisters colors = memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown();
        lighting.Validate();
        geometry.Validate();
        colors.Validate();
        ImmutableArray<byte?> vector = slot switch
        {
            0 => colors.NormalInput is { } input ? input.Select((value, index) => colors.NormalInputKnown is { } known && !known[index] ? (byte?)null : value).ToImmutableArray() : new byte?[8].ToImmutableArray(),
            1 => geometry.V1,
            _ => geometry.V2
        };
        short?[] components = [Word(vector, 0), Word(vector, 2), Word(vector, 4)];
        int?[] mac = new int?[3];
        int?[] ir = new int?[3];
        uint flags = 0;
        uint[] saturation = [0x81000000, 0x80800000, 0x00400000];
        for (int row = 0; row < 3; row++)
        {
            double?[] products = new double?[3];
            for (int column = 0; column < 3; column++)
            {
                int offset = (row * 3 + column) * 2;
                short? coefficient = lighting.LightMatrix is { } matrix && matrix.Known[offset] && matrix.Known[offset + 1] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(offset, 2)) : null;
                short? component = components[column];
                products[column] = coefficient == 0 || component == 0 ? 0 : coefficient is { } a && component is { } b ? a * (b * (1d / 4096d)) : null;
            }
            int? translation = lighting.BackColor?.ReadLane(row);
            if (translation is not { } add || products.Any(value => !value.HasValue))
            {
                continue;
            }
            int value = unchecked(NativePrecision53.RoundInteger(products[2]!.Value + products[1]!.Value + products[0]!.Value) + add);
            mac[row] = value;
            ir[row] = Math.Clamp(value, short.MinValue, short.MaxValue);
            if (value is < short.MinValue or > short.MaxValue)
            {
                flags |= saturation[row];
            }
        }
        return memory with { GeometryRegisters = geometry, ScriptedColorRegisters = colors with { Mac = mac.ToImmutableArray(), Ir = ir.ToImmutableArray(), Flags = mac.All(value => value.HasValue) ? flags : null } };
    }

    private static short? Word(ImmutableArray<byte?> bytes, int offset)
    {
        return bytes[offset] is { } low && bytes[offset + 1] is { } high ? unchecked((short)(low | high << 8)) : null;
    }
}
