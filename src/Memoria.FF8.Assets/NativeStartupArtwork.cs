using Memoria.FF8.Resources;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Original startup pictures: FF8_EN52FFB0's B91E50 directory and 5305D0 LZS image stream.</summary>
public sealed class NativeStartupArtwork
{
    public const int Width = 640;
    public const int Height = 400;
    public const int SquareLogoResourceIndex = 4;
    public static TextureKey SquareLogoTexture { get; } = new(new("main/square.lzs"), "atlas");
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
        TextureKey key = resourceIndex == SquareLogoResourceIndex ? SquareLogoTexture : new(new ResourceKey("main/" + ResourceName(resourceIndex)), "atlas");
        return resources is null ? image : resources.GetTexture(new(key, resources.Source, 1, "native-artwork", Width, Height, () => image)).Value.Image;
    }

    public static PixelBounds PictureBounds(int resourceIndex, int pictureWidth, int pictureHeight, int viewWidth, int viewHeight)
    {
        return resourceIndex == SquareLogoResourceIndex ? ViewportLayout.Fit(pictureWidth, pictureHeight, viewWidth, viewHeight) : new(0, 0, viewWidth, viewHeight);
    }

    public static string ResourceName(int resourceIndex)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(resourceIndex, SquareLogoResourceIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(resourceIndex, 33);
        if (resourceIndex == SquareLogoResourceIndex)
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
        for (int index = SquareLogoResourceIndex; index <= 33; index++)
        {
            string name = ResourceName(index);
            using Stream source = archive.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals(name, StringComparison.OrdinalIgnoreCase)).OpenForRead();
            using MemoryStream compressed = new();
            source.CopyTo(compressed);
            images.Add(index, Decode(compressed.ToArray()));
        }
        return new NativeStartupArtwork(images, resources);
    }

    public static PixelImage Decode(byte[] bytes)
    {
        return NativeLzsArtwork.Decode(bytes, Width, Height);
    }
}
