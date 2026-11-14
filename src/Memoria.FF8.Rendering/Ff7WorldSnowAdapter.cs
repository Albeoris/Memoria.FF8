using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed record Ff7WorldSnowLayer(int Layer, float Brightness, float U, float V);

/// <summary>Original snowfield wind overlays, sampled from the completed simulation callback.</summary>
public sealed class Ff7WorldSnowAdapter(PcTexture first, PcTexture second)
{
    private readonly PixelImage firstImage = Ff7FieldRenderer.DecodeTexture(first, useColorKey: true);
    private readonly PixelImage secondImage = Ff7FieldRenderer.DecodeTexture(second, useColorKey: true);

    public void Draw(ITriangleRenderer renderer, int width, int height, IReadOnlyList<Ff7WorldSnowLayer> layers)
    {
        foreach (Ff7WorldSnowLayer layer in layers)
        {
            if (layer.Layer is not (1 or 2) || !float.IsFinite(layer.Brightness) || layer.Brightness < 0 || layer.Brightness > 128 || !float.IsFinite(layer.U) || !float.IsFinite(layer.V))
            {
                throw new InvalidDataException("FFVII snow layers require their original layer, bounded intensity and finite texture phase.");
            }
            if (layer.Brightness == 0)
            {
                continue;
            }
            PixelImage image = layer.Layer == 1 ? firstImage : secondImage;
            Vector4 color = new(layer.Brightness / 255, layer.Brightness / 255, layer.Brightness / 255, 128 / 255f);
            Vector2 uv = new(layer.U / 256, layer.V / 256);
            TriangleVertex a = new(Vector2.Zero, 0, 1, uv, color);
            TriangleVertex b = new(new(width, 0), 0, 1, uv + new Vector2(width / 256f, 0), color);
            TriangleVertex c = new(new(0, height), 0, 1, uv + new Vector2(0, height / 256f), color);
            TriangleVertex d = new(new(width, height), 0, 1, uv + new Vector2(width / 256f, height / 256f), color);
            TriangleMaterial material = new(image, TriangleBlend.AlphaAdd, TriangleDepthComparison.Always, false, Sampler: TriangleSampler.PointWrap) { PreserveTargetAlpha = false };
            renderer.Draw(a, b, c, material);
            renderer.Draw(b, d, c, material);
        }
    }
}
