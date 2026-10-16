using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using ImageMagick;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class PngCodec
{
    private const UInt32 CrcPolynomial = 0xEDB88320U;
    private static readonly UInt32[] CrcTable = CreateCrcTable();
    private static ReadOnlySpan<Byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];
    private static ReadOnlySpan<Byte> IhdrChunkType => "IHDR"u8;
    private static ReadOnlySpan<Byte> PlteChunkType => "PLTE"u8;
    private static ReadOnlySpan<Byte> TransparencyChunkType => "tRNS"u8;
    private static ReadOnlySpan<Byte> ImageDataChunkType => "IDAT"u8;
    private static ReadOnlySpan<Byte> ImageEndChunkType => "IEND"u8;

    public static void WriteRgba(Stream output, PixelImage image)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(image);
        ValidateDimensions(image.Width, image.Height);
        output.Write(Signature);

        Span<Byte> header = stackalloc Byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, image.Width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], image.Height);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(output, IhdrChunkType, header);

        ReadOnlySpan<Byte> pixels = MemoryMarshal.AsBytes(image.Pixels.AsSpan());
        Int32 rowLength = checked(image.Width * 4);
        using (MemoryStream compressed = new())
        {
            using (ZLibStream compressor = new(compressed, CompressionLevel.Fastest, leaveOpen: true))
            {
                for (Int32 y = 0; y < image.Height; y++)
                {
                    compressor.WriteByte(0);
                    compressor.Write(pixels.Slice(y * rowLength, rowLength));
                }
            }
            WriteChunk(output, ImageDataChunkType, compressed.GetBuffer().AsSpan(0, checked((Int32)compressed.Length)));
        }
        WriteChunk(output, ImageEndChunkType, []);
    }

    public static void WriteIndexed(
        Stream output,
        Int32 width,
        Int32 height,
        ReadOnlySpan<Byte> indices,
        IReadOnlyList<Rgba32> palette)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(palette);
        ValidateDimensions(width, height);
        if (indices.Length != checked(width * height))
            throw new ArgumentException("The index count does not match the PNG dimensions.", nameof(indices));
        if (palette.Count is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(palette), "An indexed PNG palette must contain 1 through 256 colors.");

        for (Int32 index = 0; index < indices.Length; index++)
        {
            if (indices[index] >= palette.Count)
            {
                throw new InvalidDataException(
                    $"Pixel {index} uses index {indices[index]}, but the PNG palette has {palette.Count} colors.");
            }
        }

        output.Write(Signature);

        Span<Byte> header = stackalloc Byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;
        header[9] = 3;
        WriteChunk(output, IhdrChunkType, header);

        Byte[] colors = new Byte[checked(palette.Count * 3)];
        Int32 lastTransparentColor = -1;
        for (Int32 index = 0; index < palette.Count; index++)
        {
            Rgba32 color = palette[index];
            Int32 offset = index * 3;
            colors[offset] = color.R;
            colors[offset + 1] = color.G;
            colors[offset + 2] = color.B;
            if (color.A != Byte.MaxValue)
                lastTransparentColor = index;
        }
        WriteChunk(output, PlteChunkType, colors);

        if (lastTransparentColor >= 0)
        {
            Byte[] alpha = new Byte[lastTransparentColor + 1];
            for (Int32 index = 0; index < alpha.Length; index++)
                alpha[index] = palette[index].A;
            WriteChunk(output, TransparencyChunkType, alpha);
        }

        using (MemoryStream compressed = new())
        {
            using (ZLibStream compressor = new(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                for (Int32 y = 0; y < height; y++)
                {
                    compressor.WriteByte(0);
                    compressor.Write(indices.Slice(y * width, width));
                }
            }

            WriteChunk(output, ImageDataChunkType, compressed.ToArray());
            WriteChunk(output, ImageEndChunkType, []);
        }
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<Byte> type, ReadOnlySpan<Byte> data)
    {
        if (type.Length != 4)
            throw new ArgumentException("A PNG chunk type must contain four bytes.", nameof(type));

        Span<Byte> value = stackalloc Byte[sizeof(UInt32)];
        BinaryPrimitives.WriteUInt32BigEndian(value, checked((UInt32)data.Length));
        output.Write(value);
        output.Write(type);
        output.Write(data);

        UInt32 crc = UpdateCrc(UInt32.MaxValue, type);
        crc = UpdateCrc(crc, data) ^ UInt32.MaxValue;
        BinaryPrimitives.WriteUInt32BigEndian(value, crc);
        output.Write(value);
    }

    private static UInt32 UpdateCrc(UInt32 crc, ReadOnlySpan<Byte> data)
    {
        foreach (Byte value in data)
            crc = CrcTable[(crc ^ value) & Byte.MaxValue] ^ (crc >> 8);
        return crc;
    }

    private static UInt32[] CreateCrcTable()
    {
        UInt32[] table = new UInt32[Byte.MaxValue + 1];
        for (UInt32 index = 0; index < table.Length; index++)
        {
            UInt32 value = index;
            for (Int32 bit = 0; bit < 8; bit++)
                value = (value >> 1) ^ (CrcPolynomial & (UInt32)(-(Int32)(value & 1)));
            table[index] = value;
        }
        return table;
    }

    public static DecodedPng Read(Byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        ValidateSignature(content);
        Boolean isIndexed = content[25] == 3;

        using (MagickImage png = new(content))
        {
            Int32 width = checked((Int32)png.Width);
            Int32 height = checked((Int32)png.Height);
            ValidateDimensions(width, height);

            using (IPixelCollection<Byte> pixels = png.GetPixels())
            {
                return isIndexed
                    ? ReadIndexed(png, pixels, width, height)
                    : ReadRgba(pixels, width, height);
            }
        }
    }

    private static DecodedPng ReadIndexed(
        MagickImage png,
        IPixelCollection<Byte> pixels,
        Int32 width,
        Int32 height)
    {
        Int32 paletteSize = checked((Int32)png.ColormapSize);
        if (paletteSize is < 1 or > 256)
            throw new InvalidDataException($"The indexed PNG contains {paletteSize} palette colors; expected 1 through 256.");

        Rgba32[] palette = new Rgba32[paletteSize];
        for (Int32 index = 0; index < palette.Length; index++)
        {
            IMagickColor<Byte> color = png.GetColormapColor(index)
                ?? throw new InvalidDataException($"The indexed PNG has no palette color at index {index}.");
            palette[index] = new Rgba32(color.R, color.G, color.B, color.A);
        }

        UInt32 channel = pixels.GetChannelIndex(PixelChannel.Index)
            ?? throw new InvalidDataException("The indexed PNG does not expose its palette index channel.");
        Byte[] values = pixels.GetValues()
            ?? throw new InvalidDataException("The PNG decoder did not return pixel data.");
        Int32 channels = checked((Int32)pixels.Channels);
        Int32 pixelCount = checked(width * height);
        if (values.Length != checked(pixelCount * channels))
            throw new InvalidDataException("The decoded PNG pixel data does not match its dimensions.");

        Byte[] indices = new Byte[pixelCount];
        Rgba32[] rgba = new Rgba32[pixelCount];
        for (Int32 index = 0; index < pixelCount; index++)
        {
            Byte paletteIndex = values[checked(index * channels + (Int32)channel)];
            if (paletteIndex >= palette.Length)
            {
                throw new InvalidDataException(
                    $"PNG pixel {index} uses index {paletteIndex}, but the palette contains {palette.Length} colors.");
            }
            indices[index] = paletteIndex;
            rgba[index] = palette[paletteIndex];
        }

        return new DecodedPng(width, height, rgba, indices, palette);
    }

    private static DecodedPng ReadRgba(IPixelCollection<Byte> pixels, Int32 width, Int32 height)
    {
        Byte[] values = pixels.ToByteArray(PixelMapping.RGBA)
            ?? throw new InvalidDataException("The PNG decoder did not return pixel data.");
        Int32 pixelCount = checked(width * height);
        if (values.Length != checked(pixelCount * 4))
            throw new InvalidDataException("The decoded PNG pixel data does not match its dimensions.");

        Rgba32[] rgba = MemoryMarshal.Cast<Byte, Rgba32>(values).ToArray();
        return new DecodedPng(width, height, rgba, null, null);
    }

    private static void ValidateSignature(ReadOnlySpan<Byte> content)
    {
        if (content.Length < 26 || !content[..Signature.Length].SequenceEqual(Signature))
            throw new InvalidDataException("The file does not have a valid PNG header.");
    }

    private static void ValidateDimensions(Int32 width, Int32 height)
    {
        if (width is < 1 or > PixelImage.MaximumDimension)
            throw new InvalidDataException($"PNG width {width} is outside the supported range.");
        if (height is < 1 or > PixelImage.MaximumDimension)
            throw new InvalidDataException($"PNG height {height} is outside the supported range.");
        _ = checked(width * height);
    }
}
