using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed record Ff7WorldEffectVisual(long Id, Vector3 Position, int Sprite, int Angle, bool GroundAligned, int Width, int Height, Vector4 Color);

/// <summary>Original world sprite sequences, palettes, native quad depth and blend ownership.</summary>
public sealed class Ff7WorldEffectsAdapter(PcTexture texture, Ff7WorldEffectTable table, Ff7WorldPresentationData data)
{
    private readonly Dictionary<int, PixelImage> palettes = [];
    private readonly Ff7WorldMath math = new(data);

    public void Draw(ITriangleRenderer renderer, Ff7WorldView view, Vector3 focus, int roll, int curvatureThreshold, IReadOnlyList<Ff7WorldEffectVisual> particles)
    {
        Matrix4x4 withoutRoll = view.NativeRotation * math.Rotation(0, 0, -roll);
        if (!Matrix4x4.Invert(withoutRoll, out Matrix4x4 billboard))
        {
            throw new InvalidDataException("FFVII world sprite camera rotation is singular.");
        }
        foreach (Ff7WorldEffectVisual particle in particles)
        {
            Vector3 position = particle.Position;
            if (Math.Abs(position.X - focus.X) >= 30000 || Math.Abs(position.Z - focus.Z) >= 30000)
            {
                continue;
            }
            Ff7WorldEffectSprite sprite = table.Sprites[particle.Sprite];
            position = Ff7WorldCameraMath.Curve(position, view.NativeView, view.Focal * 240 / view.Height, curvatureThreshold);
            Matrix4x4 rotation = particle.GroundAligned ? math.Rotation(0, particle.Angle, 0) : billboard;
            Vector3 Corner(int x, int y)
            {
                Vector3 offset = particle.GroundAligned ? new(x * particle.Width * 8, 0, y * particle.Height * 8) : new(x * particle.Width * 8, y * particle.Height * 8, 0);
                return view.ToCamera(position + Vector3.Transform(offset, rotation));
            }
            Vector3[] corners = [Corner(-1, -1), Corner(1, -1), Corner(-1, 1), Corner(1, 1)];
            if (corners.Any(corner => corner.Z <= 0))
            {
                continue;
            }
            float nativeDepth = corners.Min(corner => corner.Z);
            if (((int)nativeDepth >> 4) >= 4096)
            {
                continue;
            }
            PixelImage image = Palette(sprite.Palette);
            TriangleBlend blend = Blend(sprite.Flags, sprite.Palette);
            TriangleMaterial material = new(image, blend, TriangleDepthComparison.GreaterEqual, false, Sampler: TriangleSampler.PointClamp) { DepthMapping = TriangleDepthMapping.Positive, DepthScale = 1, PreserveTargetAlpha = false };
            Vector2 Uv(int x, int y) => new((x == 0 ? sprite.Left : sprite.Right) / (float)image.Width, (y == 0 ? sprite.Top : sprite.Bottom) / (float)image.Height);
            TriangleVertex Vertex(int corner, int x, int y)
            {
                CameraSpaceScreenVertex projected = CameraSpaceTriangleSubmission.Project(new(corners[corner], Vector2.Zero), view.Width, view.Height, view.Focal);
                Vector2 screen = new(MathF.Round(projected.Position.X, MidpointRounding.ToEven), MathF.Round(projected.Position.Y, MidpointRounding.ToEven));
                return new(screen, 1 / nativeDepth, 1, Uv(x, y), particle.Color);
            }
            TriangleVertex a = Vertex(0, 0, 0);
            TriangleVertex b = Vertex(1, 1, 0);
            TriangleVertex c = Vertex(2, 0, 1);
            TriangleVertex d = Vertex(3, 1, 1);
            renderer.Draw(a, b, c, material);
            renderer.Draw(b, d, c, material);
        }
    }

    public static TriangleBlend Blend(int flags, int palette)
    {
        return (flags & 3) switch
        {
            1 or 3 => TriangleBlend.AlphaAdd,
            2 when palette == 20 => TriangleBlend.AlphaAdd,
            _ => TriangleBlend.Alpha
        };
    }

    private PixelImage Palette(int index)
    {
        if (palettes.TryGetValue(index, out PixelImage? image))
        {
            return image;
        }
        if (texture.BytesPerPixel != 1 || (uint)index >= texture.Palettes.Count)
        {
            throw new InvalidDataException("FFVII world sprite references an absent indexed texture palette.");
        }
        image = new(texture.Width, texture.Height);
        IReadOnlyList<uint> palette = texture.Palettes[index];
        ReadOnlySpan<byte> pixels = texture.Pixels.Span;
        for (int pixel = 0; pixel < pixels.Length; pixel++)
        {
            int selected = pixels[pixel];
            uint color = selected == 0 ? 0 : palette[selected];
            image.Pixels[pixel] = new Rgba32((byte)(color >> 16), (byte)(color >> 8), (byte)color, (byte)(color >> 24));
        }
        palettes.Add(index, image);
        return image;
    }
}
