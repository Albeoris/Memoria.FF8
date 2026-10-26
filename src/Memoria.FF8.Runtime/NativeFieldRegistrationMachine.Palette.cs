using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeFieldRegistrationMachine
{
    /// <summary>Projects a native model palette; explicitly absent PCB preserves all source words without color correction.</summary>
    public static byte[] ProjectPalette532A40(ReadOnlySpan<byte> palette, ReadOnlySpan<byte> modelName, NativeFieldModelSource? colors)
    {
        if (palette.Length != 512 || modelName.Length < 4)
        {
            throw new InvalidDataException("Field palette projection requires 256 native words and the model name.");
        }
        byte[] result = palette.ToArray();
        if (colors is null)
        {
            return result;
        }
        colors.Validate();
        ReadOnlySpan<byte> records = colors.Data.AsSpan();
        if (records.Length < 8 || records.Length % 8 != 0)
        {
            throw new InvalidDataException("Present field PCB must contain complete native eight-byte records.");
        }
        int color = 0;
        for (int current = 0; current < Math.Min(records.Length / 8, 64); current++)
        {
            if (modelName[..4].SequenceEqual(records.Slice(current * 8, 4)) && records[current * 8 + 4] == 0)
            {
                color = current * 8;
                break;
            }
        }
        for (int word = 0; word < 256; word++)
        {
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(word * 2, 2));
            if (value != 0)
            {
                int red = Math.Clamp((value & 31) + (records[color + 5] - 128) / 8, 1, 31);
                int green = Math.Clamp((value >> 5 & 31) + (records[color + 6] - 128) / 8, 1, 31);
                int blue = Math.Clamp((value >> 10 & 31) + (records[color + 7] - 128) / 8, 1, 31);
                BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(word * 2, 2), (ushort)(0x8000 | red | green << 5 | blue << 10));
            }
        }
        return result;
    }
}
