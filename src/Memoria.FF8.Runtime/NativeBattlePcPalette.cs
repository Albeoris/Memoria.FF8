namespace Memoria.FF8.Runtime;

/// <summary>467370's PC indexed-CLUT expansion to the original BGRA DWORD, including its two black entries.</summary>
public static class NativeBattlePcPalette
{
    /// <summary>467470's alternate black-RGB palette for the D3D alpha-palette-capable profile (1CCFD80=1).</summary>
    public static uint Decode467470(ushort color, byte shift)
    {
        if (shift > 1)
        {
            throw new NotSupportedException("Native467470 requires the observed0/1 producer of retained DWORD1CA89E8.");
        }
        int brightness = (color & 31) + (color >> 5 & 31) + (color >> 10 & 31);
        uint alpha = (uint)Math.Min(255, brightness << (1 - shift));
        return alpha << 24;
    }

    public static uint Decode467370(ushort color)
    {
        if (color == 0)
        {
            return 0x00000001;
        }
        if (color == 0x8000)
        {
            return 0x7F000008;
        }
        uint red = (uint)(color & 31) * 255 / 31;
        uint green = (uint)(color >> 5 & 31) * 255 / 31;
        uint blue = (uint)(color >> 10 & 31) * 255 / 31;
        return 0x7F000000 | red << 16 | green << 8 | blue;
    }
}
