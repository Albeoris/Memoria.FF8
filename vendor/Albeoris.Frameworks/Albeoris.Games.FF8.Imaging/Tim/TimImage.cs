using System.Buffers.Binary;

namespace Albeoris.Games.FF8.Imaging.Tim;

/// <summary>The single pixel image stored by a TIM file.</summary>
public sealed class TimImage
{
    internal TimImage(TimPixelMode pixelMode,
        Int16 x,
        Int16 y,
        Int16 widthInWords,
        Int16 height,
        Byte[] pixels,
        TimClut? clut,
        TimFlags flags,
        Int32 declaredBlockSize)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (widthInWords <= 0)
            throw new ArgumentOutOfRangeException(nameof(widthInWords));
        if (height < 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (declaredBlockSize < TimBlockHeader.Size)
            throw new ArgumentOutOfRangeException(nameof(declaredBlockSize));
        if (pixelMode is TimPixelMode.Indexed4 or TimPixelMode.Indexed8 && clut is null)
            throw new ArgumentNullException(nameof(clut), "An indexed TIM image requires a CLUT.");
        if (pixelMode is TimPixelMode.Direct16 or TimPixelMode.Direct24 && clut is not null)
            throw new ArgumentException("A direct-color TIM image cannot use a CLUT.", nameof(clut));
        Boolean flagsHaveClut = (flags & TimFlags.HasClut) == TimFlags.HasClut;
        if (flagsHaveClut != (clut is not null))
            throw new ArgumentException("The TIM flags and CLUT presence disagree.", nameof(flags));

        PixelMode = pixelMode;
        X = x;
        Y = y;
        WidthInWords = widthInWords;
        Height = height;
        Pixels = pixels;
        Clut = clut;
        Flags = flags;
        DeclaredBlockSize = declaredBlockSize;

        _ = PixelWidth;
        if (height > PixelImage.MaximumDimension)
            throw new InvalidDataException($"The TIM image height {height} exceeds the 8K limit.");
    }

    public TimImage(TimPixelMode pixelMode,
        Int16 x,
        Int16 y,
        Int16 widthInWords,
        Int16 height,
        Byte[] pixels,
        TimClut? clut = null,
        TimFlags flags = TimFlags.None)
        : this(
            pixelMode,
            x,
            y,
            widthInWords,
            height,
            pixels,
            clut,
            clut is null ? flags : flags | TimFlags.HasClut,
            checked(TimBlockHeader.Size + widthInWords * sizeof(UInt16) * height))
    {
    }

    public TimPixelMode PixelMode { get; }
    public Int16 X { get; set; }
    public Int16 Y { get; set; }
    public Int16 WidthInWords { get; }
    public Int16 Height { get; }
    public Int32 PixelWidth => GetPixelWidth(PixelMode, WidthInWords);

    /// <summary>Expanded indices for indexed images; native bytes for direct-color images.</summary>
    public Byte[] Pixels { get; }

    public TimClut? Clut { get; }
    public TimFlags Flags { get; }
    public IReadOnlyList<TimPalette> Palettes => Clut?.GetPalettes(PixelMode) ?? [];
    public Int32 DeclaredBlockSize { get; }
    public Boolean IsEmpty => Height == 0;
    public Int32 FilePixelByteCount => checked(WidthInWords * sizeof(UInt16) * Height);

    public Int32 ExpectedPixelCount => PixelMode == TimPixelMode.Indexed4
        ? checked(FilePixelByteCount * 2)
        : FilePixelByteCount;

    public Boolean CanDecode => !IsEmpty && Pixels.Length == ExpectedPixelCount;

    public PixelImage Decode(Int32 paletteIndex = 0)
    {
        if (IsEmpty)
            throw new InvalidOperationException("An empty TIM image has no pixels to decode.");
        if (!CanDecode)
            throw new InvalidOperationException($"The TIM image contains {Pixels.Length} values, while its dimensions require {ExpectedPixelCount}.");

        TimPalette? palette = null;
        if (PixelMode is TimPixelMode.Indexed4 or TimPixelMode.Indexed8)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(paletteIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(paletteIndex, Palettes.Count);
            palette = Palettes[paletteIndex];
        }

        PixelImage result = new(PixelWidth, Height);
        switch (PixelMode)
        {
            case TimPixelMode.Indexed4:
            case TimPixelMode.Indexed8:
                DecodeIndexed(result, palette!);
                break;
            case TimPixelMode.Direct16:
                DecodeDirect16(result);
                break;
            case TimPixelMode.Direct24:
                DecodeDirect24(result);
                break;
            default:
                throw new InvalidDataException($"Unsupported TIM pixel mode {(Int32)PixelMode}.");
        }

        return result;
    }

    private static Int32 GetPixelWidth(TimPixelMode mode, Int16 widthInWords)
    {
        Int32 rowBytes = checked(widthInWords * sizeof(UInt16));
        Int32 width = mode switch
        {
            TimPixelMode.Indexed4 => rowBytes * 2,
            TimPixelMode.Indexed8 => rowBytes,
            TimPixelMode.Direct16 => widthInWords,
            TimPixelMode.Direct24 when rowBytes % 3 == 0 => rowBytes / 3,
            TimPixelMode.Direct24 => throw new InvalidDataException(
                $"A 24-bit TIM row contains {rowBytes} bytes, which is not divisible by three."),
            _ => throw new InvalidDataException($"Unsupported TIM pixel mode {(Int32)mode}."),
        };

        if (width > PixelImage.MaximumDimension)
            throw new InvalidDataException($"The TIM image width {width} exceeds the 8K limit.");
        return width;
    }

    private void DecodeIndexed(PixelImage result, TimPalette palette)
    {
        for (Int32 index = 0; index < Pixels.Length; index++)
        {
            Byte colorIndex = Pixels[index];
            if (colorIndex >= palette.Count)
            {
                throw new InvalidDataException(
                    $"TIM pixel {index} uses palette index {colorIndex}, but the palette has {palette.Count} colors.");
            }

            result.Pixels[index] = palette[colorIndex];
        }
    }

    private void DecodeDirect16(PixelImage result)
    {
        for (Int32 index = 0; index < result.Pixels.Length; index++)
        {
            UInt16 value = BinaryPrimitives.ReadUInt16LittleEndian(Pixels.AsSpan(index * sizeof(UInt16)));
            result.Pixels[index] = Rgba32.FromPsxBgr555(value);
        }
    }

    private void DecodeDirect24(PixelImage result)
    {
        for (Int32 index = 0; index < result.Pixels.Length; index++)
        {
            Int32 position = index * 3;
            result.Pixels[index] = new Rgba32(Pixels[position + 2], Pixels[position + 1], Pixels[position]);
        }
    }
}