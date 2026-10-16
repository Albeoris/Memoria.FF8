namespace Memoria.FF8.Runtime;

/// <summary>Explicit selected-device direct16 format; an indexed palette profile does not determine this value.</summary>
public enum NativeBattlePcDirectTextureFormat
{
    Rgb555 = 1,
    Rgb565 = 2
}

/// <summary>4675C0 depth2 conversion followed by418DEC's non-paletted, alpha-free device-format expansion.</summary>
public static class NativeBattlePcDirectTexture
{
    public static ushort Convert4675C0(ushort vram, NativeBattlePcDirectTextureFormat format)
    {
        int red = vram & 31;
        int green = vram >> 5 & 31;
        int blue = vram >> 10 & 31;
        return format switch
        {
            NativeBattlePcDirectTextureFormat.Rgb555 => (ushort)(red << 10 | green << 5 | blue),
            NativeBattlePcDirectTextureFormat.Rgb565 => (ushort)(red << 11 | green << 6 | blue),
            _ => throw new NotSupportedException("Native4675C0 requires an explicit observed RGB555 or RGB565 device-format selection.")
        };
    }

    public static uint Expand418DEC(ushort devicePixel, NativeBattlePcDirectTextureFormat format)
    {
        uint red;
        uint green;
        uint blue = (uint)(devicePixel & 31) * 255 / 31;
        switch (format)
        {
            case NativeBattlePcDirectTextureFormat.Rgb555:
                red = (uint)(devicePixel >> 10 & 31) * 255 / 31;
                green = (uint)(devicePixel >> 5 & 31) * 255 / 31;
                break;
            case NativeBattlePcDirectTextureFormat.Rgb565:
                red = (uint)(devicePixel >> 11 & 31) * 255 / 31;
                green = (uint)(devicePixel >> 5 & 63) * 255 / 63;
                break;
            default:
                throw new NotSupportedException("Native418DEC requires its explicit direct16 device masks; no indexed-profile inference is valid.");
        }
        return 0xFF000000 | red << 16 | green << 8 | blue;
    }

    public static uint DecodeArgb(ushort vram, NativeBattlePcDirectTextureFormat format) => Expand418DEC(Convert4675C0(vram, format), format);
}
