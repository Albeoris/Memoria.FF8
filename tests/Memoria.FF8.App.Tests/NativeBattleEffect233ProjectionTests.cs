using System.Numerics;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect233ProjectionTests
{
    [Theory]
    [InlineData(1, 0.125f, 127.875f, false)]
    [InlineData(-1, 0.125f, -128f, false)]
    [InlineData(1, 1f, 1023f, false)]
    [InlineData(-1, 1f, -1024f, false)]
    [InlineData(1, 0.125f, 127.875f, true)]
    [InlineData(-1, 0.125f, -128f, true)]
    [InlineData(1, 1f, 1023f, true)]
    [InlineData(-1, 1f, -1024f, true)]
    public void Module_projection_keeps_native_screen_limits_before_scale_and_offsets_at_endpoints_and_between_callbacks(int direction, float scale, float boundary, bool offsets)
    {
        NativeBattlePreparedPresentation first = Frame(1, direction * 20000, true, scale, offsets);
        NativeBattlePreparedPresentation second = Frame(2, direction * 30000, true, scale, offsets);
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        renderer.RenderNativePrimitiveFrame(first.Primitives, null);
        TriangleVertex[] native = capture.Vertices.ToArray();
        TriangleMaterial material = Assert.Single(capture.Materials);
        Assert.Equal(3, native.Length);
        Vector2 offset = offsets ? new(1, -2) : Vector2.Zero;
        Assert.Equal(new Vector2(boundary, boundary) + offset, native[0].Position);
        Assert.Contains(native, vertex => vertex.Position == (scale == 1 ? new Vector2(160, 108) : new Vector2(20, 13.5f)) + offset);
        Assert.Contains(native, vertex => vertex.Position == (scale == 1 ? new Vector2(240, 108) : new Vector2(30, 13.5f)) + offset);

        foreach (float phase in new[] { 0, 0.0001f, 0.5f, 0.9999f, 1 })
        {
            renderer.RenderNativePilotPrimitiveFrame(first, second, phase);
            Assert.Equal(3, capture.Vertices.Count);
            Assert.Equal(material, Assert.Single(capture.Materials));
            for (int corner = 0; corner < native.Length; corner++)
            {
                TriangleVertex sampled = Assert.Single(capture.Vertices, vertex => vertex.Position == native[corner].Position);
                Assert.True(float.IsFinite(sampled.Position.X));
                Assert.True(float.IsFinite(sampled.Position.Y));
                Assert.Equal(native[corner].Position, sampled.Position);
                Assert.Equal(native[corner].Color, sampled.Color);
                Assert.Equal(native[corner].Uv, sampled.Uv);
                Assert.Equal(native[corner].Depth, sampled.Depth);
                Assert.Equal(native[corner].ReciprocalW, sampled.ReciprocalW);
            }
        }
    }

    [Fact]
    public void Absent_projection_bounds_preserve_unrestricted_motion_and_interior_vertices()
    {
        NativeBattlePreparedPresentation first = Frame(1, 20000, false);
        NativeBattlePreparedPresentation second = Frame(2, 30000, false);
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        renderer.RenderNativePrimitiveFrame(first.Primitives, null);
        TriangleVertex[] native = capture.Vertices.ToArray();
        TriangleMaterial material = Assert.Single(capture.Materials);
        renderer.RenderNativePilotPrimitiveFrame(first, second, 0.5f);
        Assert.Equal(3, capture.Vertices.Count);
        Assert.Equal(3145, capture.Vertices[0].Position.X);
        Assert.Equal(3138.5f, capture.Vertices[0].Position.Y);
        Assert.Equal(native[1], capture.Vertices[1]);
        Assert.Equal(native[2], capture.Vertices[2]);
        Assert.Equal(material, Assert.Single(capture.Materials));
    }

    private static NativeBattlePreparedPresentation Frame(long callback, float outside, bool bounded, float scale = 0.125f, bool offsets = false)
    {
        NativeBattlePilotVisualIdentity identity = new(233, 1, 0, 1, 0);
        int boundary = scale == 1 ? outside > 0 ? 8184 : -8192 : outside > 0 ? 1023 : -1024;
        int multiplier = scale == 1 ? 8 : 1;
        int offsetX = offsets ? 8 : 0;
        int offsetY = offsets ? -16 : 0;
        NativeBattleLogicalCommand command = new(new("ground", 0, 0), 1, 0x20, [0x20345678, Point(boundary + offsetX, boundary + offsetY), Point(160 * multiplier + offsetX, 108 * multiplier + offsetY), Point(240 * multiplier + offsetX, 108 * multiplier + offsetY)], 0, [], null)
        {
            OrderingBucket = 50,
            PilotGeometry = new(identity, new(outside, outside, 100), new(0, 0, 100), new(80, 0, 100), default, 3)
            {
                ScreenScale = scale,
                ProjectionScreenBounds = bounded ? new(-8192, -8192, 8184, 8184) : null,
                ScreenOffsets = offsets ? [new(8, -16), new(8, -16), new(8, -16)] : [],
                SourceDepths = Vector4.Zero,
                UseSourceDepths = true
            }
        };
        NativeBattlePreparedPrimitiveFrame primitives = new([new(command, new(0), 1)], [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
        return new(callback, callback, callback, false, primitives, null, null, null, new(new(0, 0, 0), new(0, 0, 100), 100, 0), [], null, null, [], null)
        {
            PilotEffect = 233,
            PilotGeneration = 1,
            PilotCamera = new(new(0, 0, 0), new(0, 0, 100), 100, 0),
            PilotProjection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108)
        };
    }

    private static uint Point(int x, int y) => unchecked((ushort)x | ((uint)(ushort)y << 16));

    private sealed class Capture : ITriangleRenderer
    {
        internal List<TriangleVertex> Vertices { get; } = [];
        internal List<TriangleMaterial> Materials { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Vertices.Clear();
            Materials.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
            Materials.Add(material);
        }

        public void End()
        {
        }
    }
}
