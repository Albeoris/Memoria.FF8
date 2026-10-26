using Memoria.FF8.Resources;
using System.Buffers.Binary;
using Albeoris.Games.Core.NSCompression.LZS;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets;

/// <summary>Original startup pictures: FF8_EN52FFB0's B91E50 directory and 5305D0 LZS image stream.</summary>
public sealed class NativeStartupArtwork
{
    public const int Width = 640;
    public const int Height = 400;
    private readonly Dictionary<int, PixelImage> images;
    private readonly ResourceService? resources;

    private NativeStartupArtwork(Dictionary<int, PixelImage> images, ResourceService? resources)
    {
        this.images = images;
        this.resources = resources;
    }

    public PixelImage Read(int resourceIndex)
    {
        if (!images.TryGetValue(resourceIndex, out PixelImage? image))
        {
            throw new ArgumentOutOfRangeException(nameof(resourceIndex));
        }
        return resources is null ? image : resources.GetTexture(new(new(new ResourceKey("main/" + ResourceName(resourceIndex)), "atlas"), resources.Source, 1, "native-artwork", Width, Height, () => image)).Value.Image;
    }

    public static string ResourceName(int resourceIndex)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(resourceIndex, 4);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(resourceIndex, 33);
        if (resourceIndex == 4)
        {
            return "square.lzs";
        }
        if (resourceIndex == 33)
        {
            return "ff8.lzs";
        }
        int pair = resourceIndex - 5;
        int group = pair / 4;
        int offset = pair % 4;
        return $"{(offset < 2 ? "name" : "loop")}{group * 2 + offset % 2 + 1:00}.lzs";
    }

    public static NativeStartupArtwork Load(string gameDirectory, ResourceService? resources = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        using IResourceSource provider = GameDataSourceFactory.Open(gameDirectory);
        using IFlArchive archive = (provider as IGameArchiveSource ?? throw new NotSupportedException("The resource source does not support archive enumeration.")).OpenArchive("main");
        Dictionary<int, PixelImage> images = [];
        for (int index = 4; index <= 33; index++)
        {
            string name = ResourceName(index);
            using Stream source = archive.Entries.Single(entry => Path.GetFileName(entry.RelativePath).Equals(name, StringComparison.OrdinalIgnoreCase)).OpenForRead();
            using MemoryStream compressed = new();
            source.CopyTo(compressed);
            images.Add(index, Decode(compressed.ToArray()));
        }
        return new NativeStartupArtwork(images, resources);
    }

    public static PixelImage Decode(byte[] bytes)
    {
        if (bytes.Length < 4 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != bytes.Length - 4)
        {
            throw new InvalidDataException("Original startup picture resource lacks its bounded inner LZS stream.");
        }
        using MemoryStream input = new(bytes, 4, bytes.Length - 4, false);
        using LZSDecompressionStream decoder = new(input, bytes.Length - 4, 8 + Width * Height * 2, false);
        byte[] decoded = new byte[8 + Width * Height * 2];
        decoder.ReadExactly(decoded);
        if (BinaryPrimitives.ReadUInt16LittleEndian(decoded.AsSpan(4)) != Width || BinaryPrimitives.ReadUInt16LittleEndian(decoded.AsSpan(6)) != Height)
        {
            throw new NotSupportedException("Original startup picture resource differs from its native640x400 image contract.");
        }
        Rgba32[] pixels = new Rgba32[Width * Height];
        for (int pixel = 0; pixel < pixels.Length; pixel++)
        {
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(decoded.AsSpan(8 + pixel * 2));
            pixels[pixel] = new Rgba32(Expand(value & 31), Expand(value >> 5 & 31), Expand(value >> 10 & 31), 255);
        }
        return new PixelImage(Width, Height, pixels);
    }

    private static byte Expand(int value)
    {
        return (byte)((value << 3) | (value >> 2));
    }
}
