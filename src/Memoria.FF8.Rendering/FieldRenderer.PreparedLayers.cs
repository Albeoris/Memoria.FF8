using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;

namespace Memoria.FF8.Rendering;

public sealed partial class FieldRenderer
{
    private void DrawPreparedColorLayer(TileRaster raster, Vector3 shade)
    {
        if (raster.Rows.Length == 0 || raster.Columns.Length == 0)
        {
            return;
        }
        PixelImage image = raster.Tile.ResolvedImage;
        for (int y = raster.Rows[0].Start; y < raster.Rows[^1].End; y++)
        {
            for (int x = raster.Columns[0].Start; x < raster.Columns[^1].End; x++)
            {
                int index = y * OutputWidth + x;
                if (raster.Depth > depth[index])
                {
                    continue;
                }
                Vector2 uv = (new Vector2(x + 0.5f, y + 0.5f) - raster.Origin) / raster.Size;
                Rgba32 sample = SamplePreparedLayer(image, uv);
                if (sample.A == 0)
                {
                    continue;
                }
                WritePixel(index, ColorGrade.Apply(Shade(sample, shade)), MapBlendMode.None);
                depth[index] = raster.Depth;
            }
        }
    }

    public static Rgba32 SamplePreparedLayer(PixelImage image, Vector2 uv)
    {
        Vector2 position = uv * new Vector2(image.Width, image.Height) - new Vector2(0.5f);
        Vector2 corner = new(MathF.Floor(position.X), MathF.Floor(position.Y));
        Vector2 mix = position - corner;
        int x = (int)corner.X;
        int y = (int)corner.Y;
        Vector4 top = Vector4.Lerp(Texel(x, y), Texel(x + 1, y), mix.X);
        Vector4 bottom = Vector4.Lerp(Texel(x, y + 1), Texel(x + 1, y + 1), mix.X);
        Vector4 result = Vector4.Lerp(top, bottom, mix.Y);
        if (result.W < 1)
        {
            return default;
        }
        return new((byte)MathF.Round(result.X / result.W), (byte)MathF.Round(result.Y / result.W), (byte)MathF.Round(result.Z / result.W), (byte)MathF.Round(result.W));

        Vector4 Texel(int u, int v)
        {
            Rgba32 color = image[Math.Clamp(u, 0, image.Width - 1), Math.Clamp(v, 0, image.Height - 1)];
            return new(color.R * color.A, color.G * color.A, color.B * color.A, color.A);
        }
    }
}
