using Memoria.FF8.Resources;
using System.Buffers.Binary;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;
using Albeoris.Games.FF8.Imaging.Tex;

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
    private readonly IReadOnlyDictionary<int, PixelImage> screenshots;

    private NativeTutorialArtwork(NativeTutorialBrowserCatalog catalog, byte[] sprites, byte[] backgroundBytes, IReadOnlyDictionary<int, PixelImage> screenshots, PixelImage localizedBackground)
    {
        this.screenshots = screenshots;
        this.catalog = catalog;
        this.sprites = sprites;
        background = WorldAssetReader.ReadTim(backgroundBytes, 0).Image;
        backgroundPixels = background.Decode();
        if (background.X != 896 || background.Y != 192 || backgroundPixels.Width != 32 || backgroundPixels.Height != 32 || background.Clut?.X != 512 || background.Clut.Y != 219)
        {
            throw new NotSupportedException("Native tutorial background differs from the original32x32 menu texture at VRAM896,192 and CLUT512,219.");
        }
        NativeArtworkTexture.Scale(localizedBackground, 32, 32);
        backgroundPixels = localizedBackground;
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
        TimImage screenshot = WorldAssetReader.ReadTim(page.ContentBytes.ToArray(), 0).Image;
        PixelImage screenshotPixels = screenshots[page.ContentResource];
        int scale = NativeArtworkTexture.Scale(screenshotPixels, 256, 256);
        PixelImage result = new(Width * scale, Height * scale);
        for (int y = 0; y < height * scale; y++)
        {
            for (int x = 0; x < width * scale; x++)
            {
                int sourceX = (row[19] * 4 + x / scale) & 31;
                int sourceY = (row[20] + y / scale - background.Y) & 31;
                int backgroundScale = backgroundPixels.Width / 32;
                result[left * scale + x, top * scale + y] = backgroundPixels[sourceX * backgroundScale + x % scale * backgroundScale / scale, sourceY * backgroundScale + y % scale * backgroundScale / scale];
            }
        }
        for (int slot = 0; slot < 4; slot++)
        {
            ReadOnlySpan<byte> placement = row.Slice(36 + slot * 4, 4);
            if (placement[3] != 255)
            {
                DrawSprite(result, screenshot, screenshotPixels, placement[3], left + BinaryPrimitives.ReadUInt16LittleEndian(placement), top + placement[2], scale);
            }
        }
        pages.Add(pageId, result);
        return result;
    }

    private void DrawSprite(PixelImage destination, TimImage tim, PixelImage atlas, int id, int x, int y, int scale)
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
            if (tim.PixelMode != TimPixelMode.Indexed8 || ((texture >> 7) & 3) != 1 || (clut & 63) * 16 != tim.Clut?.X || clut >> 6 != tim.Clut?.Y || sourceX < 0 || sourceY < 0 || sourceX + width > atlas.Width / scale || sourceY + height > atlas.Height / scale)
            {
                throw new NotSupportedException("Native tutorial screenshot sprite requires its original8bit TIM coordinates and palette.");
            }
            int destinationX = x + unchecked((sbyte)part[8]);
            int destinationY = y + unchecked((sbyte)part[9]);
            for (int row = 0; row < height * scale; row++)
            {
                for (int column = 0; column < width * scale; column++)
                {
                    int targetX = destinationX * scale + column;
                    int targetY = destinationY * scale + row;
                    Rgba32 pixel = atlas[sourceX * scale + column, sourceY * scale + row];
                    if ((uint)targetX < destination.Width && (uint)targetY < destination.Height && pixel.A != 0)
                    {
                        destination[targetX, targetY] = pixel;
                    }
                }
            }
        }
    }

    public static NativeTutorialArtwork Load(string gameDirectory, ResourceService? resources = null)
    {
        using GameAssets assets = new(gameDirectory, resources: resources);
        using IResourceSource provider = GameDataSourceFactory.Open(gameDirectory);
        using IFlArchive menu = (provider as IGameArchiveSource ?? throw new NotSupportedException("The resource source does not support archive enumeration.")).OpenArchive("menu");
        byte[] Read(string name)
        {
            if (resources is not null)
            {
                return resources.Read(new ResourceKey("menu/" + name));
            }
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
        NativeTutorialBrowserCatalog catalog = assets.ReadNativeTutorialBrowserCatalog();
        Dictionary<int, PixelImage> screenshots = [];
        foreach (NativeTutorialBrowserPage page in catalog.Categories.SelectMany(category => category.Pages).DistinctBy(page => page.ContentResource))
        {
            int atlas = page.ContentGroup switch
            {
                0 => page.ContentIndex,
                1 => 0,
                2 => 7 + page.ContentIndex,
                3 => 8 + page.ContentIndex,
                5 => 10 + page.ContentIndex,
                6 => 15 + page.ContentIndex,
                _ => throw new NotSupportedException("Tutorial screenshot resource group is unsupported.")
            };
            PixelImage original = WorldAssetReader.ReadTim(page.ContentBytes.ToArray(), 0).Image.Decode();
            if (original.Width != 256 || original.Height > 256)
            {
                throw new NotSupportedException("Tutorial screenshot requires its native256 pixel canvas.");
            }
            PixelImage canvas = new(256, 256);
            original.Pixels.CopyTo(canvas.Pixels, 0);
            ResourceKey atlasKey = new($"menu/mag{atlas:00}.tex");
            if (resources is not null)
            {
                using Stream originalStream = (provider.Find(atlasKey) ?? throw new FileNotFoundException($"Original tutorial atlas {atlasKey} is unavailable.")).OpenRead();
                using MemoryStream originalTexture = new();
                originalStream.CopyTo(originalTexture);
                byte[] selected = resources.Read(atlasKey);
                if (!selected.AsSpan().SequenceEqual(originalTexture.ToArray()))
                {
                    canvas = TexFile.Read(selected).Decode();
                    if (canvas.Width != 256 || canvas.Height != 256)
                    {
                        throw new InvalidDataException("Selected tutorial atlas must preserve its native 256 by 256 canvas.");
                    }
                }
            }
            PixelImage resolved = NativeArtworkTexture.Resolve(provider, atlasKey, canvas, resources: resources);
            TextureKey nativeKey = new(new ResourceKey("menu/mngrp.bin"), $"slot{page.ContentResource:000}");
            if (resources?.Localization.Artwork.ContainsKey(nativeKey) == true)
            {
                PixelImage translated = NativeArtworkTexture.Resolve(provider, nativeKey.Owner, original, subresource: nativeKey.Subresource, resources: resources);
                int scale = NativeArtworkTexture.Scale(translated, original.Width, original.Height);
                resolved = new PixelImage(256 * scale, 256 * scale);
                for (int row = 0; row < translated.Height; row++)
                {
                    translated.Pixels.AsSpan(row * translated.Width, translated.Width).CopyTo(resolved.Pixels.AsSpan(row * resolved.Width, translated.Width));
                }
            }
            screenshots.Add(page.ContentResource, resolved);
        }
        byte[] background = Resource(12);
        PixelImage backgroundPixels = WorldAssetReader.ReadTim(background, 0).Image.Decode();
        backgroundPixels = NativeArtworkTexture.Resolve(provider, new ResourceKey("menu/mngrp.bin"), backgroundPixels, subresource: "slot012", resources: resources);
        return new NativeTutorialArtwork(catalog, Resource(7), background, screenshots, backgroundPixels);
    }
}
