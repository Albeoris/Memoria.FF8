using System.Runtime.InteropServices;
using Albeoris.Games.Core.NsStreams;

namespace Albeoris.Games.FF8.Imaging.Tim;

/// <summary>A sequence of complete TIM images.</summary>
public sealed class TimFile
{
    public TimFile(IEnumerable<TimImage> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        Images.AddRange(images);
        if (Images.Count == 0)
            throw new ArgumentException("A TIM file must contain at least one image.", nameof(images));
    }

    public List<TimImage> Images { get; } = [];

    public static TimFile Read(Byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (MemoryStream input = new(content, writable: false))
        {
            return Read(input);
        }
    }

    public static TimFile Read(ReadOnlySpan<Byte> content) => Read(content.ToArray());

    public Byte[] Write()
    {
        if (Images.Count == 0)
            throw new InvalidOperationException("A TIM file must contain at least one image.");

        using (MemoryStream output = new())
        {
            foreach (TimImage image in Images)
                WriteImage(output, image);
            return output.ToArray();
        }
    }

    internal static TimFile Read(Stream input)
    {
        if (!input.CanSeek)
            throw new ArgumentException("TIM reading requires a seekable stream.", nameof(input));

        List<TimImage> images = [];
        while (input.Position < input.Length)
            images.Add(ReadImage(input, images.Count));

        if (input.Position != input.Length)
            throw new InvalidDataException("TIM reading did not finish at the end of the stream.");
        return new TimFile(images);
    }

    private static TimImage ReadImage(Stream input, Int32 imageIndex)
    {
        TimHeader header = ReadStruct<TimHeader>(input, $"TIM image {imageIndex} header");
        if (header.Magic != TimHeader.ExpectedMagic)
        {
            throw new InvalidDataException(
                $"TIM image {imageIndex} magic is 0x{header.Magic:X8} instead of 0x{TimHeader.ExpectedMagic:X8}.");
        }

        TimPixelMode pixelMode = header.PixelMode;
        TimClut? clut = header.HasClut ? ReadClut(input, imageIndex) : null;
        if (pixelMode is TimPixelMode.Indexed4 or TimPixelMode.Indexed8 && clut is null)
            throw new InvalidDataException($"Indexed TIM image {imageIndex} has no CLUT.");

        TimBlockHeader imageHeader = ReadStruct<TimBlockHeader>(input, $"TIM image {imageIndex} data header");
        if (imageHeader.BlockSize < TimBlockHeader.Size)
            throw new InvalidDataException($"TIM image {imageIndex} block has invalid size {imageHeader.BlockSize}.");

        Int32 filePixelByteCount = GetFilePixelByteCount(imageHeader, imageIndex);
        Byte[] pixels;
        if (pixelMode == TimPixelMode.Indexed4)
        {
            pixels = new Byte[checked(filePixelByteCount * 2)];
            using (HalfByteStream halfBytes = new(input, leaveOpen: true))
            {
                ReadExactly(halfBytes, pixels, $"TIM image {imageIndex} 4bpp pixels");
            }
        }
        else
        {
            pixels = new Byte[filePixelByteCount];
            ReadExactly(input, pixels, $"TIM image {imageIndex} pixels");
        }

        return new TimImage(
            pixelMode,
            imageHeader.X,
            imageHeader.Y,
            imageHeader.Width,
            imageHeader.Height,
            pixels,
            clut,
            header.Flags,
            imageHeader.BlockSize);
    }

    private static TimClut ReadClut(Stream input, Int32 imageIndex)
    {
        TimBlockHeader header = ReadStruct<TimBlockHeader>(input, $"TIM image {imageIndex} CLUT header");
        if (header.BlockSize < TimBlockHeader.Size || header.ContentSize % sizeof(UInt16) != 0)
            throw new InvalidDataException($"TIM image {imageIndex} CLUT block has invalid size {header.BlockSize}.");

        UInt16[] colors = new UInt16[header.ContentSize / sizeof(UInt16)];
        ReadExactly(input, MemoryMarshal.AsBytes(colors.AsSpan()), $"TIM image {imageIndex} CLUT colors");
        return new TimClut(header.X, header.Y, header.Width, header.Height, colors);
    }

    private static void WriteImage(Stream output, TimImage image)
    {
        output.WriteStruct(new TimHeader(image.PixelMode, image.Flags));
        if (image.Clut is not null)
            WriteClut(output, image.Clut);

        output.WriteStruct(new TimBlockHeader(
            image.DeclaredBlockSize,
            image.X,
            image.Y,
            image.WidthInWords,
            image.Height));

        if (image.PixelMode == TimPixelMode.Indexed4)
        {
            using (HalfByteStream pixels = new(output, leaveOpen: true))
            {
                pixels.Write(image.Pixels);
            }
        }
        else
        {
            output.Write(image.Pixels);
        }
    }

    private static void WriteClut(Stream output, TimClut clut)
    {
        Int32 blockSize = checked(TimBlockHeader.Size + clut.Colors.Length * sizeof(UInt16));
        output.WriteStruct(new TimBlockHeader(blockSize, clut.X, clut.Y, clut.Width, clut.Height));
        output.Write(MemoryMarshal.AsBytes(clut.Colors.AsSpan()));
    }

    private static Int32 GetFilePixelByteCount(TimBlockHeader header, Int32 imageIndex)
    {
        if (header.Width <= 0)
            throw new InvalidDataException($"TIM image {imageIndex} width {header.Width} is invalid.");
        if (header.Height < 0)
            throw new InvalidDataException($"TIM image {imageIndex} height {header.Height} is invalid.");
        return checked(header.Width * sizeof(UInt16) * header.Height);
    }

    private static T ReadStruct<T>(Stream input, String description) where T : unmanaged
    {
        try
        {
            return input.ReadStruct<T>();
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException($"Cannot read {description}: the stream ended unexpectedly.", exception);
        }
    }

    private static void ReadExactly(Stream input, Span<Byte> destination, String description)
    {
        try
        {
            input.ReadExactly(destination);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException(
                $"Cannot read {description}: {destination.Length} bytes are required, but the stream ended unexpectedly.",
                exception);
        }
    }
}
