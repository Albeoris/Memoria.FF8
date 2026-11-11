using System.Numerics;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

internal sealed class CrossoverEffectViewportCapture : ITriangleRenderer
{
    private readonly List<TriangleVertex> vertices = [];

    public static bool CheckProjection(Action<ITriangleRenderer, int, int> draw)
    {
        const int width = 2383;
        const int height = 1096;
        CrossoverEffectViewportCapture standard = new();
        CrossoverEffectViewportCapture widescreen = new();
        draw(standard, 320, 240);
        draw(widescreen, width, height);
        Assert.Equal(standard.vertices.Count, widescreen.vertices.Count);
        bool perspective = false;
        for (int index = 0; index < standard.vertices.Count; index++)
        {
            TriangleVertex source = standard.vertices[index];
            TriangleVertex actual = widescreen.vertices[index];
            if (source.ReciprocalW == 1)
            {
                continue;
            }
            Vector2 expected = (source.Position - new Vector2(160, 120)) * (height / 240f) + new Vector2(width / 2f, height / 2f);
            Assert.InRange(Vector2.Distance(actual.Position, expected), 0, 0.01f);
            Assert.Equal(source.Depth, actual.Depth);
            perspective |= MathF.Abs(source.Position.X - 160) > 10;
        }
        return perspective;
    }

    public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
    {
    }

    public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
    {
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
    }

    public void End()
    {
    }
}
