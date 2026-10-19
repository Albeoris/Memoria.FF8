using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleLineRenderingTests
{
    [Theory]
    [InlineData(0x50, 1)]
    [InlineData(0x52, 2)]
    public void Gouraud_line_preserves_independent_endpoint_colors_depths_and_native_pass(int opcode, int pass)
    {
        Capture capture = new();
        NativeBattleLogicalResolvedCommand line = GouraudLine((byte)opcode, (byte)pass);
        new BattleRenderer(320, 216) { TriangleRenderer = capture }.RenderNativePrimitiveFrame(Frame(line) with { LastDescriptorPerspective = true }, null);
        var drawn = Assert.Single(capture.Lines);
        Assert.Equal(new Vector2(10, 18), drawn.A.Position);
        Assert.Equal(new Vector2(30, 18), drawn.B.Position);
        Assert.Equal(new Vector3(1, 0, 0), new Vector3(drawn.A.Color.X, drawn.A.Color.Y, drawn.A.Color.Z));
        Assert.Equal(new Vector3(0, 0, 1), new Vector3(drawn.B.Color.X, drawn.B.Color.Y, drawn.B.Color.Z));
        Assert.Equal(1f / 1000, drawn.A.ReciprocalW);
        Assert.Equal(1f / 2000, drawn.B.ReciprocalW);
        Assert.True(drawn.A.Depth < drawn.B.Depth);
        Assert.Equal(pass == 1, drawn.Material.DepthWrite);
        Assert.Equal(pass == 1 ? TriangleBlend.Replace : TriangleBlend.Add, drawn.Material.Blend);
        capture.Lines.Clear();
        new BattleRenderer(320, 216) { TriangleRenderer = capture }.RenderNativePrimitiveFrame(Frame(line with { Pass = (byte)(3 - pass) }), null);
        Assert.Empty(capture.Lines);
    }

    [Fact]
    public void Cpu_gouraud_line_interpolates_endpoint_colors_and_round_trips_without_filling_adjacent_rows()
    {
        NativeBattlePrimitiveFrame frame = Frame(GouraudLine(0x50, 1));
        byte[] pixels = new BattleRenderer(320, 216).RenderNativePrimitiveFrame(frame, null).ToArray();
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, Pixel(pixels, 10, 18));
        byte[] midpoint = Pixel(pixels, 20, 18);
        Assert.InRange(midpoint[0], (byte)127, (byte)128);
        Assert.InRange(midpoint[2], (byte)127, (byte)128);
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, Pixel(pixels, 20, 19));
        NativeBattlePrimitiveFrame restored = JsonSerializer.Deserialize<NativeBattlePrimitiveFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(pixels, new BattleRenderer(320, 216).RenderNativePrimitiveFrame(restored, null));
    }

    private static NativeBattleLogicalResolvedCommand GouraudLine(byte opcode, byte pass)
    {
        NativeBattleLogicalCommand command = new(new("line", 0, 0), 1, opcode, [(uint)opcode << 24 | 255, Point(10, 18), 0x00FF0000, Point(30, 18)], 1, [1000, 2000, 0, 0], null) { OrderingBucket = 50 };
        return new(command, new(0x20), pass);
    }

    [Theory]
    [InlineData(0, 3.9375f, 0.375f)]
    [InlineData(1, 14.875f, 13.5f)]
    public void Line_endpoints_use_signed_words_and_actual_flag_selected_viewport_without_an_opcode_bias(ushort flags, float x, float y)
    {
        Capture capture = new();
        NativeBattleLogicalResolvedCommand line = Line(0x40, 0x00502010, flags, Point(10, 20), Point(-10, 30)) with { DrawMode = new(0) { NormalViewport = new(2.5f, 1.5f, 0.3125f, 0.1875f, 3, 4.25f, -3.5f), ViewportInputs = new(800, 600, 0, 0) } };
        new BattleRenderer(400, 300) { TriangleRenderer = capture }.RenderNativePrimitiveFrame(Frame(line), null);
        var drawn = Assert.Single(capture.Lines);
        Assert.Equal(new Vector2(x, y), drawn.A.Position);
        Assert.Equal(flags == 1 ? -10.125f : 0.8125f, drawn.B.Position.X);
        Assert.Equal(drawn.A.Color, drawn.B.Color);
        Assert.Equal(new Vector4(16f / 255, 32f / 255, 80f / 255, 127f / 255), drawn.A.Color);
        Assert.Equal(1, drawn.A.ReciprocalW);
        Assert.Equal(1, drawn.B.ReciprocalW);
        Assert.Equal(drawn.A.Depth, drawn.B.Depth);
        Assert.True(drawn.Material.DepthWrite);
        Assert.Null(drawn.Material.Texture);
        Assert.Equal(TriangleBlend.Replace, drawn.Material.Blend);
    }

    [Fact]
    public void Ordered_host_dispatch_keeps_lines_between_triangles_and_applies_semitransparent_material()
    {
        Capture capture = new();
        NativeBattleLogicalResolvedCommand semi = Line(0x42, 0x00000080, 1, Point(10, 20), Point(30, 20), 2) with { DrawMode = new(0x20) };
        new BattleRenderer(320, 216) { TriangleRenderer = capture }.RenderNativePrimitiveFrame(Frame(Triangle(0x000000FF), semi, Triangle(0x0000FF00)), null);
        Assert.Equal(new[] { "triangle", "line", "triangle" }, capture.Order);
        Assert.Equal(TriangleBlend.Add, Assert.Single(capture.Lines).Material.Blend);
        Assert.False(capture.Lines[0].Material.DepthWrite);
    }

    [Fact]
    public void Cpu_line_coverage_retains_submission_order_with_polygon_depth_and_does_not_fill_a_quad()
    {
        NativeBattleLogicalResolvedCommand line = Line(0x40, 0x0000FF00, 1, Point(12, 18), Point(28, 18));
        BattleRenderer renderer = new(320, 216);
        byte[] after = renderer.RenderNativePrimitiveFrame(Frame(Triangle(0x000000FF), line), null).ToArray();
        Assert.Equal(new byte[] { 0, 255, 0, 255 }, Pixel(after, 18, 18));
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, Pixel(after, 18, 19));
        byte[] before = renderer.RenderNativePrimitiveFrame(Frame(line, Triangle(0x000000FF)), null).ToArray();
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, Pixel(before, 18, 18));
        Assert.Equal(1, renderer.DrawnTriangles);
    }

    [Fact]
    public void Cpu_semitransparent_line_blends_once_and_json_restores_the_same_pixels()
    {
        NativeBattleLogicalResolvedCommand line = Line(0x42, 0x00000080, 1, Point(-300, 18), Point(300, 18), 2) with { DrawMode = new(0x20) };
        NativeBattlePrimitiveFrame frame = Frame(Triangle(0x00000040), line);
        byte[] pixels = new BattleRenderer(320, 216).RenderNativePrimitiveFrame(frame, null).ToArray();
        Assert.Equal(192, Pixel(pixels, 18, 18)[0]);
        NativeBattlePrimitiveFrame restored = JsonSerializer.Deserialize<NativeBattlePrimitiveFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(pixels, new BattleRenderer(320, 216).RenderNativePrimitiveFrame(restored, null));
    }

    [Fact]
    public void Wrong_pass_skips_lines_but_missing_line_backend_and_partial_viewport_fail()
    {
        Capture capture = new();
        NativeBattleLogicalResolvedCommand line = Line(0x40, 0xFFFFFF, 1, Point(10, 10), Point(20, 20));
        new BattleRenderer(320, 216) { TriangleRenderer = capture }.RenderNativePrimitiveFrame(Frame(line with { Pass = 2 }), null);
        Assert.Empty(capture.Lines);
        Assert.Throws<NotSupportedException>(() => new BattleRenderer(320, 216) { TriangleRenderer = new TriangleOnly() }.RenderNativePrimitiveFrame(Frame(line), null));
        line = line with { DrawMode = new(0) { ViewportInputs = new(320, 216, 0, 0) } };
        Assert.Throws<NotSupportedException>(() => new BattleRenderer(320, 216).RenderNativePrimitiveFrame(Frame(line), null));
    }

    private static byte[] Pixel(byte[] pixels, int x, int y) => pixels.AsSpan(4 * (y * 320 + x), 4).ToArray();
    private static uint Point(int x, int y) => (ushort)x | unchecked((uint)y << 16);
    private static NativeBattleLogicalResolvedCommand Line(byte opcode, uint color, ushort flags, uint a, uint b, byte pass = 1)
    {
        NativeBattleLogicalCommand command = new(new("line", 0, 0), 1, opcode, [(uint)opcode << 24 | color, a, b], flags, [0, 0, 0, 0], null) { OrderingBucket = 50 };
        return new(command, new(0), pass);
    }
    private static NativeBattleLogicalResolvedCommand Triangle(uint color)
    {
        NativeBattleLogicalCommand command = new(new("polygon", 0, 0), 1, 0x20, [0x20000000 | color, Point(10, 10), Point(40, 10), Point(10, 40)], 1, [0, 0, 0, 0], null) { OrderingBucket = 50 };
        return new(command, new(0), 1);
    }
    private static NativeBattlePrimitiveFrame Frame(params NativeBattleLogicalResolvedCommand[] commands) => new(NativeBattleLogicalOrdering.Clear(), commands.ToImmutableArray(), []) { LastDescriptorPerspective = false, LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(), LastColorState = NativeBattlePrimitiveColorState.FromColdProcess() };

    private class TriangleOnly : ITriangleRenderer
    {
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }
        public virtual void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
        }
        public void End()
        {
        }
    }
    private sealed class Capture : TriangleOnly, ILineRenderer
    {
        public List<string> Order { get; } = [];
        public List<(TriangleVertex A, TriangleVertex B, TriangleMaterial Material)> Lines { get; } = [];
        public override void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Order.Add("triangle");
        }
        public void DrawLine(in TriangleVertex a, in TriangleVertex b, in TriangleMaterial material)
        {
            Order.Add("line");
            Lines.Add((a, b, material));
        }
    }
}
