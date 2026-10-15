using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using System.Collections;
using System.Numerics;

namespace Memoria.FF8.Assets;

/// <summary>Connects decoded source assets to the common texture lifecycle, before native packing.</summary>
internal sealed class AssetTextureScope(ResourceService? resources) : IDisposable
{
    private readonly HashSet<TextureKey> keys = [];

    public AssetHandle<TextureValue>? Register(TextureKey key, PixelImage image, string layout, TextureSourceRegion? region = null, Func<int, PixelImage>? palettes = null, int paletteCount = 1)
    {
        if (resources is null)
        {
            return null;
        }
        bool retain = keys.Add(key);
        ResourceContent encoded = resources.Get(key.Owner);
        return resources.GetTexture(new TextureBuildContext(key, resources.Source, encoded.Revision, layout, image.Width, image.Height, () => image, region, palettes, paletteCount, encoded), retain);
    }

    public IReadOnlyList<PixelImage> RegisterList(ResourceKey owner, IReadOnlyList<PixelImage> images, string layout, string prefix = "texture")
    {
        if (resources is null)
        {
            return images;
        }
        AssetHandle<TextureValue>[] handles = images.Select((image, index) => Register(new TextureKey(owner, $"{prefix}/{index}"), image, layout)!).ToArray();
        return new ResourceTextureList(handles);
    }

    public void Dispose()
    {
        resources?.Release(keys);
        keys.Clear();
    }
}

/// <summary>A stable list whose images follow the resource generation without rebuilding the scene.</summary>
public sealed class ResourceTextureList(AssetHandle<TextureValue>[] textures) : IReadOnlyList<PixelImage>
{
    public int Count => textures.Length;
    public PixelImage this[int index] => textures[index].Value.Image;
    public TextureValue GetTexture(int index) => textures[index].Value;
    public static Vector2 LogicalSize(IReadOnlyList<PixelImage> textures, int index)
    {
        if (textures is ResourceTextureList resources)
        {
            TextureValue value = resources.GetTexture(index);
            return new Vector2(value.LogicalWidth, value.LogicalHeight);
        }
        return new Vector2(textures[index].Width, textures[index].Height);
    }

    public static Vector2 PhysicalScale(IReadOnlyList<PixelImage> textures, int index)
    {
        PixelImage image = textures[index];
        return new Vector2(image.Width, image.Height) / LogicalSize(textures, index);
    }
    public IEnumerator<PixelImage> GetEnumerator() => textures.Select(texture => texture.Value.Image).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
