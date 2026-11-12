using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverForeignModelRenderingTests
{
    [Fact]
    public void NativeBodyOrderingUsesTheHostsActualTextureUvColorsAndMaterialWithoutNativeVram()
    {
        PixelImage texture = new(2, 2);
        TriangleMaterial original = new(texture, TriangleBlend.Replace, TriangleDepthComparison.GreaterEqual, true, 0.25f, TriangleSampler.LinearWrap) { PreserveTargetAlpha = false, TextureSelectsAlpha = false };
        NativeBattleForeignSurface surface = new(new NativeBattleForeignRenderMaterial(original, true), [new(0.1f, 0.2f), new(0.8f, 0.3f), new(0.4f, 0.9f)], [Vector4.One, new(1, 0, 0, 1), new(0, 1, 0, 1)]);
        NativeBattleLogicalCommand command = new(new("host-body", 0, 0), 1, 0x24, [0x24808080, Point(10, 10), 0, Point(30, 10), 0, Point(10, 30), 0], 1, [], null)
        {
            OrderingBucket = 100,
            PilotGeometry = new(new(1, 0, 0, 1, 0), default, default, default, default, 3) { ForeignSurface = surface }
        };
        NativeBattlePrimitiveFrame frame = new(NativeBattleLogicalOrdering.Clear(), [new(command, new(null), 1)], [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
        CaptureRenderer captured = new();
        BattleRenderer renderer = new(320, 240) { TriangleRenderer = captured };
        renderer.RenderNativePrimitiveFrame(frame, null);
        Assert.Equal(1, renderer.DrawnTriangles);
        Assert.Equal(original, captured.Material);
        Assert.Equal(surface.Uv[0], captured.Vertices[0].Uv);
        Assert.Equal(surface.Uv[1], captured.Vertices[1].Uv);
        Assert.Equal(surface.Uv[2], captured.Vertices[2].Uv);
        Assert.Equal(surface.Colors[0], captured.Vertices[0].Color);
        Assert.Equal(surface.Colors[1], captured.Vertices[1].Color);
        Assert.Equal(surface.Colors[2], captured.Vertices[2].Color);
        Assert.Equal(new Vector2(10, 10 * 240f / 216), captured.Vertices[0].Position);
    }

    private static uint Point(short x, short y) => unchecked((ushort)x | (uint)(ushort)y << 16);

    private sealed class CaptureRenderer : ITriangleRenderer
    {
        public ImmutableArray<TriangleVertex> Vertices { get; private set; } = [];
        public TriangleMaterial Material { get; private set; }

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices = [a, b, c];
            Material = material;
        }

        public void End()
        {
        }
    }
}
