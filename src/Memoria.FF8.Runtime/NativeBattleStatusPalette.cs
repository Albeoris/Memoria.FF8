using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>509E00's512-word body palette projection; this reads the saved palette, never the currently tinted VRAM.</summary>
public static class NativeBattleStatusPalette
{
    public static ImmutableArray<byte> Transform509E00(ReadOnlySpan<byte> original, uint flags)
    {
        if (original.Length != 1024)
        {
            throw new InvalidDataException("Native509E00 requires both original256-color body CLUTs.");
        }
        byte[] result = original.ToArray();
        if ((flags & 0x410) == 0)
        {
            return result.ToImmutableArray();
        }
        for (int index = 0; index < 512; index++)
        {
            ushort color = BinaryPrimitives.ReadUInt16LittleEndian(original.Slice(index * 2));
            int red = color & 31;
            int green = color >> 5 & 31;
            int blue = color >> 10 & 31;
            ushort transformed;
            if ((flags & 0x10) != 0)
            {
                int level = PetrifyLevel(red, green, blue);
                transformed = unchecked((ushort)(level | 32 * (level | 32 * (level | 0xFFE0))));
            }
            else
            {
                int fraction = ((red + 2 * green + blue) << 12) / 124;
                int projectedGreen = 25 * fraction >> 12;
                int projectedBlue = 10 * NativeBattleCameraMath.Sine(fraction / 2) >> 12;
                transformed = unchecked((ushort)(32 * (projectedGreen | 32 * (projectedBlue | 0xFFE0))));
            }
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(index * 2), transformed);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(result, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(512), 0);
        return result.ToImmutableArray();
    }

    public static int PetrifyLevel(int red5, int green5, int blue5) => (red5 + 2 * green5 + blue5) / 5 + 5;
}
