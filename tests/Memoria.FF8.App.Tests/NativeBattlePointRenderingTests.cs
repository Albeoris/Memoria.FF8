using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattlePointRenderingTests
{
    [Theory]
    [InlineData(0, 80, 160)]
    [InlineData(1, 10, 20)]
    public void Fixed_TILE1_covers_one_pixel_for_both_coordinate_units_and_restores_the_same_RGBA(ushort flags, int x, int y)
    {
        NativeBattleLogicalCommand command = new(new("point", 0, 0), 1, 0x68, [0x680000FF, Point(x, y)], flags, [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveFrame frame = Frame(new NativeBattleLogicalResolvedCommand(command, new(0), 1));
        byte[] pixels = new BattleRenderer(320, 216).RenderNativePrimitiveFrame(frame, null).ToArray();
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, pixels.AsSpan(4 * (20 * 320 + 10), 4).ToArray());
        Assert.Equal(1, Enumerable.Range(0, 320 * 216).Count(index => pixels[index * 4] != 0));
        NativeBattlePrimitiveFrame restored = JsonSerializer.Deserialize<NativeBattlePrimitiveFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(pixels, new BattleRenderer(320, 216).RenderNativePrimitiveFrame(restored, null));
    }

    [Fact]
    public void Fixed_TILE1_uses_current_viewport_and_the_first_proxy_depth_for_every_corner()
    {
        NativeBattleLogicalCommand command = new(new("point", 0, 0), 1, 0x68, [0x680000FF, Point(10, 20)], 1, [1000, 2000, 3000, 4000], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveDrawMode mode = new(0) { NormalViewport = new(2.5f, 1.5f, 0.3125f, 0.1875f, 3, 4.25f, -3.5f), ViewportInputs = new(800, 600, 0, 0) };
        Capture capture = new();
        NativeBattlePrimitiveFrame frame = Frame(new NativeBattleLogicalResolvedCommand(command, mode, 1)) with { LastDescriptorPerspective = true };
        new BattleRenderer(400, 300) { TriangleRenderer = capture }.RenderNativePrimitiveFrame(frame, null);
        Assert.Equal(6, capture.Vertices.Count);
        Assert.Equal(new Vector2(14.875f, 13.5f), capture.Vertices[0].Position);
        Assert.Equal(1.25f, capture.Vertices.Max(vertex => vertex.Position.X) - capture.Vertices.Min(vertex => vertex.Position.X));
        Assert.Equal(0.75f, capture.Vertices.Max(vertex => vertex.Position.Y) - capture.Vertices.Min(vertex => vertex.Position.Y));
        Assert.All(capture.Vertices, vertex =>
        {
            Assert.Equal(capture.Vertices[0].Depth, vertex.Depth);
            Assert.Equal(0.001f, vertex.ReciprocalW);
        });
    }

    [Fact]
    public void Fixed_TILE1_semitransparency_uses_the_executed_pass_and_ABR_once()
    {
        NativeBattleLogicalCommand command = new(new("point", 0, 0), 1, 0x6A, [0x6A000064, Point(10, 20)], 1, [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveFrame frame = Frame(new(command, new(0), 1), new(command, new(0), 2));
        byte[] pixels = new BattleRenderer(320, 216).RenderNativePrimitiveFrame(frame, null).ToArray();
        Assert.Equal(49, pixels[4 * (20 * 320 + 10)]);
        Assert.Equal(1, Enumerable.Range(0, 320 * 216).Count(index => pixels[index * 4] != 0));
    }

    private static uint Point(int x, int y) => unchecked((ushort)x | (uint)y << 16);
    private static NativeBattlePrimitiveFrame Frame(params NativeBattleLogicalResolvedCommand[] commands) => new(NativeBattleLogicalOrdering.Clear(), commands.ToImmutableArray(), []) { LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(), LastColorState = NativeBattlePrimitiveColorState.FromColdProcess(), LastDescriptorPerspective = false };

    private sealed class Capture : ITriangleRenderer
    {
        public List<TriangleVertex> Vertices { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
        }

        public void End()
        {
        }
    }
}
