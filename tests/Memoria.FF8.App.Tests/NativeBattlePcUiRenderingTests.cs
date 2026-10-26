using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattlePcUiRenderingTests
{
    [Fact]
    public void Ui_opaque_precedes_world_and_semi_follows_world_without_writing_depth()
    {
        Capture backend = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = backend };
        renderer.RenderNativePrimitiveFrame(World(), null, uiFrame: Ui(), uiTexture: Texture);
        Assert.Equal(6, backend.Materials.Count);
        Assert.False(backend.Materials[0].DepthWrite);
        Assert.Equal(TriangleBlend.Replace, backend.Materials[0].Blend);
        Assert.True(backend.Materials[2].DepthWrite);
        Assert.False(backend.Materials[4].DepthWrite);
        Assert.Equal(TriangleBlend.Alpha, backend.Materials[4].Blend);
        foreach (int index in new[] { 0, 1, 4, 5 })
        {
            Assert.Equal(TriangleSampler.PointWrap, backend.Materials[index].Sampler);
            Assert.Equal(TriangleDepthComparison.LessEqual, backend.Materials[index].DepthComparison);
            Assert.Equal(1f / 255, backend.Materials[index].MinimumAlpha);
            Assert.True(backend.Materials[index].TextureSelectsAlpha);
        }
    }

    [Fact]
    public void Cpu_overlap_proves_opaque_ui_did_not_occlude_world_depth_and_semi_is_last()
    {
        BattleRenderer renderer = new(320, 216);
        byte[] pixels = renderer.RenderNativePrimitiveFrame(World(), null, uiFrame: Ui(), uiTexture: Texture);
        int offset = (20 * 320 + 20) * 4;
        Assert.Equal((byte)0, pixels[offset]);
        Assert.InRange(pixels[offset + 1], (byte)127, (byte)128);
        Assert.Equal((byte)127, pixels[offset + 2]);
        Assert.Equal((byte)255, pixels[offset + 3]);
    }

    [Fact]
    public void Ui_viewport_clips_geometry_and_uvs_before_the_shared_rasterizer()
    {
        NativeBattlePcUiFrame ui = Ui();
        NativeBattlePcUiQuad quad = ui.Opaque[0] with { Viewport = new(20, 10, 10, 20) };
        ui = ui with { Opaque = [quad], Semi = [] };
        Capture backend = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = backend };
        renderer.RenderNativePrimitiveFrame(Empty(), null, uiFrame: ui, uiTexture: Texture);
        Assert.Equal(2, backend.Vertices.Count);
        Assert.Equal(20, backend.Vertices[0].Position.X);
        Assert.Equal(0.5f, backend.Vertices[0].Uv.X);
    }

    private static PixelImage Texture(int page, int palette, bool semi)
    {
        return new(1, 1, [semi ? new(0, 0, 255, 127) : new Rgba32(255, 0, 0, 255)]);
    }

    private static NativeBattlePcUiFrame Ui()
    {
        NativeBattlePcUiQuad quad = new(0, 0, 0x7FFFFFFF,
        [
            new(9.5f, 9.5f, 0.001f, 0, 0),
            new(9.5f, 29.5f, 0.001f, 0, 1),
            new(29.5f, 9.5f, 0.001f, 1, 0),
            new(29.5f, 29.5f, 0.001f, 1, 1)
        ], new(0, 0, 320, 216), 0);
        return new([quad], [new(quad.Viewport, 0, [quad])], [], 320, 216);
    }

    private static NativeBattlePrimitiveFrame World()
    {
        static uint Point(int x, int y) => (ushort)x | unchecked((uint)y << 16);
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, 0x28, [0x2800FF00, Point(10, 10), Point(30, 10), Point(10, 30), Point(30, 30)], 1, [], null) { OrderingBucket = 50 };
        return Empty() with { LastCommands = [new(command, new(0), 1)] };
    }

    private static NativeBattlePrimitiveFrame Empty()
    {
        return new(NativeBattleLogicalOrdering.Clear(), [], []) { LastDescriptorPerspective = false, LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(), LastColorState = NativeBattlePrimitiveColorState.FromColdProcess() };
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<TriangleMaterial> Materials { get; } = [];
        public List<TriangleVertex> Vertices { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Materials.Clear();
            Vertices.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Materials.Add(material);
            Vertices.Add(a);
        }

        public void End()
        {
        }
    }
}
