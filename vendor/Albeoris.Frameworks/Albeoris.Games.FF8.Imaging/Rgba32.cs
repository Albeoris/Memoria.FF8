using System.Runtime.InteropServices;

namespace Albeoris.Games.FF8.Imaging;

/// <summary>An eight-bit-per-channel color independent of a graphics API.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct Rgba32(Byte R, Byte G, Byte B, Byte A = Byte.MaxValue)
{
    /// <summary>Decodes a PlayStation BGR555 color and its semi-transparency bit.</summary>
    public static Rgba32 FromPsxBgr555(UInt16 value)
    {
        if (value == 0)
            return default;

        return new Rgba32(
            Expand5Bit(value),
            Expand5Bit(value >> 5),
            Expand5Bit(value >> 10),
            (value & 0x8000) == 0x8000 ? (Byte)128 : Byte.MaxValue);
    }

    /// <summary>Decodes a PlayStation color for display while leaving special transparency processing to the renderer.</summary>
    public static Rgba32 FromPsxBgr555Display(UInt16 value)
    {
        if (value == 0)
            return default;
        Rgba32 color = FromPsxBgr555(value);
        return new Rgba32(color.R, color.G, color.B);
    }

    /// <summary>Encodes a color as PlayStation BGR555. Alpha must express transparent, semi-transparent, or opaque.</summary>
    public UInt16 ToPsxBgr555()
    {
        if (A == 0)
            return 0;
        if (A is not (128 or Byte.MaxValue))
            throw new InvalidDataException($"PlayStation BGR555 cannot represent alpha {A}; use 0, 128, or 255.");

        UInt16 result = (UInt16)(Compress5Bit(R) | Compress5Bit(G) << 5 | Compress5Bit(B) << 10);
        if (A == 128)
            result |= 0x8000;
        return result;
    }

    private static Byte Expand5Bit(Int32 value)
    {
        Int32 component = value & 0x1F;
        return (Byte)((component << 3) | (component >> 2));
    }

    private static Int32 Compress5Bit(Byte value) => (value * 31 + 127) / Byte.MaxValue;
}
