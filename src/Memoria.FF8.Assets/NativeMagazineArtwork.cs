using System.Buffers.Binary;
using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tex;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Original magazine illustrations composed from mngrp section7 sprite parts and PC mag00..09 TEX atlases.</summary>
public sealed class NativeMagazineArtwork
{
    private readonly PixelImage[] atlases;
    private readonly byte[] index;
    private readonly NativeMenuArtwork menu;
    private readonly Dictionary<int, PixelImage> images = [];

    private NativeMagazineArtwork(PixelImage[] atlases, byte[] index, NativeMenuArtwork menu)
    {
        this.atlases = atlases;
        this.index = index;
        this.menu = menu;
    }

    public static NativeMagazineArtwork Load(string gameDirectory)
    {
        using IFlArchive archive = FlArchive.OpenForRead(Path.Combine(gameDirectory, "Data", "lang-en", "menu.fs"), FlArchiveRepresentation.Files);
        byte[] Read(string name)
        {
            IFlArchiveEntry entry = archive.Entries.Single(entry => Path.GetFileName(entry.RelativePath).Equals(name, StringComparison.OrdinalIgnoreCase));
            using Stream stream = entry.OpenForRead();
            using MemoryStream result = new();
            stream.CopyTo(result);
            return result.ToArray();
        }
        byte[] content = Read("mngrp.bin");
        byte[] header = Read("mngrphd.bin");
        int offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(7 * 8)) - 1);
        int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(7 * 8 + 4)));
        if (offset < 0 || length < 4 || offset > content.Length - length)
        {
            throw new InvalidDataException("Magazine sprite section7 lies outside mngrp.bin.");
        }
        PixelImage[] atlases = Enumerable.Range(0, 10).Select(id => TexFile.Read(Read($"mag{id:00}.TEX")).Decode()).ToArray();
        if (atlases.Any(atlas => atlas.Width != 256 || atlas.Height != 256))
        {
            throw new InvalidDataException("Original magazine atlases must be 256x256 pixels.");
        }
        return new NativeMagazineArtwork(atlases, content.AsSpan(offset, length).ToArray(), NativeMenuArtwork.Load(gameDirectory));
    }

    public PixelImage GetButtonImage(int buttonBit) => menu.GetOriginalButtonIcon(buttonBit);

    public static byte[] ReadItemIconKinds(byte[] executable)
    {
        if (Convert.ToHexString(SHA256.HashData(executable)) != NativeFieldRuntimeProfile.FF8EnExecutableHash || executable.Length < 0x788024 + 22)
        {
            throw new NotSupportedException("Item icons require the authenticated original English executable.");
        }
        return executable.AsSpan(0x788024, 22).ToArray();
    }

    /// <summary>The native magita paper's mean display color, for procedural paper generation.</summary>
    public static Rgba32 GetPaperColor(byte[] texture)
    {
        PixelImage paper = TexFile.Read(texture).Decode();
        if (paper.Width != 32 || paper.Height != 32 || paper.Pixels.Any(pixel => pixel.A != 255))
        {
            throw new InvalidDataException("Magazine paper requires the original opaque32x32 magita texture.");
        }
        return new Rgba32((byte)Math.Round(paper.Pixels.Average(pixel => pixel.R)), (byte)Math.Round(paper.Pixels.Average(pixel => pixel.G)), (byte)Math.Round(paper.Pixels.Average(pixel => pixel.B)), 255);
    }

    public PixelImage GetImage(int imageId)
    {
        if (!images.TryGetValue(imageId, out PixelImage? image))
        {
            (int width, int height) = GetDimensions(index, imageId);
            image = new PixelImage(width, height);
            PixelImage atlas = atlases[imageId < 28 ? imageId / 4 : imageId < 34 ? 7 : imageId < 37 ? 8 : 9];
            foreach (Part part in ReadParts(index, imageId))
            {
                for (int y = 0; y < part.Height; y++)
                {
                    for (int x = 0; x < part.Width; x++)
                    {
                        Rgba32 pixel = atlas[part.SourceX + x, part.SourceY + y];
                        if (pixel.A != 0)
                        {
                            image[part.X + x, part.Y + y] = pixel;
                        }
                    }
                }
            }
            images.Add(imageId, image);
        }
        return image;
    }

    public static (int Width, int Height) GetDimensions(ReadOnlySpan<byte> index, int imageId)
    {
        Part[] parts = ReadParts(index, imageId);
        return (parts.Max(part => part.X + part.Width), parts.Max(part => part.Y + part.Height));
    }

    private static Part[] ReadParts(ReadOnlySpan<byte> index, int imageId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(imageId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(imageId, 40);
        if (index.Length < 4 || BinaryPrimitives.ReadUInt32LittleEndian(index) != 79 || index.Length < 4 + 79 * 4)
        {
            throw new InvalidDataException("Original magazine sprite index requires its79 entries.");
        }
        int offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(index[(4 + imageId * 4)..]));
        if (offset < 4 + 79 * 4 || offset > index.Length - 4)
        {
            throw new InvalidDataException("Magazine sprite offset leaves section7.");
        }
        int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(index[offset..]));
        if (count is < 1 or > 2 || offset + 4 > index.Length - count * 12)
        {
            throw new InvalidDataException("Magazine illustration requires one or two bounded native sprite parts.");
        }
        Part[] result = new Part[count];
        for (int id = 0; id < count; id++)
        {
            ReadOnlySpan<byte> part = index.Slice(offset + 4 + id * 12, 12);
            Part value = new(part[0], part[1], BinaryPrimitives.ReadUInt16LittleEndian(part[4..]), BinaryPrimitives.ReadUInt16LittleEndian(part[6..]), (sbyte)part[8], (sbyte)part[9]);
            if (BinaryPrimitives.ReadUInt16LittleEndian(part[2..]) != 0x36A0 || BinaryPrimitives.ReadUInt16LittleEndian(part[10..]) != 0xAE || value.X < 0 || value.Y < 0 || value.Width < 1 || value.Height < 1 || value.SourceX + value.Width > 256 || value.SourceY + value.Height > 256)
            {
                throw new InvalidDataException("Magazine sprite violates its native palette, texture page or atlas bounds.");
            }
            result[id] = value;
        }
        return result;
    }

    private sealed record Part(int SourceX, int SourceY, int Width, int Height, int X, int Y);
}
