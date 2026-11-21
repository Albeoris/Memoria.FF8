using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed class Ff7SnowboardPresentation
{
    private static readonly TriangleMaterial Solid = new(null, MinimumAlpha: 0);
    private readonly PcSnowboardModel model;
    private readonly IReadOnlyList<Ff7SnowboardTextureBinding> bindings;
    private readonly PixelImage[] textures;

    public Ff7SnowboardPresentation(Ff7Installation installation)
        : this(Ff7SnowboardResources.LoadStory(installation), Ff7PcExecutableData.LoadSnowboardTextureBindings(installation))
    {
    }

    internal Ff7SnowboardPresentation(Ff7SnowboardAssets assets, IReadOnlyList<Ff7SnowboardTextureBinding> bindings)
    {
        model = assets.Model;
        this.bindings = bindings;
        textures = bindings.Select(binding => Ff7FieldRenderer.DecodeTexture(assets.Textures[binding.Name])).ToArray();
    }

    public void Render(ITriangleRenderer renderer, Ff7SnowboardFrame frame, int width, int height)
    {
        renderer.Begin(width, height, new(1, 1, 1, 0));
        try
        {
            foreach (Ff7SnowboardModelInstance instance in frame.Instances)
            {
                if ((uint)instance.ModelIndex >= model.Objects.Count)
                {
                    throw new InvalidDataException("Snowboard draw references an invalid native model.");
                }
                PcSnowboardObject source = model.Objects[instance.ModelIndex];
                Matrix4x4 transform = instance.Transform * frame.View * frame.Projection;
                Vector4[] vertices = source.Vertices.Select(vertex => Vector4.Transform(new Vector4(vertex.X, vertex.Y, vertex.Z, 1), transform)).ToArray();
                foreach (PcSnowboardPrimitive primitive in source.Primitives)
                {
                    IReadOnlyList<uint> words = primitive.Words;
                    int kind = instance.NativePrimitiveKind;
                    int indexWord = kind == 2 ? 4 : 3;
                    if (kind is < 0 or > 3 || words.Count <= indexWord + 1)
                    {
                        throw new InvalidDataException($"Unsupported native snowboard primitive layout: model {instance.ModelIndex}, kind {kind}, mode {primitive.Mode:X2}, words {words.Count}.");
                    }
                    int a = (ushort)words[indexWord];
                    int b = (ushort)words[indexWord + 1];
                    int c = (ushort)(words[indexWord] >> 16);
                    if ((uint)a >= vertices.Length || (uint)b >= vertices.Length || (uint)c >= vertices.Length)
                    {
                        throw new InvalidDataException("Snowboard primitive vertex index exceeds its model.");
                    }
                    TriangleMaterial material = Solid;
                    Vector2 uvA = default;
                    Vector2 uvB = default;
                    Vector2 uvC = default;
                    Vector4 colorA;
                    Vector4 colorB;
                    Vector4 colorC;
                    if (kind == 2)
                    {
                        (material, uvA, uvB, uvC, colorA) = Texture(words, instance.DrawArgument);
                        colorB = colorA;
                        colorC = colorA;
                    }
                    else
                    {
                        colorA = Color(words[0], kind, instance.DrawArgument, vertices[a].Z);
                        colorB = Color(words[2], kind, instance.DrawArgument, vertices[b].Z);
                        colorC = Color(words[1], kind, instance.DrawArgument, vertices[c].Z);
                    }
                    Submit(renderer, new(vertices[a], uvA, colorA), new(vertices[b], uvB, colorB), new(vertices[c], uvC, colorC), material, width, height);
                }
            }
            if (frame.OverlayArgb is uint overlay)
            {
                TriangleMaterial material = new(null, TriangleBlend.Alpha, TriangleDepthComparison.Always, false, MinimumAlpha: 0);
                TrianglePrimitives.Quad(renderer, Vector2.Zero, new(width, height), 0, Argb(overlay), material, Vector2.One);
            }
        }
        finally
        {
            renderer.End();
        }
    }

    private (TriangleMaterial Material, Vector2 A, Vector2 B, Vector2 C, Vector4 Color) Texture(IReadOnlyList<uint> words, int argument)
    {
        int page = (ushort)(words[1] >> 16);
        int depth = (page >> 7) & 3;
        if (depth == 3)
        {
            throw new InvalidDataException("Unsupported snowboard texture page depth.");
        }
        int pageX = page & 15;
        int pageY = (page >> 4) & 1;
        int u = (byte)words[0];
        int v = (byte)(words[0] >> 8);
        for (int index = 0; index < bindings.Count; index++)
        {
            Ff7SnowboardTextureBinding binding = bindings[index];
            int baseU = (binding.X - pageX * 64) << (2 - depth);
            int baseV = binding.Y - pageY * 256;
            int pixelWidth = binding.WidthWords << (2 - depth);
            if (binding.X >> 6 != pageX || binding.Y >> 8 != pageY || u < baseU || u >= baseU + pixelWidth || v < baseV || v >= baseV + binding.Height)
            {
                continue;
            }
            if (index is >= 12 and <= 15)
            {
                uint[] tints = [0x9FFF0000, 0x9F0000FF, 0x9F00FF00, 0x9FFFFF00, 0x9FFFFFFF];
                int tint = argument & 15;
                int variant = index + ((argument >> 4) & 15);
                if ((uint)tint >= tints.Length || (uint)variant >= bindings.Count || bindings[variant].MaterialType != 0)
                {
                    throw new InvalidDataException("Unsupported native balloon material selector.");
                }
                return (Solid, Corner(words[0]), Corner(words[2]), Corner(words[1]), Argb(tints[tint]));
            }
            TriangleMaterial material = Solid with { Texture = textures[index], Sampler = TriangleSampler.PointWrap };
            return (material, Uv(words[0]), Uv(words[2]), Uv(words[1]), Vector4.One);

            Vector2 Uv(uint packed)
            {
                return new(((byte)packed - baseU) / (float)pixelWidth, ((byte)(packed >> 8) - baseV) / (float)binding.Height);
            }
        }
        throw new InvalidDataException("Snowboard primitive has no matching native texture rectangle.");

        static Vector2 Corner(uint packed)
        {
            int x = (byte)packed;
            int y = (byte)(packed >> 8);
            return new(x != 0 && (x == 159 || (x + 1) % 40 != 0) ? 1 : 0, y % 128 != 0 ? 1 : 0);
        }
    }

    private static Vector4 Color(uint packed, int kind, int argument, float depth)
    {
        float factor = kind == 1 ? Math.Clamp(1 - depth / 4096, 0, 1) : 1;
        return new((int)((byte)packed * factor) / 255f, (int)((byte)(packed >> 8) * factor) / 255f, (int)((byte)(packed >> 16) * factor) / 255f, kind == 3 ? (argument == 0 ? 95 : 14) / 255f : 1);
    }

    private static Vector4 Argb(uint color)
    {
        return new((byte)(color >> 16) / 255f, (byte)(color >> 8) / 255f, (byte)color / 255f, (byte)(color >> 24) / 255f);
    }

    private readonly record struct Vertex(Vector4 Clip, Vector2 Uv, Vector4 Color);

    private static void Submit(ITriangleRenderer renderer, Vertex a, Vertex b, Vertex c, TriangleMaterial material, int width, int height)
    {
        Span<Vertex> first = stackalloc Vertex[12];
        Span<Vertex> second = stackalloc Vertex[12];
        first[0] = a;
        first[1] = b;
        first[2] = c;
        int count = 3;
        for (int plane = 0; plane < 6 && count != 0; plane++)
        {
            int output = 0;
            Vertex previous = first[count - 1];
            float previousDistance = Distance(previous.Clip, plane);
            for (int index = 0; index < count; index++)
            {
                Vertex current = first[index];
                float currentDistance = Distance(current.Clip, plane);
                if ((previousDistance >= 0) != (currentDistance >= 0))
                {
                    float fraction = previousDistance / (previousDistance - currentDistance);
                    second[output++] = new(Vector4.Lerp(previous.Clip, current.Clip, fraction), Vector2.Lerp(previous.Uv, current.Uv, fraction), Vector4.Lerp(previous.Color, current.Color, fraction));
                }
                if (currentDistance >= 0)
                {
                    second[output++] = current;
                }
                previous = current;
                previousDistance = currentDistance;
            }
            count = output;
            second[..count].CopyTo(first);
        }
        for (int index = 1; index + 1 < count; index++)
        {
            TriangleVertex x = Project(first[0]);
            TriangleVertex y = Project(first[index]);
            TriangleVertex z = Project(first[index + 1]);
            float area = (y.Position.X - x.Position.X) * (z.Position.Y - x.Position.Y) - (y.Position.Y - x.Position.Y) * (z.Position.X - x.Position.X);
            if (area < 0)
            {
                renderer.Draw(x, y, z, material);
            }
        }

        TriangleVertex Project(Vertex vertex)
        {
            float reciprocal = 1 / vertex.Clip.W;
            return new(new((vertex.Clip.X * reciprocal + 1) * width / 2, (1 - vertex.Clip.Y * reciprocal) * height / 2), Math.Clamp(vertex.Clip.Z * reciprocal, 0, 1), reciprocal, vertex.Uv, vertex.Color);
        }

        static float Distance(Vector4 value, int plane)
        {
            return plane switch
            {
                0 => value.X + value.W,
                1 => value.W - value.X,
                2 => value.Y + value.W,
                3 => value.W - value.Y,
                4 => value.Z,
                _ => value.W - value.Z
            };
        }
    }
}
