using Memoria.FF8.Resources;
using System.Buffers.Binary;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;

namespace Memoria.FF8.Assets;

/// <summary>Composes native tutorial backgrounds and screenshot sprites from menu resources7/12 and each mmag page's TIM.</summary>
public sealed class NativeTutorialArtwork
{
    public const int Width = 384;
    public const int Height = 256;
    private readonly NativeTutorialBrowserCatalog catalog;
    private readonly byte[] sprites;
    private readonly TimImage background;
    private readonly PixelImage backgroundPixels;
    private readonly Dictionary<int, PixelImage> pages = [];

    private NativeTutorialArtwork(NativeTutorialBrowserCatalog catalog, byte[] sprites, byte[] backgroundBytes)
    {
        this.catalog = catalog;
        this.sprites = sprites;
        background = WorldAssetReader.ReadTim(backgroundBytes, 0).Image;
        backgroundPixels = background.Decode();
        if (background.X != 896 || background.Y != 192 || backgroundPixels.Width != 32 || backgroundPixels.Height != 32 || background.Clut?.X != 512 || background.Clut.Y != 219)
        {
            throw new NotSupportedException("Native tutorial background differs from the original32x32 menu texture at VRAM896,192 and CLUT512,219.");
        }
    }

    public PixelImage Read(int pageId)
    {
        if (pages.TryGetValue(pageId, out PixelImage? cached))
        {
            return cached;
        }
        NativeTutorialBrowserPage page = catalog.Categories.SelectMany(category => category.Pages).SingleOrDefault(page => page.Id == pageId) ?? throw new ArgumentOutOfRangeException(nameof(pageId));
        ReadOnlySpan<byte> row = page.Metadata.AsSpan();
        int left = BinaryPrimitives.ReadInt16LittleEndian(row);
        int top = BinaryPrimitives.ReadInt16LittleEndian(row[2..]);
        int width = BinaryPrimitives.ReadInt16LittleEndian(row[4..]);
        int height = BinaryPrimitives.ReadInt16LittleEndian(row[6..]);
        if (left < 0 || top < 0 || width <= 0 || height <= 0 || left + width > Width || top + height > Height)
        {
            throw new InvalidDataException("Native tutorial page rectangle leaves its original384x256 screen.");
        }
        PixelImage result = new(Width, Height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int sourceX = (row[19] * 4 + x) & 31;
                int sourceY = (row[20] + y - background.Y) & 31;
                result[left + x, top + y] = backgroundPixels[sourceX, sourceY];
            }
        }
        TimImage screenshot = WorldAssetReader.ReadTim(page.ContentBytes.ToArray(), 0).Image;
        PixelImage screenshotPixels = screenshot.Decode();
        for (int slot = 0; slot < 4; slot++)
        {
            ReadOnlySpan<byte> placement = row.Slice(36 + slot * 4, 4);
            if (placement[3] != 255)
            {
                DrawSprite(result, screenshot, screenshotPixels, placement[3], left + BinaryPrimitives.ReadUInt16LittleEndian(placement), top + placement[2]);
            }
        }
        pages.Add(pageId, result);
        return result;
    }

    private void DrawSprite(PixelImage destination, TimImage tim, PixelImage atlas, int id, int x, int y)
    {
        int spriteCount = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(sprites));
        if (id >= spriteCount || 4 + spriteCount * 4 > sprites.Length)
        {
            throw new InvalidDataException("Native tutorial screenshot sprite leaves resource7's index.");
        }
        int offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(sprites.AsSpan(4 + id * 4)));
        if (offset < 4 + spriteCount * 4 || offset + 4 > sprites.Length)
        {
            throw new InvalidDataException("Native tutorial screenshot sprite lacks its original part directory.");
        }
        int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(sprites.AsSpan(offset)));
        if (count <= 0 || count > (sprites.Length - offset - 4) / 12)
        {
            throw new InvalidDataException("Native tutorial screenshot sprite has an invalid part count.");
        }
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> part = sprites.AsSpan(offset + 4 + index * 12, 12);
            ushort clut = BinaryPrimitives.ReadUInt16LittleEndian(part[2..]);
            ushort texture = BinaryPrimitives.ReadUInt16LittleEndian(part[10..]);
            int width = BinaryPrimitives.ReadUInt16LittleEndian(part[4..]);
            int height = BinaryPrimitives.ReadUInt16LittleEndian(part[6..]);
            int sourceX = ((texture & 15) * 64 - tim.X) * 2 + part[0];
            int sourceY = ((texture & 16) != 0 ? 256 : 0) + part[1] - tim.Y;
            if (tim.PixelMode != TimPixelMode.Indexed8 || ((texture >> 7) & 3) != 1 || (clut & 63) * 16 != tim.Clut?.X || clut >> 6 != tim.Clut?.Y || sourceX < 0 || sourceY < 0 || sourceX + width > atlas.Width || sourceY + height > atlas.Height)
            {
                throw new NotSupportedException("Native tutorial screenshot sprite requires its original8bit TIM coordinates and palette.");
            }
            int destinationX = x + unchecked((sbyte)part[8]);
            int destinationY = y + unchecked((sbyte)part[9]);
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    int targetX = destinationX + column;
                    int targetY = destinationY + row;
                    Rgba32 pixel = atlas[sourceX + column, sourceY + row];
                    if ((uint)targetX < destination.Width && (uint)targetY < destination.Height && pixel.A != 0)
                    {
                        destination[targetX, targetY] = pixel;
                    }
                }
            }
        }
    }

    public static NativeTutorialArtwork Load(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        using IResourceSource provider = GameDataSourceFactory.Open(gameDirectory);
        using IFlArchive menu = (provider as IGameArchiveSource ?? throw new NotSupportedException("The resource source does not support archive enumeration.")).OpenArchive("menu");
        byte[] Read(string name)
        {
            using Stream source = menu.Entries.Single(entry => Path.GetFileName(entry.RelativePath).Equals(name, StringComparison.OrdinalIgnoreCase)).OpenForRead();
            using MemoryStream result = new();
            source.CopyTo(result);
            return result.ToArray();
        }
        byte[] header = Read("mngrphd.bin");
        byte[] content = Read("mngrp.bin");
        byte[] Resource(int id)
        {
            int offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(id * 8)) - 1);
            int size = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(id * 8 + 4)));
            return content.AsSpan(offset, size).ToArray();
        }
        return new NativeTutorialArtwork(assets.ReadNativeTutorialBrowserCatalog(), Resource(7), Resource(12));
    }
}
