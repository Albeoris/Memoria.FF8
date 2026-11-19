using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Rendering;

public sealed partial class FieldRenderer
{
    /// <summary>Prepare dependent background pages before the shared source generation is published.</summary>
    public PreparedFieldTextures? PrepareTexturePublication(IReadOnlyDictionary<TextureKey, TextureValue> changed, long generation)
    {
        if (!background.Any(raster => raster.Tile.Texture?.TextureKey is { } key && changed.ContainsKey(key)))
        {
            return null;
        }
        PixelImage[] sources = background.Select(raster => raster.Tile.Texture?.TextureKey is { } key && changed.TryGetValue(key, out TextureValue? replacement) ? replacement.Image : raster.Tile.ResolvedImage).ToArray();
        Dictionary<(PixelImage Image, FieldBackgroundIndexedSource? Source, ushort Clut), PixelImage> subtractive = [];
        List<PixelImage> all = [];
        for (int index = 0; index < background.Length; index++)
        {
            BackgroundTile tile = background[index].Tile;
            PixelImage source = sources[index];
            all.Add(source);
            if (UsesSubtractivePalette(tile))
            {
                var key = (source, tile.IndexedSource, SubtractiveClut(tile));
                if (!subtractive.TryGetValue(key, out PixelImage? alpha))
                {
                    alpha = new(source.Width, source.Height);
                    for (int texel = 0; texel < alpha.Pixels.Length; texel++)
                    {
                        alpha.Pixels[texel] = SubtractiveColor(tile, source, texel);
                    }
                    subtractive.Add(key, alpha);
                }
                all.Add(alpha);
            }
        }
        Dictionary<PixelImage, BackgroundAtlasRegion> atlas = BackgroundTextureAtlas.Build(all);
        return new PreparedFieldTextures(atlas.Values.Select(region => region.Image).Distinct<PixelImage>(ReferenceEqualityComparer.Instance).ToArray(), backgroundAtlas.Values.Select(region => region.Image).Distinct<PixelImage>(ReferenceEqualityComparer.Instance).ToArray(), () =>
        {
            backgroundAtlas = atlas;
            backgroundAtlasGeneration = generation;
            subtractiveImages.Clear();
            foreach (var pair in subtractive)
            {
                subtractiveImages.Add(pair.Key, pair.Value);
            }
            subtractivePublishedSources = sources;
            subtractivePublicationGeneration = generation;
        });
    }
}

public sealed class PreparedFieldTextures(IReadOnlyList<PixelImage> images, IReadOnlyList<PixelImage> retiredImages, Action commit)
{
    public IReadOnlyList<PixelImage> Images { get; } = images;
    public IReadOnlyList<PixelImage> RetiredImages { get; } = retiredImages;
    public void Commit() => commit();
}
