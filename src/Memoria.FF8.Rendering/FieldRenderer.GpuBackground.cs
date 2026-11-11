using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Rendering;

public sealed partial class FieldRenderer
{
    private Dictionary<PixelImage, BackgroundAtlasRegion> backgroundAtlas = new(ReferenceEqualityComparer.Instance);
    private long backgroundAtlasGeneration = -1;

    private BackgroundAtlasRegion GpuBackgroundRegion(PixelImage image)
    {
        long generation = backgroundPublication?.Generation ?? 0;
        if (backgroundAtlasGeneration != generation || !backgroundAtlas.ContainsKey(image))
        {
            backgroundAtlas = BackgroundTextureAtlas.Build(background.Select(raster => raster.Tile.ResolvedImage));
            backgroundAtlasGeneration = generation;
        }
        return backgroundAtlas[image];
    }
}
