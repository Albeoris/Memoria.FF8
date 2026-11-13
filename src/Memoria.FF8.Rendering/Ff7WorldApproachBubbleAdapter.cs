using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed record Ff7WorldBubbleVisual(Vector3 Position, int Age);

internal sealed class Ff7WorldApproachBubbleAdapter
{
    private const int TextureX = 0x30;
    private const int TextureWidth = 8;
    private const int TextureHeight = 16;
    private readonly PixelImage image = new(TextureWidth, TextureHeight);

    public Ff7WorldApproachBubbleAdapter(PcTexture texture)
    {
        if (texture.BytesPerPixel != 1 || texture.Width < TextureX + TextureWidth || texture.Height < TextureHeight || texture.Palettes.Count < 2)
        {
            throw new InvalidDataException("FFVII world approach bubble requires its original indexed texture.");
        }
        IReadOnlyList<uint> palette = texture.Palettes[1];
        ReadOnlySpan<byte> pixels = texture.Pixels.Span;
        for (int y = 0; y < TextureHeight; y++)
        {
            for (int x = 0; x < TextureWidth; x++)
            {
                int index = pixels[y * texture.Width + TextureX + x];
                uint color = index == 0 ? 0 : palette[index];
                image.Pixels[y * TextureWidth + x] = new Rgba32((byte)(color >> 16), (byte)(color >> 8), (byte)color, (byte)(color >> 24));
            }
        }
    }

    public void Draw(ITriangleRenderer renderer, Ff7WorldView view, Vector3 focus, Ff7WorldBubbleVisual bubble)
    {
        Vector3 position = bubble.Position;
        if (Math.Abs(position.X - focus.X) >= 30000 || Math.Abs(position.Z - focus.Z) >= 30000)
        {
            return;
        }
        Vector3 point = view.ToCamera(position);
        if (point.Z <= 0)
        {
            return;
        }
        float depth = 1 / point.Z;
        float scale = view.Focal * depth;
        float halfSize = 128 * scale;
        Vector2 center = new(view.Width / 2f + point.X * scale, view.Height / 2f + point.Y * scale);
        Vector4 color = new(0, 0, 0, (128 - bubble.Age) / 255f);
        TriangleMaterial material = new(image, TriangleBlend.Alpha, TriangleDepthComparison.GreaterEqual, false, Sampler: TriangleSampler.PointClamp)
        {
            DepthMapping = TriangleDepthMapping.Positive,
            DepthScale = 1
        };
        TriangleVertex a = new(center + new Vector2(-halfSize, -halfSize), depth, 1, Vector2.Zero, color);
        TriangleVertex b = new(center + new Vector2(halfSize, -halfSize), depth, 1, Vector2.UnitX, color);
        TriangleVertex c = new(center + new Vector2(-halfSize, halfSize), depth, 1, Vector2.UnitY, color);
        TriangleVertex d = new(center + new Vector2(halfSize, halfSize), depth, 1, Vector2.One, color);
        renderer.Draw(a, b, c, material);
        renderer.Draw(b, d, c, material);
    }
}
