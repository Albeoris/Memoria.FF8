using Memoria.FF8.Resources;
using System.Buffers.Binary;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tex;

namespace Memoria.FF8.Assets;

/// <summary>The original right-pointing menu finger, decoded from menu/icon.tex and icon.sp1 entry zero.</summary>
public sealed record NativeMenuPointer(PixelImage Image, int SourceX, int SourceY, int OffsetX, int OffsetY)
{
    public const int Palette = 2;

    public float AspectRatio => (float)Image.Width / Image.Height;

    public static NativeMenuPointer Read(string gameDirectory)
    {
        using IResourceSource provider = GameDataSourceFactory.Open(gameDirectory);
        using IFlArchive menu = (provider as IGameArchiveSource ?? throw new NotSupportedException("The resource source does not support archive enumeration.")).OpenArchive("menu");
        return Decode(ReadEntry(menu, "icon.tex"), ReadEntry(menu, "icon.sp1"));
    }

    public static NativeMenuPointer Decode(ReadOnlySpan<byte> texture, ReadOnlySpan<byte> index)
    {
        if (index.Length < 8)
        {
            throw new InvalidDataException("Original menu icon index lacks its first entry.");
        }

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(index);
        int position = BinaryPrimitives.ReadUInt16LittleEndian(index[4..]);
        int parts = BinaryPrimitives.ReadUInt16LittleEndian(index[6..]);
        if (count == 0 || count > (index.Length - 4) / 4 || parts != 1 || position < 4 + count * 4 || position > index.Length - 8)
        {
            throw new InvalidDataException("Original right-pointing menu finger requires one bounded icon.sp1 part.");
        }

        ReadOnlySpan<byte> entry = index.Slice(position, 8);
        int x = entry[0];
        int y = entry[1];
        int width = entry[4];
        int height = entry[6];
        PixelImage atlas = TexFile.Read(texture).Decode(Palette);
        if (width == 0 || height == 0 || x + width > atlas.Width || y + height > atlas.Height)
        {
            throw new InvalidDataException("Original right-pointing menu finger leaves its icon texture.");
        }

        PixelImage image = new(width, height);
        for (int row = 0; row < height; row++)
        {
            atlas.Pixels.AsSpan((y + row) * atlas.Width + x, width).CopyTo(image.Pixels.AsSpan(row * width, width));
        }

        return new NativeMenuPointer(image, x, y, unchecked((sbyte)entry[5]), unchecked((sbyte)entry[7]));
    }

    private static byte[] ReadEntry(IFlArchive menu, string basename)
    {
        IFlArchiveEntry entry = menu.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals(basename, StringComparison.OrdinalIgnoreCase));
        using Stream source = entry.OpenForRead();
        using MemoryStream result = new(checked((int)entry.Size));
        source.CopyTo(result);
        return result.ToArray();
    }
}
