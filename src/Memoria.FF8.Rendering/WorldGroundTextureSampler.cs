using System.Numerics;
using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Rendering;

/// <summary>Filters ground page colors for the software world comparison.</summary>
internal sealed class WorldGroundTextureSampler
{
    private static readonly ConditionalWeakTable<PixelImage, WorldGroundTextureSampler> samplers = new();
    private readonly List<PixelImage> levels;
    private readonly Vector2 textureSize;

    private WorldGroundTextureSampler(PixelImage texture)
    {
        levels = [texture];
        textureSize = new(texture.Width, texture.Height);
        while (texture.Width > 1 || texture.Height > 1)
        {
            texture = Downsample(texture);
            levels.Add(texture);
        }
    }

    internal static WorldGroundTextureSampler For(PixelImage texture) => samplers.GetValue(texture, static image => new(image));

    internal Rgba32 Sample(Vector2 uv, float distance, Vector3 textureStepX, Vector3 textureStepY, Rgba32 pointColor)
    {
        // Differentiate UV/Z divided by 1/Z to measure the footprint in source texels.
        Vector2 dx = (new Vector2(textureStepX.X, textureStepX.Y) - uv * textureStepX.Z) * distance * textureSize;
        Vector2 dy = (new Vector2(textureStepY.X, textureStepY.Y) - uv * textureStepY.Z) * distance * textureSize;
        float footprintSquared = MathF.Max(dx.LengthSquared(), dy.LengthSquared());
        float lod = footprintSquared > 1 ? MathF.Min(0.5f * MathF.Log2(footprintSquared), levels.Count - 1) : 0;
        int level = (int)lod;
        Vector4 filtered = Bilinear(levels[level], uv);
        if (lod > level)
        {
            filtered = Vector4.Lerp(filtered, Bilinear(levels[level + 1], uv), lod - level);
        }
        // Preserve the original cutout test while keeping transparent RGB out of nearby samples.
        return filtered.W > 0 ? new(ToByte(filtered.X / filtered.W), ToByte(filtered.Y / filtered.W), ToByte(filtered.Z / filtered.W), pointColor.A) : pointColor;
    }

    private static Vector4 Bilinear(PixelImage texture, Vector2 uv)
    {
        float x = Math.Clamp(uv.X * texture.Width - 0.5f, 0, texture.Width - 1);
        float y = Math.Clamp(uv.Y * texture.Height - 0.5f, 0, texture.Height - 1);
        int x0 = (int)x;
        int x1 = Math.Min(x0 + 1, texture.Width - 1);
        int y0 = (int)y * texture.Width;
        int y1 = Math.Min((int)y + 1, texture.Height - 1) * texture.Width;
        Rgba32[] pixels = texture.Pixels;
        Vector4 top = Vector4.Lerp(Premultiplied(pixels[y0 + x0]), Premultiplied(pixels[y0 + x1]), x - x0);
        Vector4 bottom = Vector4.Lerp(Premultiplied(pixels[y1 + x0]), Premultiplied(pixels[y1 + x1]), x - x0);
        return Vector4.Lerp(top, bottom, y - (int)y);
    }

    private static PixelImage Downsample(PixelImage source)
    {
        PixelImage result = new(Math.Max(1, source.Width / 2), Math.Max(1, source.Height / 2));
        double width = source.Width / (double)result.Width;
        double height = source.Height / (double)result.Height;
        for (int y = 0; y < result.Height; y++)
        {
            double top = y * height;
            double bottom = Math.Min((y + 1) * height, source.Height);
            for (int x = 0; x < result.Width; x++)
            {
                double left = x * width;
                double right = Math.Min((x + 1) * width, source.Width);
                Vector4 sum = default;
                for (int sy = (int)top; sy < Math.Ceiling(bottom); sy++)
                {
                    double vertical = Math.Min(sy + 1, bottom) - Math.Max(sy, top);
                    for (int sx = (int)left; sx < Math.Ceiling(right); sx++)
                    {
                        double horizontal = Math.Min(sx + 1, right) - Math.Max(sx, left);
                        sum += Premultiplied(source.Pixels[sy * source.Width + sx]) * (float)(horizontal * vertical);
                    }
                }
                if (sum.W > 0)
                {
                    result.Pixels[y * result.Width + x] = new(ToByte(sum.X / sum.W), ToByte(sum.Y / sum.W), ToByte(sum.Z / sum.W), ToByte(sum.W / (float)(width * height)));
                }
            }
        }
        return result;
    }

    private static Vector4 Premultiplied(Rgba32 color) => new(color.R * color.A, color.G * color.A, color.B * color.A, color.A);

    private static byte ToByte(float value) => (byte)Math.Clamp(value + 0.5f, 0, 255);
}
