using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleNormalViewportRenderingTests
{
    [Theory]
    [InlineData(0x20, 0)]
    [InlineData(0x24, 0)]
    [InlineData(0x28, 0)]
    [InlineData(0x2C, 0)]
    [InlineData(0x30, 0)]
    [InlineData(0x34, 0)]
    [InlineData(0x38, 0)]
    [InlineData(0x3C, 0)]
    [InlineData(0x20, 1)]
    [InlineData(0x24, 1)]
    [InlineData(0x28, 1)]
    [InlineData(0x2C, 1)]
    [InlineData(0x30, 1)]
    [InlineData(0x34, 1)]
    [InlineData(0x38, 1)]
    [InlineData(0x3C, 1)]
    public void Every_polygon_leaf_uses_selected_stored_float_scales_before_portable_viewport_mapping(byte opcode, ushort flags)
    {
        Capture capture = new();
        BattleRenderer renderer = new(400, 300) { TriangleRenderer = capture };
        NativeBattlePrimitiveDrawMode mode = Mode() with { NormalViewport = new(2.5f, 1.5f, 0.3125f, 0.1875f, 3, 4.25f, -3.5f), ViewportInputs = new(800, 600, 0, 0) };
        renderer.RenderNativePrimitiveFrame(Frame(Command(opcode, flags, mode)), Texture(), capabilities: Capabilities());
        Assert.Equal((opcode & 8) != 0 ? 2 : 1, capture.Triangles.Count);
        Vector2 first = capture.Triangles[0].A.Position;
        Assert.Equal(flags == 1 ? new Vector2(14.875f, 13.5f) : new Vector2(3.9375f, 0.375f), first);
        Assert.All(capture.Triangles, triangle => Assert.True(float.IsFinite(triangle.A.Depth)));
    }

    [Fact]
    public void Polygon_coordinate_keeps_signed_WORD_and_stores_native_float_after_extended_multiply_add()
    {
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        NativeBattlePrimitiveDrawMode mode = new(0) { NormalViewport = new(1.0000001192092896f, 1, 0.125f, 0.125f, 0, -32759.75f, -0.5f), ViewportInputs = new(320, 216, 0, 0) };
        renderer.RenderNativePrimitiveFrame(Frame(Command(0x20, 1, mode, 32760, 20, 4)), null);
        Assert.Equal(0.7539052963256836f, capture.Triangles[0].A.Position.X);
        Assert.NotEqual(0.75390625f, capture.Triangles[0].A.Position.X);
        mode = mode with { NormalViewport = new(2.5f, 1.5f, 0.3125f, 0.1875f, 0, 4.25f, -3.5f), ViewportInputs = new(800, 600, 0, 0) };
        renderer.RenderNativePrimitiveFrame(Frame(Command(0x20, 1, mode, -10, 20)), null);
        Assert.Equal(-8.1f, capture.Triangles[0].A.Position.X);
    }

    [Fact]
    public void Default_native_metadata_preserves_legacy_polygon_pixels_and_json_next_render()
    {
        NativeBattleLogicalResolvedCommand legacy = Command(0x28, 1, new(0));
        NativeBattleLogicalResolvedCommand current = legacy with { DrawMode = legacy.DrawMode with { NormalViewport = new(1, 1, 0.125f, 0.125f, 0, -0.5f, -0.5f), ViewportInputs = new(320, 216, 0, 0) } };
        BattleRenderer renderer = new(640, 432);
        byte[] oldPixels = renderer.RenderNativePrimitiveFrame(Frame(legacy), null).ToArray();
        byte[] pixels = renderer.RenderNativePrimitiveFrame(Frame(current), null).ToArray();
        Assert.Equal(oldPixels, pixels);
        NativeBattlePrimitiveFrame cold = JsonSerializer.Deserialize<NativeBattlePrimitiveFrame>(JsonSerializer.Serialize(Frame(current)))!;
        Assert.Equal(pixels, new BattleRenderer(640, 432).RenderNativePrimitiveFrame(cold, null));
    }

    [Fact]
    public void Nondefault_polygon_offset_moves_cpu_pixels_while_TILE_keeps_its_existing_path()
    {
        NativeBattlePrimitiveDrawMode mode = new(0) { NormalViewport = new(1, 1, 0.125f, 0.125f, 0, 19.5f, -0.5f), ViewportInputs = new(320, 216, 0, 0) };
        BattleRenderer renderer = new(320, 216);
        byte[] moved = renderer.RenderNativePrimitiveFrame(Frame(Command(0x28, 1, mode)), null).ToArray();
        byte[] original = renderer.RenderNativePrimitiveFrame(Frame(Command(0x28, 1, new(0))), null).ToArray();
        Assert.Equal(0, moved[4 * (25 * 320 + 15)]);
        Assert.True(original[4 * (25 * 320 + 15)] > 0);
        Assert.True(moved[4 * (25 * 320 + 35)] > 0);
        Assert.Equal(0, original[4 * (25 * 320 + 35)]);
        NativeBattleLogicalCommand tile = new(new("tile", 0, 0), 1, 0x60, [0x60808080, Point(10, 20), Point(20, 20)], 1, [], null) { OrderingBucket = 50 };
        byte[] tileBefore = renderer.RenderNativePrimitiveFrame(Frame(new(tile, new(0), 1)), null).ToArray();
        byte[] tileAfter = renderer.RenderNativePrimitiveFrame(Frame(new(tile, mode, 1)), null).ToArray();
        Assert.Equal(tileBefore, tileAfter);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Partial_viewport_metadata_fails_instead_of_silently_using_default_coordinates(bool derivedOnly)
    {
        NativeBattlePrimitiveDrawMode mode = derivedOnly ? new(0) { NormalViewport = new(1, 1, 0.125f, 0.125f, 0, -0.5f, -0.5f) } : new(0) { ViewportInputs = new(320, 216, 0, 0) };
        Assert.Throws<NotSupportedException>(() => new BattleRenderer(320, 216).RenderNativePrimitiveFrame(Frame(Command(0x20, 1, mode)), null));
        mode = new(0) { NormalViewport = new(1, 1, 0.125f, 0.125f, 0, -0.5f, -0.5f), ViewportInputs = new(0, 216, 0, 0) };
        Assert.Throws<InvalidDataException>(() => new BattleRenderer(320, 216).RenderNativePrimitiveFrame(Frame(Command(0x20, 1, mode)), null));
    }

    private static NativeBattleLogicalResolvedCommand Command(byte opcode, ushort flags, NativeBattlePrimitiveDrawMode mode, short x = 10, short y = 20, short extent = 20)
    {
        bool textured = (opcode & 4) != 0;
        bool gouraud = (opcode & 16) != 0;
        bool quad = (opcode & 8) != 0;
        int count = quad ? 4 : 3;
        ImmutableArray<uint>.Builder words = ImmutableArray.CreateBuilder<uint>();
        for (int corner = 0; corner < count; corner++)
        {
            if (corner == 0 || gouraud)
            {
                words.Add((corner == 0 ? (uint)opcode << 24 : 0) | 0x808080);
            }
            words.Add(Point(unchecked((short)(x + ((corner & 1) != 0 ? extent : 0))), unchecked((short)(y + (corner >= 2 ? extent : 0)))));
            if (textured)
            {
                words.Add(corner == 1 ? 0x1000000u : 0);
            }
        }
        NativeBattleLogicalCommand command = new(new("polygon", 0, 0), 1, opcode, words.ToImmutable(), flags, [], null) { OrderingBucket = 50 };
        return new(command, mode, 1);
    }

    private static NativeBattlePrimitiveFrame Frame(NativeBattleLogicalResolvedCommand command) => new(NativeBattleLogicalOrdering.Clear(), [command], []) { LastDescriptorPerspective = false, LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(), LastColorState = NativeBattlePrimitiveColorState.FromColdProcess() };
    private static uint Point(short x, short y) => unchecked((ushort)x | ((uint)(ushort)y << 16));
    private static NativeBattlePrimitiveDrawMode Mode()
    {
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null).Preprocess462F90(0x100, 0).Prepare464BD0().Cache;
        return new(0x100) { TextureWindowBits = 0, TextureSelection = cache.Resolve465CE0(0x100, 0, 0).Selection };
    }
    private static NativeBattleEffectTextureState Texture() => NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, [31, 0])] };
    private static NativeBattlePrimitiveCapabilities Capabilities() => NativeBattlePrimitiveCapabilities.PrimaryRgba8() with { DirectTextureFormat = NativeBattlePcDirectTextureFormat.Rgb555 };

    private sealed class Capture : ITriangleRenderer
    {
        public List<(TriangleVertex A, TriangleVertex B, TriangleVertex C)> Triangles { get; } = [];
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Triangles.Clear();
        }
        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Triangles.Add((a, b, c));
        }
        public void End()
        {
        }
    }
}
