using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The identical8D57C0/89E6E0/89B0D0 source-count XYZ morph; fourth WORDs and all mesh records remain current.</summary>
public static class NativeBattleEnhancementMorph
{
    public static void Validate(ImmutableArray<byte> bytes)
    {
        if (bytes.IsDefault || bytes.Length < 8)
        {
            throw new InvalidDataException("Native enhancement morph requires its current destination mesh bytes.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(4));
        if (count < 0 || count > 4096 || BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan()) != 8 + count * 8 || 8L + count * 8L > bytes.Length)
        {
            throw new InvalidDataException("Native enhancement morph vertex directory exceeds its current destination.");
        }
    }

    public static ImmutableArray<byte> Morph(ImmutableArray<byte> current, ImmutableArray<byte> first, ImmutableArray<byte> second, short ratio)
    {
        if (first.IsDefault || first.Length < 8)
        {
            throw new InvalidDataException("Native enhancement morph requires its original first shape count.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(first.AsSpan().Slice(4));
        if (count <= 0)
        {
            return current;
        }
        Validate(current);
        return NativeBattleGfMorphGeometry.Morph45E9D0(first, 8, second, 8, count, ratio, current, 8);
    }
}
