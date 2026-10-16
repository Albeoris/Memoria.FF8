using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Albeoris.Games.FF8.Imaging.Tex;

/// <summary>An FF8 TEX texture with optional BGRA palettes.</summary>
public sealed class TexFile
{
    private const Int32 BitsPerByte = 8;
    private const Int32 IndexedBytesPerPixel = 1;
    private const Int32 MaximumPaletteColorCount = Byte.MaxValue + 1;
    private const Byte ReferenceAlphaMarker = 254;

    public TexFile(
        TexHeader header,
        IEnumerable<UInt32[]> palettes,
        Byte[] pixels,
        Byte[]? colorKeys = null)
    {
        ArgumentNullException.ThrowIfNull(palettes);
        ArgumentNullException.ThrowIfNull(pixels);
        Header = header;
        Palettes.AddRange(palettes);
        Pixels = pixels;
        ColorKeys = colorKeys;
        Validate();
    }

    public TexHeader Header { get; }
    public List<UInt32[]> Palettes { get; } = [];
    public Byte[] Pixels { get; }
    public Byte[]? ColorKeys { get; }

    public static TexFile Read(ReadOnlySpan<Byte> content)
    {
        TexHeader header = TexHeader.Read(content);
        ValidateHeader(header);
        Int32 paletteByteCount = checked(header.PaletteCount * header.ColorsPerPalette * sizeof(UInt32));
        Int32 pixelByteCount = checked(header.Width * header.Height * header.BytesPerPixel);
        Int32 colorKeyByteCount = HasColorKeyArray(header) ? header.PaletteCount : 0;
        Int32 expectedByteCount = checked(TexHeader.Size + paletteByteCount + pixelByteCount + colorKeyByteCount);
        if (content.Length != expectedByteCount)
            throw new InvalidDataException($"The TEX contains {content.Length} bytes instead of {expectedByteCount}.");

        ReadOnlySpan<Byte> remaining = content[TexHeader.Size..];
        List<UInt32[]> palettes = new(header.PaletteCount);
        for (Int32 paletteIndex = 0; paletteIndex < header.PaletteCount; paletteIndex++)
        {
            Int32 byteCount = checked(header.ColorsPerPalette * sizeof(UInt32));
            palettes.Add(MemoryMarshal.Cast<Byte, UInt32>(remaining[..byteCount]).ToArray());
            remaining = remaining[byteCount..];
        }

        Byte[] pixels = remaining[..pixelByteCount].ToArray();
        remaining = remaining[pixelByteCount..];
        Byte[]? colorKeys = HasColorKeyArray(header)
            ? remaining.ToArray()
            : null;
        return new TexFile(header, palettes, pixels, colorKeys);
    }

    public Byte[] Write()
    {
        Validate();
        Int32 paletteByteCount = checked(Header.PaletteCount * Header.ColorsPerPalette * sizeof(UInt32));
        Int32 colorKeyByteCount = ColorKeys?.Length ?? 0;
        Byte[] result = new Byte[checked(TexHeader.Size + paletteByteCount + Pixels.Length + colorKeyByteCount)];
        Span<Byte> remaining = result;
        Header.Write(remaining);
        remaining = remaining[TexHeader.Size..];
        foreach (UInt32[] palette in Palettes)
        {
            ReadOnlySpan<Byte> paletteBytes = MemoryMarshal.AsBytes(palette.AsSpan());
            paletteBytes.CopyTo(remaining);
            remaining = remaining[paletteBytes.Length..];
        }
        Pixels.CopyTo(remaining);
        remaining = remaining[Pixels.Length..];
        if (ColorKeys is not null)
            ColorKeys.CopyTo(remaining);
        return result;
    }

    /// <summary>Decodes the texture entirely in managed memory.</summary>
    public PixelImage Decode(Int32 paletteIndex = 0) => Decode(paletteIndex, applyRuntimeTransparency: true);

    internal PixelImage DecodeRaw(Int32 paletteIndex = 0) => Decode(paletteIndex, applyRuntimeTransparency: false);

    private PixelImage Decode(Int32 paletteIndex, Boolean applyRuntimeTransparency)
    {
        PixelImage image = new(Header.Width, Header.Height);
        if (Palettes.Count > 0)
        {
            Rgba32[] palette = applyRuntimeTransparency
                ? GetEffectivePalette(paletteIndex)
                : GetRawPalette(paletteIndex);
            for (Int32 index = 0; index < image.Pixels.Length; index++)
            {
                Int32 colorIndex = Pixels[index];
                if (colorIndex >= palette.Length)
                {
                    throw new InvalidDataException(
                        $"TEX pixel {index} uses palette index {colorIndex}, but the palette has {palette.Length} colors.");
                }
                image.Pixels[index] = palette[colorIndex];
            }
            return image;
        }

        for (Int32 index = 0; index < image.Pixels.Length; index++)
        {
            UInt32 value = ReadPixel(Pixels.AsSpan(index * Header.BytesPerPixel), Header.BytesPerPixel);
            Rgba32 color = new(
                Extract(value, Header.RedMask, Header.RedShift, Header.RedMaximum, Header.RedBits, 0),
                Extract(value, Header.GreenMask, Header.GreenShift, Header.GreenMaximum, Header.GreenBits, 0),
                Extract(value, Header.BlueMask, Header.BlueShift, Header.BlueMaximum, Header.BlueBits, 0),
                Extract(value, Header.AlphaMask, Header.AlphaShift, Header.AlphaMaximum, Header.AlphaBits, Byte.MaxValue));
            image.Pixels[index] = applyRuntimeTransparency && Header.ColorKeyIndex == value
                ? color with { A = 0 }
                : color;
        }
        return image;
    }

    internal Rgba32[] GetRawPalette(Int32 paletteIndex)
    {
        ValidatePaletteIndex(paletteIndex);
        UInt32[] source = Palettes[paletteIndex];
        Rgba32[] result = new Rgba32[source.Length];
        for (Int32 index = 0; index < source.Length; index++)
            result[index] = DecodeBgra(source[index]);
        return result;
    }

    internal Rgba32[] GetEffectivePalette(Int32 paletteIndex)
    {
        Rgba32[] result = GetRawPalette(paletteIndex);
        for (Int32 index = 0; index < result.Length; index++)
        {
            if (result[index].A == ReferenceAlphaMarker)
                result[index] = result[index] with { A = checked((Byte)Header.ReferenceAlpha) };
        }

        Int32? colorKeyIndex = GetColorKeyIndex(paletteIndex);
        if (colorKeyIndex is Int32 colorKey)
        {
            if (colorKey >= result.Length)
            {
                throw new InvalidDataException(
                    $"TEX palette {paletteIndex} selects color key {colorKey}, but contains {result.Length} colors.");
            }
            result[colorKey] = result[colorKey] with { A = 0 };
        }
        return result;
    }

    internal static UInt32 EncodeBgra(Rgba32 color) =>
        color.B | (UInt32)color.G << 8 | (UInt32)color.R << 16 | (UInt32)color.A << 24;

    private static Rgba32 DecodeBgra(UInt32 value) => new(
        (Byte)(value >> 16),
        (Byte)(value >> 8),
        (Byte)value,
        (Byte)(value >> 24));

    private Int32? GetColorKeyIndex(Int32 paletteIndex)
    {
        if (Header.ColorKeyIndex is null)
            return null;
        if (ColorKeys is null)
            return Header.ColorKeyIndex;

        Byte value = ColorKeys[paletteIndex];
        return value == 0 ? null : value - 1;
    }

    private void ValidatePaletteIndex(Int32 paletteIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(paletteIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(paletteIndex, Palettes.Count);
    }

    private void Validate()
    {
        ValidateHeader(Header);
        if (Palettes.Count != Header.PaletteCount)
            throw new InvalidDataException($"The TEX header declares {Header.PaletteCount} palettes, but {Palettes.Count} are present.");
        for (Int32 index = 0; index < Palettes.Count; index++)
        {
            if (Palettes[index].Length != Header.ColorsPerPalette)
            {
                throw new InvalidDataException(
                    $"TEX palette {index} has {Palettes[index].Length} colors instead of {Header.ColorsPerPalette}.");
            }
        }

        Int32 expectedLength = checked(Header.Width * Header.Height * Header.BytesPerPixel);
        if (Pixels.Length != expectedLength)
            throw new InvalidDataException($"The TEX contains {Pixels.Length} pixel bytes instead of {expectedLength}.");
        if (HasColorKeyArray(Header) != (ColorKeys is not null))
            throw new InvalidDataException("The TEX color-key array flag and data disagree.");
        if (ColorKeys is not null && ColorKeys.Length != Header.PaletteCount)
        {
            throw new InvalidDataException(
                $"The TEX has {ColorKeys.Length} palette color keys instead of {Header.PaletteCount}.");
        }
    }

    private static void ValidateHeader(TexHeader header)
    {
        Boolean hasPalette = HasPalette(header);
        Boolean hasColorKeyArray = HasColorKeyArray(header);

        if (header.Version != TexVersion.FinalFantasy8)
            throw new InvalidDataException($"Unsupported TEX version {(Int32)header.Version}; FF8 uses version 2.");
        if (header.Width is < 1 or > PixelImage.MaximumDimension)
            throw new InvalidDataException($"The TEX width {header.Width} is outside the supported 1..8192 range.");
        if (header.Height is < 1 or > PixelImage.MaximumDimension)
            throw new InvalidDataException($"The TEX height {header.Height} is outside the supported 1..8192 range.");
        if (header.BytesPerPixel is < 1 or > sizeof(UInt32))
            throw new InvalidDataException($"Unsupported TEX byte depth {header.BytesPerPixel}.");
        if (header.BitsPerPixel != checked(header.BytesPerPixel * BitsPerByte))
            throw new InvalidDataException($"TEX declares {header.BitsPerPixel} bits but {header.BytesPerPixel} bytes per pixel.");
        if (header.PaletteCount < 0 || header.ColorsPerPalette < 0)
            throw new InvalidDataException("TEX palette dimensions cannot be negative.");
        if (hasPalette != (header.PaletteCount > 0))
            throw new InvalidDataException("TEX palette presence and palette count disagree.");
        if (header.PaletteCount > 0 && header.RepeatedColorsPerPalette != header.ColorsPerPalette)
            throw new InvalidDataException("The two TEX colors-per-palette fields disagree.");
        if (header.PaletteCount > 0
            && header.PaletteSize != checked(header.PaletteCount * header.ColorsPerPalette))
        {
            throw new InvalidDataException("The TEX palette size does not match its dimensions.");
        }
        if (hasPalette && header.BytesPerPixel != IndexedBytesPerPixel)
            throw new InvalidDataException("An indexed TEX must store one palette index per byte.");
        if (hasPalette && (header.BitsPerPixel != BitsPerByte || header.BitsPerIndex != BitsPerByte))
            throw new InvalidDataException("An indexed TEX must store one eight-bit palette index per pixel.");
        if (hasPalette && header.ColorsPerPalette is < 1 or > MaximumPaletteColorCount)
            throw new InvalidDataException("An indexed TEX palette must contain 1 through 256 colors.");
        if (hasPalette && HasDirectChannelData(header))
            throw new InvalidDataException("An indexed TEX must not contain a direct-color channel layout.");
        if (!hasPalette
            && (header.PaletteCount != 0
                || header.ColorsPerPalette != 0
                || header.PaletteSize != 0
                || header.RepeatedColorsPerPalette != 0
                || header.BitsPerIndex != 0))
        {
            throw new InvalidDataException("A direct-color TEX must not contain indexed palette dimensions.");
        }
        if (hasColorKeyArray && !hasPalette)
            throw new InvalidDataException("A TEX color-key array requires palettes.");
        if (header.ReferenceAlpha is < Byte.MinValue or > Byte.MaxValue)
            throw new InvalidDataException($"TEX reference alpha {header.ReferenceAlpha} is outside the byte range.");
    }

    private static Boolean HasPalette(TexHeader header) =>
        (header.PaletteFlags & TexPaletteFlags.HasPalette) != 0;

    private static Boolean HasColorKeyArray(TexHeader header) =>
        (header.ColorKeyArrayFlags & TexColorKeyArrayFlags.HasColorKeyArray) != 0;

    private static Boolean HasDirectChannelData(TexHeader header) =>
        header.RedBits != 0
        || header.GreenBits != 0
        || header.BlueBits != 0
        || header.AlphaBits != 0
        || header.RedMask != 0
        || header.GreenMask != 0
        || header.BlueMask != 0
        || header.AlphaMask != 0
        || header.RedShift != 0
        || header.GreenShift != 0
        || header.BlueShift != 0
        || header.AlphaShift != 0
        || header.RedBitsConstant != 0
        || header.GreenBitsConstant != 0
        || header.BlueBitsConstant != 0
        || header.AlphaBitsConstant != 0
        || header.RedMaximum != 0
        || header.GreenMaximum != 0
        || header.BlueMaximum != 0
        || header.AlphaMaximum != 0;

    private static UInt32 ReadPixel(ReadOnlySpan<Byte> source, Int32 byteCount)
    {
        return byteCount switch
        {
            1 => source[0],
            2 => BinaryPrimitives.ReadUInt16LittleEndian(source),
            3 => (UInt32)(source[0] | source[1] << 8 | source[2] << 16),
            4 => BinaryPrimitives.ReadUInt32LittleEndian(source),
            _ => throw new InvalidDataException($"Unsupported TEX byte depth {byteCount}.")
        };
    }

    private static Byte Extract(UInt32 value, Int32 mask, Int32 shift, Int32 maximum, Int32 bits, Byte missing)
    {
        if (bits == 0 || mask == 0)
            return missing;
        UInt32 component = (value & unchecked((UInt32)mask)) >> shift;
        UInt32 max = maximum > 0 ? unchecked((UInt32)maximum) : (1u << bits) - 1;
        return (Byte)((component * Byte.MaxValue + max / 2) / max);
    }
}
