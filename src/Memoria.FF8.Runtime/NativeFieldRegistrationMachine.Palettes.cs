using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeFieldRegistrationMachine
{
    /// <summary>Projects the original 256-word TIM palette through the selected field PCB entry.</summary>
    public static byte[] ProjectPalette532A40(ReadOnlySpan<byte> source, ReadOnlySpan<byte> colors, ReadOnlySpan<byte> sourceName)
    {
        if (source.Length != 512 || colors.Length < 8 || sourceName.Length < 4)
        {
            throw new InvalidDataException("Native field palette projection requires its complete TIM palette, PCB and original model name.");
        }
        int color = 0;
        for (int current = 0; current < Math.Min(colors.Length / 8, 64); current++)
        {
            if (sourceName.Slice(0, 4).SequenceEqual(colors.Slice(current * 8, 4)) && colors[current * 8 + 4] == 0)
            {
                color = current * 8;
                break;
            }
        }
        byte[] palette = source.ToArray();
        for (int word = 0; word < 256; word++)
        {
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(palette.AsSpan(word * 2, 2));
            if (value != 0)
            {
                int red = Math.Clamp((value & 31) + (colors[color + 5] - 128) / 8, 1, 31);
                int green = Math.Clamp((value >> 5 & 31) + (colors[color + 6] - 128) / 8, 1, 31);
                int blue = Math.Clamp((value >> 10 & 31) + (colors[color + 7] - 128) / 8, 1, 31);
                BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(word * 2, 2), (ushort)(0x8000 | red | green << 5 | blue << 10));
            }
        }
        return palette;
    }
}
