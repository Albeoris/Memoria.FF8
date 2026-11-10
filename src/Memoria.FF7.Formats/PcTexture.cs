namespace Memoria.FF7.Formats;

public sealed record PcTexture(int Width, int Height, int BytesPerPixel, IReadOnlyList<IReadOnlyList<uint>> Palettes, ReadOnlyMemory<byte> Pixels, ReadOnlyMemory<byte> Header)
{
    public PcTextureRenderInfo RenderInfo => PcTextureRenderInfo.Read(Header.Span);
    public ReadOnlyMemory<byte> TrailingData { get; init; }
    public static PcTexture Read(ReadOnlySpan<byte> source, int maximumPixels = 16 * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumPixels);
        BinaryCursor cursor = new(source);
        uint version = cursor.U32();
        if (version != 1)
        {
            throw new InvalidDataException($"Unsupported FF7 TEX version {version}.");
        }
        cursor.Seek(0x30);
        int paletteCount = cursor.I32();
        int colors = cursor.I32();
        cursor.Seek(0x3C);
        int width = cursor.I32();
        int height = cursor.I32();
        cursor.Seek(0x58);
        int paletteEntries = cursor.I32();
        cursor.Seek(0x68);
        int bpp = cursor.I32();
        if (width <= 0 || height <= 0 || (long)width * height > maximumPixels || bpp is not (1 or 2 or 3 or 4) || paletteCount < 0 || colors < 0 || paletteEntries < 0 || (paletteCount > 0 && colors == 0) || 0xECL + 4L * paletteEntries + (long)width * height * bpp > source.Length)
        {
            throw new InvalidDataException("FF7 TEX dimensions, palettes or pixel bounds are invalid.");
        }
        // The native entry count sizes the stored CLUT. Battle textures can advertise more palettes than they store.
        if ((long)paletteCount * colors > paletteEntries)
        {
            if (paletteEntries < colors || paletteEntries % colors != 0)
            {
                throw new InvalidDataException("FF7 TEX palette entries do not form complete palettes.");
            }
            paletteCount = paletteEntries / colors;
        }
        cursor.Seek(0xEC);
        IReadOnlyList<uint>[] palettes = new IReadOnlyList<uint>[paletteCount];
        for (int palette = 0; palette < palettes.Length; palette++)
        {
            uint[] entries = new uint[colors];
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] = cursor.U32();
            }
            palettes[palette] = Array.AsReadOnly(entries);
        }
        cursor.Seek(0xEC + paletteEntries * 4);
        byte[] pixels = cursor.Take(checked(width * height * bpp)).ToArray();
        return new PcTexture(width, height, bpp, Array.AsReadOnly(palettes), pixels, source[..0xEC].ToArray()) { TrailingData = cursor.Take(cursor.Remaining).ToArray() };
    }
}

public sealed record PcTextureRenderInfo(uint ColorKey, uint UsePalette, uint BitsPerIndex, uint UsePaletteColorKey, uint ReferenceAlpha, uint BlendMode, uint PaletteIndex)
{
    public static PcTextureRenderInfo Read(ReadOnlySpan<byte> header)
    {
        BinaryCursor cursor = new(header);
        cursor.Seek(8);
        uint key = cursor.U32();
        cursor.Seek(0x4C);
        uint palette = cursor.U32();
        uint bits = cursor.U32();
        cursor.Seek(0xBC);
        uint paletteKey = cursor.U32();
        cursor.Skip(4);
        uint alpha = cursor.U32();
        uint blend = cursor.U32();
        cursor.Skip(4);
        return new PcTextureRenderInfo(key, palette, bits, paletteKey, alpha, blend, cursor.U32());
    }
}
