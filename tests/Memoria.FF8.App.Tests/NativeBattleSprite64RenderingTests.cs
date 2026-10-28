using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSprite64RenderingTests
{
    [Fact]
    public void TextureQueriesReceiveTheRenderedFirstCornerAndSeparateSourceVariants()
    {
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        SampleSource source = new();
        NativeBattleEffectTextureState state = Texture(31);
        renderer.RenderNativePrimitiveFrame(Frame(Command(Mode(), u: 32, v: 64)), state, capabilities: Capabilities(), effectTextureSource: source);
        renderer.RenderNativePrimitiveFrame(Frame(Command(Mode(), u: 32, v: 192)), state, capabilities: Capabilities(), effectTextureSource: source);
        Assert.Equal(new[] { new TexturePrimitiveSample(32f / 256, 64f / 256), new TexturePrimitiveSample(32f / 256, 192f / 256) }, source.Samples);
    }

    private sealed class SampleSource : INativeBattleEffectTextureSource
    {
        public long Generation => 0;
        public List<TexturePrimitiveSample> Samples { get; } = [];

        public int GetPrimitiveSampleKey(TexturePrimitiveSample sample)
        {
            return sample.FirstV < 0.5f ? 1 : 2;
        }

        public PixelImage Apply(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels = null, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null)
        {
            throw new InvalidOperationException("The renderer must publish its actual primitive sampling coordinates.");
        }

        public PixelImage Apply(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels, byte? alternatePaletteShift, NativeBattlePcDirectTextureFormat? directTextureFormat, TexturePrimitiveSample sample)
        {
            Samples.Add(sample);
            return original;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Sprite_uses_current_viewport_fixed_UV_table_and_native_diffuse_alpha(ushort flags)
    {
        Capture capture = new();
        NativeBattlePrimitiveDrawMode mode = Mode() with { NormalViewport = new(2.5f, 1.5f, 0.3125f, 0.1875f, 3, 4.25f, -3.5f), ViewportInputs = new(800, 600, 0, 0) };
        NativeBattlePrimitiveFrame frame = Frame(Command(mode, flags: flags, u: 32, v: 64));
        new BattleRenderer(400, 300) { TriangleRenderer = capture }.RenderNativePrimitiveFrame(frame, Texture(31), capabilities: Capabilities());
        Assert.Equal(2, capture.Triangles.Count);
        TriangleVertex[] vertices = capture.Vertices;
        Assert.Equal(flags == 1 ? new Vector2(14.875f, 13.5f) : new Vector2(3.9375f, 0.375f), vertices[0].Position);
        Assert.Equal(20f, vertices.Max(vertex => vertex.Position.X) - vertices.Min(vertex => vertex.Position.X));
        Assert.Equal(6f, vertices.Max(vertex => vertex.Position.Y) - vertices.Min(vertex => vertex.Position.Y));
        Assert.Equal(new Vector2(32f / 256, 64f / 256), vertices[0].Uv);
        Assert.Equal(48f / 256, vertices.Max(vertex => vertex.Uv.X));
        Assert.Equal(72f / 256, vertices.Max(vertex => vertex.Uv.Y));
        Assert.All(vertices, vertex => Assert.Equal(127f / 255, vertex.Color.W));
    }

    [Fact]
    public void Signed_dimensions_reverse_geometry_and_UV_without_culling_the_sprite()
    {
        Capture capture = new();
        new BattleRenderer(320, 216) { TriangleRenderer = capture }.RenderNativePrimitiveFrame(Frame(Command(Mode(), x: 30, y: 40, width: -16, height: -8, u: 32, v: 64)), Texture(31), capabilities: Capabilities());
        Assert.Equal(2, capture.Triangles.Count);
        Assert.Equal(14f, capture.Vertices.Min(vertex => vertex.Position.X));
        Assert.Equal(30f, capture.Vertices.Max(vertex => vertex.Position.X));
        Assert.Equal(32f, capture.Vertices.Min(vertex => vertex.Position.Y));
        Assert.Equal(40f, capture.Vertices.Max(vertex => vertex.Position.Y));
        Assert.Equal(16f / 256, capture.Vertices.Min(vertex => vertex.Uv.X));
        Assert.Equal(56f / 256, capture.Vertices.Min(vertex => vertex.Uv.Y));
    }

    [Fact]
    public void UV_endpoint_256_is_preserved_and_uninitialized_table_endpoint_is_rejected()
    {
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        renderer.RenderNativePrimitiveFrame(Frame(Command(Mode(), width: 6, u: 250)), Texture(31), capabilities: Capabilities());
        Assert.Equal(1f, capture.Vertices.Max(vertex => vertex.Uv.X));
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(Frame(Command(Mode(), width: 16, u: 250)), Texture(31), capabilities: Capabilities()));
    }

    [Theory]
    [InlineData(0, 3, 2)]
    [InlineData(2, 0, 2)]
    [InlineData(2, 3, 4)]
    public void Opaque_filter_copy_requires_both_current_control_and_viewport_scale_flags(uint filter, uint scaleFlags, int count)
    {
        Capture capture = new();
        NativeBattlePrimitiveDrawMode mode = Mode() with { SpriteFilter = new(filter, uint.MaxValue), NormalViewport = new(2, 2, 0.25f, 0.25f, scaleFlags, -0.5f, -0.5f), ViewportInputs = new(640, 432, 0, 0) };
        new BattleRenderer(320, 216) { TriangleRenderer = capture }.RenderNativePrimitiveFrame(Frame(Command(mode)), Texture(31), capabilities: Capabilities());
        Assert.Equal(count, capture.Triangles.Count);
        Assert.Equal(TriangleBlend.Replace, capture.Triangles[0].Material.Blend);
        if (count == 4)
        {
            Assert.Equal(TriangleBlend.Alpha, capture.Triangles[2].Material.Blend);
            Assert.True(capture.Triangles[2].Material.DepthWrite);
            Assert.Same(capture.Triangles[0].Material.Texture, capture.Triangles[2].Material.Texture);
            Assert.Equal(capture.Triangles[0].A.Position + new Vector2(4), capture.Triangles[2].A.Position);
            Assert.Equal(capture.Triangles[0].A.Uv, capture.Triangles[2].A.Uv);
            Assert.Equal(capture.Triangles[0].A.Depth, capture.Triangles[2].A.Depth);
            Assert.Equal(capture.Triangles[0].A.Color, capture.Triangles[2].A.Color);
        }
    }

    [Fact]
    public void Filter_copy_adds_four_raw_units_before_low_coordinate_scaling()
    {
        Capture capture = new();
        NativeBattlePrimitiveDrawMode mode = Mode() with { SpriteFilter = new(2, uint.MaxValue), NormalViewport = new(2, 2, 0.25f, 0.25f, 3, -0.5f, -0.5f), ViewportInputs = new(640, 432, 0, 0) };
        new BattleRenderer(320, 216) { TriangleRenderer = capture }.RenderNativePrimitiveFrame(Frame(Command(mode, flags: 0, x: 80, y: 160)), Texture(31), capabilities: Capabilities());
        Assert.Equal(4, capture.Triangles.Count);
        Assert.Equal(capture.Triangles[0].A.Position + new Vector2(0.5f), capture.Triangles[2].A.Position);
    }

    [Theory]
    [InlineData(0x64, 1, 2)]
    [InlineData(0x64, 2, 0)]
    [InlineData(0x66, 1, 0)]
    [InlineData(0x66, 2, 2)]
    public void Sprite_opcode_selects_its_actual_pass_and_semi_never_submits_filter_copy(byte opcode, int pass, int count)
    {
        Capture capture = new();
        NativeBattlePrimitiveDrawMode mode = Mode() with { SpriteFilter = new(opcode == 0x66 ? 2u : 0u, uint.MaxValue), NormalViewport = new(1, 1, 0.125f, 0.125f, 3, -0.5f, -0.5f), ViewportInputs = new(320, 216, 0, 0) };
        new BattleRenderer(320, 216) { TriangleRenderer = capture }.RenderNativePrimitiveFrame(Frame(Command(mode, opcode: opcode, pass: pass)), Texture(31), capabilities: Capabilities());
        Assert.Equal(count, capture.Triangles.Count);
        if (count != 0)
        {
            Assert.Equal(opcode == 0x66 ? TriangleBlend.Alpha : TriangleBlend.Replace, capture.Triangles[0].Material.Blend);
            Assert.Equal(pass == 1, capture.Triangles[0].Material.DepthWrite);
            Assert.All(capture.Vertices, vertex => Assert.Equal(127f / 255, vertex.Color.W));
        }
    }

    [Fact]
    public void Perspective_sprite_repeats_first_proxy_depth_for_all_four_corners()
    {
        Capture capture = new();
        NativeBattleLogicalResolvedCommand command = Command(Mode());
        command = command with { Command = command.Command with { VertexPayload4 = [1000, 2000, 3000, 4000] } };
        NativeBattlePrimitiveFrame frame = Frame(command) with { LastDescriptorPerspective = true };
        new BattleRenderer(320, 216) { TriangleRenderer = capture }.RenderNativePrimitiveFrame(frame, Texture(31), capabilities: Capabilities());
        Assert.All(capture.Vertices, vertex =>
        {
            Assert.Equal(capture.Vertices[0].Depth, vertex.Depth);
            Assert.Equal(0.001f, vertex.ReciprocalW);
        });
    }

    [Fact]
    public void Sprite_reads_current_texture_pixels_and_JSON_frame_reproduces_the_next_render()
    {
        NativeBattlePrimitiveFrame frame = Frame(Command(Mode()));
        BattleRenderer renderer = new(320, 216);
        byte[] red = renderer.RenderNativePrimitiveFrame(frame, Texture(31), capabilities: Capabilities()).ToArray();
        byte[] green = renderer.RenderNativePrimitiveFrame(frame, Texture(0x3E0), capabilities: Capabilities()).ToArray();
        int offset = 4 * (23 * 320 + 13);
        Assert.True(red[offset] > red[offset + 1]);
        Assert.True(green[offset + 1] > green[offset]);
        NativeBattlePrimitiveFrame restored = JsonSerializer.Deserialize<NativeBattlePrimitiveFrame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!;
        Assert.Equal(green, new BattleRenderer(320, 216).RenderNativePrimitiveFrame(restored, Texture(0x3E0), capabilities: Capabilities()));
    }

    private static NativeBattleLogicalResolvedCommand Command(NativeBattlePrimitiveDrawMode mode, ushort flags = 1, short x = 10, short y = 20, short width = 16, short height = 8, byte u = 0, byte v = 0, byte opcode = 0x64, int pass = 1)
    {
        NativeBattleLogicalCommand command = new(new("sprite", 0, 0), 1, opcode, [(uint)opcode << 24 | 0x808080, Pair(x, y), (uint)(u | v << 8), Pair(width, height)], flags, [], null) { OrderingBucket = 50 };
        return new(command, mode, checked((byte)pass));
    }

    private static uint Pair(short x, short y) => unchecked((ushort)x | (uint)(ushort)y << 16);
    private static NativeBattlePrimitiveFrame Frame(NativeBattleLogicalResolvedCommand command) => new(NativeBattleLogicalOrdering.Clear(), [command], []) { LastDescriptorPerspective = false, LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(), LastColorState = NativeBattlePrimitiveColorState.FromColdProcess() };

    private static NativeBattlePrimitiveDrawMode Mode() => new(0x100)
    {
        TextureWindowBits = 0,
        TextureSelection = new(64, 0, 0, 0, 256, 256, 1f / 64, 1f / 32, false, false) { UsesRetainedPixelWords = false },
        SpriteFilter = new(0, uint.MaxValue),
        NormalViewport = new(1, 1, 0.125f, 0.125f, 0, -0.5f, -0.5f),
        ViewportInputs = new(320, 216, 0, 0)
    };

    private static NativeBattleEffectTextureState Texture(ushort color)
    {
        byte[] words = new byte[256 * 256 * 2];
        for (int offset = 0; offset < words.Length; offset += 2)
        {
            words[offset] = (byte)color;
            words[offset + 1] = (byte)(color >> 8);
        }
        return NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 256, 256, words);
    }

    private static NativeBattlePrimitiveCapabilities Capabilities() => NativeBattlePrimitiveCapabilities.PrimaryRgba8() with { DirectTextureFormat = NativeBattlePcDirectTextureFormat.Rgb555 };

    private sealed class Capture : ITriangleRenderer
    {
        public List<(TriangleVertex A, TriangleVertex B, TriangleVertex C, TriangleMaterial Material)> Triangles { get; } = [];
        public TriangleVertex[] Vertices => Triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }).ToArray();

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Triangles.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Triangles.Add((a, b, c, material));
        }

        public void End()
        {
        }
    }
}
