using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using ImageMagick;

namespace Memoria.FF8.Mods;

public sealed record DecodedModImage(PixelImage Image, IReadOnlyList<PixelImage> MipLevels);

/// <summary>CPU decoding shared by texture-package adapters. MipLevels includes the base image.</summary>
public static class ModImageDecoder
{
    private static readonly ConditionalWeakTable<ModFile, DecodedFileCache> decodedFiles = new();

    /// <summary>Reuses living mip images across catalog generations without retaining unloaded images or files.</summary>
    public static DecodedModImage Decode(ModFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return decodedFiles.GetValue(file, _ => new DecodedFileCache()).Get(file);
    }

    private sealed class DecodedFileCache
    {
        private readonly object sync = new();
        private WeakReference<PixelImage>[]? levels;

        public DecodedModImage Get(ModFile file)
        {
            lock (sync)
            {
                if (levels is not null)
                {
                    PixelImage[] living = new PixelImage[levels.Length];
                    bool complete = true;
                    for (int i = 0; i < levels.Length; i++)
                    {
                        if (!levels[i].TryGetTarget(out PixelImage? image))
                        {
                            complete = false;
                            break;
                        }
                        living[i] = image;
                    }
                    if (complete)
                    {
                        return new DecodedModImage(living[0], Array.AsReadOnly(living));
                    }
                }
                DecodedModImage decoded = DecodeFile(file);
                levels = decoded.MipLevels.Select(image => new WeakReference<PixelImage>(image)).ToArray();
                return decoded;
            }
        }
    }

    private static DecodedModImage DecodeFile(ModFile file)
    {
        try
        {
            return Decode(file.ReadBytes(), file.RelativePath);
        }
        catch (Exception exception) when (exception is not (ModTextureLoadException or OutOfMemoryException or OperationCanceledException))
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, exception);
        }
    }

    public static DecodedModImage Decode(byte[] bytes, string path)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            return DecodeCore(bytes, path);
        }
        catch (Exception exception) when (exception is not (InvalidDataException or NotSupportedException or OutOfMemoryException or OperationCanceledException))
        {
            throw new InvalidDataException($"Texture image '{path}' could not be decoded.", exception);
        }
    }

    private static DecodedModImage DecodeCore(byte[] bytes, string path)
    {
        if (Path.GetExtension(path).Equals(".dds", StringComparison.OrdinalIgnoreCase))
        {
            return DecodeDds(bytes);
        }
        if (!Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) || bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            throw new InvalidDataException($"Unsupported texture image '{path}'. Expected PNG or a two-dimensional DDS.");
        }
        ValidateDimensions(checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16))), checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20))));
        using MagickImage png = new(bytes);
        using IPixelCollection<byte> pixels = png.GetPixels();
        byte[] rgba = pixels.ToByteArray(PixelMapping.RGBA) ?? throw new InvalidDataException("PNG decoder returned no pixels.");
        PixelImage image = new(checked((int)png.Width), checked((int)png.Height), MemoryMarshal.Cast<byte, Rgba32>(rgba).ToArray());
        return new(image, Array.AsReadOnly(new[] { image }));
    }

    private static DecodedModImage DecodeDds(byte[] bytes)
    {
        if (bytes.Length < 128 || Read(bytes, 0) != 0x20534444 || Read(bytes, 4) != 124 || Read(bytes, 76) != 32)
        {
            throw new InvalidDataException("Invalid DDS header.");
        }
        int width = checked((int)Read(bytes, 16));
        int height = checked((int)Read(bytes, 12));
        ValidateDimensions(width, height);
        if ((Read(bytes, 112) & 0xFE200) != 0 || Read(bytes, 24) > 1)
        {
            throw new NotSupportedException("DDS cube maps and volume textures are unsupported.");
        }
        int mipCount = Math.Max(1, checked((int)Read(bytes, 28)));
        int maximumMips = 1 + (int)Math.Floor(Math.Log2(Math.Max(width, height)));
        if (mipCount > maximumMips)
        {
            throw new InvalidDataException("DDS mip count exceeds its dimensions.");
        }
        uint fourCc = Read(bytes, 84);
        int offset = 128;
        CompressionFormat? compression = null;
        uint red = Read(bytes, 92);
        uint green = Read(bytes, 96);
        uint blue = Read(bytes, 100);
        uint alpha = Read(bytes, 104);
        if ((Read(bytes, 80) & 4) != 0)
        {
            compression = fourCc switch
            {
                0x31545844 => CompressionFormat.Bc1,
                0x33545844 => CompressionFormat.Bc2,
                0x35545844 => CompressionFormat.Bc3,
                0x30315844 => null,
                _ => throw new NotSupportedException($"Unsupported DDS FourCC 0x{fourCc:X8}.")
            };
            if (fourCc == 0x30315844)
            {
                if (bytes.Length < 148 || Read(bytes, 132) != 3 || Read(bytes, 140) != 1 || (Read(bytes, 136) & 4) != 0)
                {
                    throw new NotSupportedException("DDS DX10 texture must be one two-dimensional image.");
                }
                offset = 148;
                uint format = Read(bytes, 128);
                compression = format switch
                {
                    71 or 72 => CompressionFormat.Bc1,
                    74 or 75 => CompressionFormat.Bc2,
                    77 or 78 => CompressionFormat.Bc3,
                    98 or 99 => CompressionFormat.Bc7,
                    28 or 29 or 87 or 91 => null,
                    _ => throw new NotSupportedException($"Unsupported DDS DXGI format {format}.")
                };
                red = format is 87 or 91 ? 0xFF0000U : 0xFF;
                green = 0xFF00;
                blue = format is 87 or 91 ? 0xFFU : 0xFF0000;
                alpha = 0xFF000000;
            }
        }
        else if ((Read(bytes, 80) & 0x40) == 0 || Read(bytes, 88) != 32)
        {
            throw new NotSupportedException("Uncompressed DDS requires 32-bit RGBA/BGRA pixels.");
        }
        if (compression is null && !((red == 0xFF && green == 0xFF00 && blue == 0xFF0000) || (red == 0xFF0000 && green == 0xFF00 && blue == 0xFF)) || compression is null && alpha is not (0 or 0xFF000000))
        {
            throw new NotSupportedException("Unsupported DDS channel masks.");
        }
        List<PixelImage> levels = [];
        BcDecoder decoder = new();
        for (int mip = 0; mip < mipCount; mip++)
        {
            Rgba32[] pixels;
            if (compression is CompressionFormat format)
            {
                int length = checked(Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * (format == CompressionFormat.Bc1 ? 8 : 16));
                Require(bytes, offset, length);
                ColorRgba32[] decoded = decoder.DecodeRaw(bytes.AsSpan(offset, length).ToArray(), width, height, format);
                pixels = new Rgba32[decoded.Length];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Rgba32(decoded[i].r, decoded[i].g, decoded[i].b, decoded[i].a);
                }
                offset += length;
            }
            else
            {
                int pitch = mip == 0 && (Read(bytes, 8) & 8) != 0 ? checked((int)Read(bytes, 20)) : checked(width * 4);
                if (pitch < width * 4)
                {
                    throw new InvalidDataException("DDS row pitch is smaller than its pixels.");
                }
                Require(bytes, offset, checked(pitch * height));
                pixels = new Rgba32[checked(width * height)];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        uint value = Read(bytes, offset + y * pitch + x * 4);
                        pixels[y * width + x] = new Rgba32((byte)((value & red) >> (red == 0xFF ? 0 : 16)), (byte)(value >> 8), (byte)((value & blue) >> (blue == 0xFF ? 0 : 16)), alpha == 0 ? byte.MaxValue : (byte)(value >> 24));
                    }
                }
                offset += pitch * height;
            }
            levels.Add(new PixelImage(width, height, pixels));
            width = Math.Max(1, width / 2);
            height = Math.Max(1, height / 2);
        }
        return new(levels[0], levels.AsReadOnly());
    }

    private static uint Read(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static void Require(byte[] bytes, int offset, int length)
    {
        if (offset > bytes.Length || length > bytes.Length - offset)
        {
            throw new InvalidDataException("Truncated DDS pixel data.");
        }
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width is < 1 or > PixelImage.MaximumDimension || height is < 1 or > PixelImage.MaximumDimension)
        {
            throw new InvalidDataException($"Texture dimensions {width}x{height} are unsupported.");
        }
    }
}
