using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicPalette47Result(ImmutableArray<byte> Palette, int AdjustedTargetHue);
public sealed record NativeBattleCinematicPalette47ColorWrite(int ActorIndex, uint GlobalAddress, uint ActorAddress, uint GlobalColor, uint ActorColor);

/// <summary>Original palette capture and per-frame HSV transformation over a retained eight-byte row per color.</summary>
public static class NativeBattleCinematicPalette47
{
    public static ImmutableArray<byte> CaptureB67710(ReadOnlySpan<byte> originalPalette, int count)
    {
        if (count <= 0)
        {
            return [];
        }
        if (originalPalette.Length < checked(count * 2))
        {
            throw new InvalidDataException("Native palette capture requires every selected original color.");
        }
        byte[] rows = new byte[checked(count * 8)];
        for (int index = 0; index < count; index++)
        {
            ushort color = BinaryPrimitives.ReadUInt16LittleEndian(originalPalette.Slice(index * 2, 2));
            int red = color & 31;
            int green = color >> 5 & 31;
            int blue = color >> 10 & 31;
            int high = Math.Max(red, Math.Max(green, blue));
            int delta = high - Math.Min(red, Math.Min(green, blue));
            int hue = 0;
            int saturation = 0;
            if (delta != 0)
            {
                saturation = (delta << 8) / high;
                hue = high == red ? (green - blue) * 128 / delta : high == green ? (blue - red) * 128 / delta + 256 : (red - green) * 128 / delta + 512;
            }
            Span<byte> row = rows.AsSpan(index * 8, 8);
            BinaryPrimitives.WriteInt16LittleEndian(row, checked((short)hue));
            BinaryPrimitives.WriteInt16LittleEndian(row[2..], checked((short)saturation));
            BinaryPrimitives.WriteInt16LittleEndian(row[4..], checked((short)high));
            BinaryPrimitives.WriteUInt16LittleEndian(row[6..], (ushort)(color & 0x8000));
        }
        return rows.ToImmutableArray();
    }

    public static NativeBattleCinematicPalette47Result TransformB67810(ReadOnlySpan<byte> capturedRows, int count, int targetHue, int blendWeight, int saturationAddition, int valueAddition)
    {
        int target = targetHue % 768;
        int weight = Math.Clamp(blendWeight, 0, 256);
        if (count <= 0)
        {
            return new([], target);
        }
        if (capturedRows.Length < checked(count * 8))
        {
            throw new InvalidDataException("Native palette transformation requires its retained original HSV rows.");
        }
        byte[] palette = new byte[checked(count * 2)];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> row = capturedRows.Slice(index * 8, 8);
            int hue = BinaryPrimitives.ReadInt16LittleEndian(row);
            if (target > hue)
            {
                if (target - hue >= 384)
                {
                    hue += 768;
                }
            }
            else if (hue - target >= 384)
            {
                target += 768;
            }
            hue = unchecked(hue + (target - hue) * weight / 256);
            if (hue >= 768)
            {
                hue -= 768;
            }
            else if (hue < 0)
            {
                hue += 768;
            }
            int saturation = Math.Clamp(unchecked(BinaryPrimitives.ReadInt16LittleEndian(row[2..]) + saturationAddition), 0, 256);
            int value = Math.Clamp(unchecked(BinaryPrimitives.ReadInt16LittleEndian(row[4..]) + valueAddition), 0, 32);
            int sector = hue / 128;
            int fraction = hue % 128;
            int p = (256 - saturation) * value / 256;
            int q = (32768 - fraction * saturation) * value / 32768;
            int t = (32768 - (128 - fraction) * saturation) * value / 32768;
            (int red, int green, int blue) = sector switch
            {
                0 => (value, t, p),
                1 => (q, value, p),
                2 => (p, value, t),
                3 => (p, q, value),
                4 => (t, p, value),
                5 => (value, p, q),
                _ => throw new NotSupportedException("Native palette HSV sector requires the actual original capture and current transform operands.")
            };
            ushort originalFlag = BinaryPrimitives.ReadUInt16LittleEndian(row[6..]);
            ushort packed = unchecked((ushort)(((blue & 31) << 10) | ((green & 31) << 5) | (red & 31) | originalFlag));
            BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(index * 2), packed);
        }
        return new(palette.ToImmutableArray(), target);
    }

    public static ImmutableArray<NativeBattleCinematicPalette47ColorWrite> ActorColorWrites(ReadOnlySpan<byte> transformedPalette, short columns)
    {
        if (columns <= 0 || transformedPalette.Length < columns * 2)
        {
            throw new InvalidDataException("Native palette actor colors require the selected first-row endpoint color.");
        }
        ushort color = BinaryPrimitives.ReadUInt16LittleEndian(transformedPalette.Slice((columns - 1) * 2, 2));
        byte red = unchecked((byte)(color << 3));
        byte green = unchecked((byte)((color >> 5) << 3));
        byte blue = unchecked((byte)((color >> 10) << 3));
        uint rgb = (uint)(red | green << 8 | blue << 16);
        return [new(3, 0xB8BA00, 0x1D974BC, rgb | 0x03000000, rgb), new(4, 0xB8BA04, 0x1D97558, rgb | 0x04000000, rgb)];
    }
}
