using Albeoris.Games.FF8.Imaging.Tex;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class TexHeaderSettings
{
    public TexVersion Version { get; init; }
    public Int32 Unknown01 { get; init; }
    public Int32? ColorKeyIndex { get; init; }
    public Int32 Unknown03 { get; init; }
    public Int32 Unknown04 { get; init; }
    public Int32 MinimumBitsPerColor { get; init; }
    public Int32 MaximumBitsPerColor { get; init; }
    public Int32 MinimumAlphaBits { get; init; }
    public Int32 MaximumAlphaBits { get; init; }
    public Int32 MinimumBitsPerPixel { get; init; }
    public Int32 MaximumBitsPerPixel { get; init; }
    public Int32 Unknown11 { get; init; }
    public Int32 BitDepth { get; init; }
    public Int32 Pitch { get; init; }
    public Int32 Unknown18 { get; init; }
    public TexPaletteFlags PaletteFlags { get; init; }
    public TexConversionFlags ConversionFlags { get; init; }
    public TexColorKeyArrayFlags ColorKeyArrayFlags { get; init; }
    public Int32 RuntimeData24 { get; init; }
    public TexDirectPixelFormat? DirectPixelFormat { get; init; }
    public Int32 RuntimeData48 { get; init; }
    public Int32 ReferenceAlpha { get; init; }
    public Int32 RuntimeData50 { get; init; }
    public Int32 Unknown51 { get; init; }
    public Int32 RuntimePaletteIndex { get; init; }
    public Int32 RuntimeData53 { get; init; }
    public Int32 RuntimeData54 { get; init; }
    public Int32 Unknown55 { get; init; }
    public Int32 Unknown56 { get; init; }
    public Int32 Unknown57 { get; init; }
    public Int32 Unknown58 { get; init; }
    public Int32 Unknown59 { get; init; }

    public static TexHeaderSettings From(TexHeader header) => new()
    {
        Version = header.Version,
        Unknown01 = header.Unknown01,
        ColorKeyIndex = header.ColorKeyIndex,
        Unknown03 = header.Unknown03,
        Unknown04 = header.Unknown04,
        MinimumBitsPerColor = header.MinimumBitsPerColor,
        MaximumBitsPerColor = header.MaximumBitsPerColor,
        MinimumAlphaBits = header.MinimumAlphaBits,
        MaximumAlphaBits = header.MaximumAlphaBits,
        MinimumBitsPerPixel = header.MinimumBitsPerPixel,
        MaximumBitsPerPixel = header.MaximumBitsPerPixel,
        Unknown11 = header.Unknown11,
        BitDepth = header.BitDepth,
        Pitch = header.Pitch,
        Unknown18 = header.Unknown18,
        PaletteFlags = header.PaletteFlags,
        ConversionFlags = header.ConversionFlags,
        ColorKeyArrayFlags = header.ColorKeyArrayFlags,
        RuntimeData24 = header.RuntimeData24,
        DirectPixelFormat = (header.PaletteFlags & TexPaletteFlags.HasPalette) != 0
            ? null
            : TexDirectPixelFormat.From(header),
        RuntimeData48 = header.RuntimeData48,
        ReferenceAlpha = header.ReferenceAlpha,
        RuntimeData50 = header.RuntimeData50,
        Unknown51 = header.Unknown51,
        RuntimePaletteIndex = header.RuntimePaletteIndex,
        RuntimeData53 = header.RuntimeData53,
        RuntimeData54 = header.RuntimeData54,
        Unknown55 = header.Unknown55,
        Unknown56 = header.Unknown56,
        Unknown57 = header.Unknown57,
        Unknown58 = header.Unknown58,
        Unknown59 = header.Unknown59,
    };

    public TexHeader Create(Int32 width, Int32 height, Int32 paletteCount, Int32 colorsPerPalette, Boolean hasColorKeys)
    {
        Boolean indexed = paletteCount > 0;
        TexHeader header = new()
        {
            Version = Version,
            Unknown01 = Unknown01,
            ColorKeyIndex = ColorKeyIndex,
            Unknown03 = Unknown03,
            Unknown04 = Unknown04,
            MinimumBitsPerColor = MinimumBitsPerColor,
            MaximumBitsPerColor = MaximumBitsPerColor,
            MinimumAlphaBits = MinimumAlphaBits,
            MaximumAlphaBits = MaximumAlphaBits,
            MinimumBitsPerPixel = MinimumBitsPerPixel,
            MaximumBitsPerPixel = MaximumBitsPerPixel,
            Unknown11 = Unknown11,
            PaletteCount = paletteCount,
            ColorsPerPalette = colorsPerPalette,
            BitDepth = BitDepth,
            Width = width,
            Height = height,
            Pitch = Pitch,
            Unknown18 = Unknown18,
            PaletteFlags = indexed
                ? this.PaletteFlags | TexPaletteFlags.HasPalette
                : this.PaletteFlags & ~TexPaletteFlags.HasPalette,
            BitsPerIndex = indexed ? TexWorkspaceCodec.IndexedBitsPerPixel : 0,
            ConversionFlags = ConversionFlags,
            PaletteSize = checked(paletteCount * colorsPerPalette),
            RepeatedColorsPerPalette = colorsPerPalette,
            RuntimeData24 = RuntimeData24,
            BitsPerPixel = indexed ? TexWorkspaceCodec.IndexedBitsPerPixel : 0,
            BytesPerPixel = indexed ? TexWorkspaceCodec.IndexedBytesPerPixel : 0,
            ColorKeyArrayFlags = hasColorKeys
                ? this.ColorKeyArrayFlags | TexColorKeyArrayFlags.HasColorKeyArray
                : this.ColorKeyArrayFlags & ~TexColorKeyArrayFlags.HasColorKeyArray,
            RuntimeData48 = RuntimeData48,
            ReferenceAlpha = ReferenceAlpha,
            RuntimeData50 = RuntimeData50,
            Unknown51 = Unknown51,
            RuntimePaletteIndex = RuntimePaletteIndex,
            RuntimeData53 = RuntimeData53,
            RuntimeData54 = RuntimeData54,
            Unknown55 = Unknown55,
            Unknown56 = Unknown56,
            Unknown57 = Unknown57,
            Unknown58 = Unknown58,
            Unknown59 = Unknown59,
        };

        if (!indexed)
        {
            TexDirectPixelFormat pixelFormat = DirectPixelFormat ?? throw new InvalidDataException("A TEX workspace without palettes requires a directPixelFormat in its manifest.");
            pixelFormat.Apply(ref header);
        }
        return header;
    }
}
