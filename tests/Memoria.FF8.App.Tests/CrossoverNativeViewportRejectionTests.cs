using System.Numerics;
using System.Reflection;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverNativeViewportRejectionTests
{
    [Theory]
    [InlineData(-100, 20)]
    [InlineData(400, 20)]
    [InlineData(20, -100)]
    [InlineData(20, 300)]
    public void Fully_outside_native_triangle_is_clipped_before_undefined_reciprocal_depth_reaches_the_host(int x, int y)
    {
        Capture capture = new();
        ITriangleRenderer mapped = Host(capture, 320, 240);
        mapped.Draw(Vertex(x, y, 1f / 1000), Vertex(x + 30, y, float.PositiveInfinity), Vertex(x, y + 30, 1f / 2000), new());
        Assert.Equal(0, capture.Triangles);
    }

    [Fact]
    public void Partly_visible_native_triangle_reaches_the_host_with_its_original_positions_and_depths()
    {
        Capture capture = new();
        ITriangleRenderer mapped = Host(capture, 320, 240);
        mapped.Draw(Vertex(-10, 20, 1f / 1000), Vertex(20, 20, 1f / 2000), Vertex(-10, 50, 1f / 3000), new());
        Assert.Equal(1, capture.Triangles);
        Assert.Equal(new Vector2(-10, 20), capture.First.Position);
        Assert.Equal(1f / 1000, capture.First.ReciprocalW);
    }

    [Fact]
    public void Widescreen_visibility_is_tested_after_centered_host_mapping()
    {
        Capture capture = new();
        ITriangleRenderer mapped = Host(capture, 640, 360);
        mapped.Draw(Vertex(330, 20, 1f / 1000), Vertex(350, 20, 1f / 2000), Vertex(330, 50, 1f / 3000), new());
        Assert.Equal(1, capture.Triangles);
        Assert.Equal(575, capture.First.Position.X);
    }

    [Theory]
    [InlineData(-100, 20)]
    [InlineData(400, 20)]
    [InlineData(20, -100)]
    [InlineData(20, 300)]
    public void Fully_outside_native_line_does_not_submit_undefined_depth(int x, int y)
    {
        Capture capture = new();
        ILineRenderer mapped = (ILineRenderer)Host(capture, 320, 240);
        mapped.DrawLine(Vertex(x, y, float.PositiveInfinity), Vertex(x + 30, y + 30, 1f / 2000), new());
        Assert.Equal(0, capture.Lines);
    }

    [Fact]
    public void Partly_visible_native_line_keeps_its_positions_depth_and_host_ordering()
    {
        Capture capture = new();
        ILineRenderer mapped = (ILineRenderer)Host(capture, 320, 240);
        mapped.DrawLine(Vertex(-10, 20, 1f / 1000), Vertex(20, 50, 1f / 2000), new(null, DepthComparison: TriangleDepthComparison.LessEqual));
        Assert.Equal(1, capture.Lines);
        Assert.Equal(new Vector2(-10, 20), capture.First.Position);
        Assert.Equal(1f / 1000, capture.First.ReciprocalW);
        Assert.Equal(TriangleDepthComparison.GreaterEqual, capture.Material.DepthComparison);
    }

    [Fact]
    public void Native_line_remains_visible_in_the_wider_host_viewport()
    {
        Capture capture = new();
        ILineRenderer mapped = (ILineRenderer)Host(capture, 640, 360);
        mapped.DrawLine(Vertex(330, 20, 1f / 1000), Vertex(350, 50, 1f / 2000), new());
        Assert.Equal(1, capture.Lines);
        Assert.Equal(575, capture.First.Position.X);
    }

    private static TriangleVertex Vertex(float x, float y, float reciprocal) => new(new(x, y), 0.0015f, reciprocal, Vector2.Zero, Vector4.One);

    private static ITriangleRenderer Host(Capture capture, int width, int height)
    {
        Type type = typeof(CrossoverNativeSpriteRenderer).GetNestedType("HostPass", BindingFlags.NonPublic)!;
        return (ITriangleRenderer)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [capture, width, height, 1f], null)!;
    }

    private sealed class Capture : ITriangleRenderer, ILineRenderer
    {
        public int Triangles { get; private set; }
        public int Lines { get; private set; }
        public TriangleVertex First { get; private set; }
        public TriangleMaterial Material { get; private set; }

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Triangles++;
            First = a;
        }

        public void End()
        {
        }

        public void DrawLine(in TriangleVertex a, in TriangleVertex b, in TriangleMaterial material)
        {
            Lines++;
            First = a;
            Material = material;
        }
    }
}
