using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossoverArenaSurfaceCaptureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeferredArenaCaptureUsesItsViewOriginalTextureVertexColorsAndDepthThenSamplesTheRetainedImage(bool additive)
    {
        using GpuTestDevice device = new();
        int created = 0;
        GpuTriangleRenderer? offscreen = null;
        using CrossoverArenaSurfaceCapture capture = new(() =>
        {
            created++;
            offscreen = new(device.Device);
            return offscreen;
        });
        NativeBattleArenaCaptureMaterial request = Request();
        Assert.Equal(0, created);
        capture.Prepare([request]);
        NativeBattleForeignRenderMaterial material = capture.Resolve(request);
        Assert.Equal(1, created);
        Assert.Same(material, capture.Resolve(request));
        Assert.Equal(1, capture.RetainedImages);
        Assert.Equal(32 * 32 * 4, capture.RetainedImageBytes);
        PixelImage image = Assert.IsType<PixelImage>(material.Material.Texture);
        AssertRgb(image[4, 4], 0, 255, 255);
        AssertRgb(image[24, 4], 0, 255, 0);
        AssertRgb(image[4, 24], 0, 0, 255);
        Rgba32 shaded = image[24, 24];
        Assert.InRange(shaded.R, (byte)127, (byte)128);
        Assert.Equal(shaded.R, shaded.G);
        Assert.Equal((byte)0, shaded.B);

        using GpuTriangleRenderer display = new(device.Device);
        NativeBattleLogicalCommand command = new(new("arena-sample", 0, 0), 1, 0x2C, [0x2C808080, 0, 0, 320, 0, 216u << 16, 0, 320u | 216u << 16, 0], 1, [], null)
        {
            OrderingBucket = 100,
            PilotGeometry = new(new(0, 0, 0, 1, 0), default, default, default, default, 4)
            {
                ForeignSurface = new(request, [Vector2.Zero, Vector2.UnitX, Vector2.UnitY, Vector2.One], [Vector4.One, Vector4.One, Vector4.One, Vector4.One])
            }
        };
        if (additive)
        {
            command = command with { Opcode = 0x2E, Words = command.Words.SetItem(0, 0x2E808080) };
        }
        NativeBattlePreparedPrimitiveFrame frame = new([new(command, new(additive ? (ushort)32 : (ushort)0), additive ? (byte)2 : (byte)1)], [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
        HostPass host = new(display);
        BattleRenderer renderer = new(32, 32)
        {
            TriangleRenderer = host,
            ForeignMaterialResolver = capture.Resolve
        };
        display.Begin(32, 32, new Vector4(0, 0, 0, 1), 0);
        renderer.RenderNativePrimitiveFrame(frame, null);
        Assert.Equal(2, renderer.DrawnTriangles);
        Assert.Equal(additive ? TriangleBlend.Add : TriangleBlend.Replace, host.Material.Blend);
        Assert.Equal(!additive, host.Material.DepthWrite);
        Assert.True(display.TryCaptureArgb(out uint[] sampled));
        display.End();
        Assert.Equal(0xFF00FFFFu, sampled[4 * 32 + 4]);
        Assert.Equal(0xFF00FF00u, sampled[4 * 32 + 24]);
        Assert.Equal(0xFF0000FFu, sampled[24 * 32 + 4]);
        Assert.Equal(0xFF000000u | (uint)shaded.R << 16 | (uint)shaded.G << 8, sampled[24 * 32 + 24]);
        RenderTarget2D target = offscreen!.Target;
        capture.Dispose();
        Assert.True(target.IsDisposed);
        Assert.Equal(0, capture.RetainedImages);
        Assert.Throws<ObjectDisposedException>(() => capture.Resolve(request));
        AssertRgb(image[4, 4], 0, 255, 255);
    }

    [Fact]
    public void CaptureProjectionOffsetsCropTheSurfaceWithoutChangingTheRetainedEarlierImage()
    {
        using GpuTestDevice device = new();
        using CrossoverArenaSurfaceCapture capture = new(() => new GpuTriangleRenderer(device.Device));
        NativeBattleArenaCaptureMaterial original = Request();
        PixelImage first = capture.Resolve(original).Material.Texture!;
        NativeBattleArenaCaptureMaterial cropped = original with { Projection = original.Projection with { OffsetX = 0 } };
        PixelImage second = capture.Resolve(cropped).Material.Texture!;
        AssertRgb(second[8, 4], 0, 255, 0);
        AssertRgb(second[24, 4], 0, 0, 0);
        AssertRgb(first[24, 4], 0, 255, 0);
        Assert.Equal((short)16, original.Projection.OffsetX);
        Assert.Equal(2, capture.RetainedImages);
    }

    private static NativeBattleArenaCaptureMaterial Request()
    {
        PixelImage texture = new(2, 2, [new(255, 0, 0, 255), new(0, 255, 0, 255), new(0, 0, 255, 255), new(255, 255, 0, 255)]);
        NativeBattleForeignRenderMaterial textured = new(new(texture, DepthComparison: TriangleDepthComparison.GreaterEqual, Sampler: TriangleSampler.PointClamp), true);
        NativeBattleForeignRenderMaterial solid = new(new(null, DepthComparison: TriangleDepthComparison.GreaterEqual), true);
        NativeBattleArenaFace near = new(new(0, 0, 0), new(-8, -8, 8), new(0, -8, 8), new(-8, 0, 8), new(solid, [Vector2.Zero, Vector2.Zero, Vector2.Zero], [new(0, 1, 1, 1), new(0, 1, 1, 1), new(0, 1, 1, 1)]));
        NativeBattleForeignSurface Surface(Vector2 a, Vector2 b, Vector2 c, Vector4 color) => new(textured, [a, b, c], [color, color, color]);
        NativeBattleArenaFace first = new(new(0, 0, 1), new(-16, -16, 16), new(16, -16, 16), new(-16, 16, 16), Surface(Vector2.Zero, Vector2.UnitX, Vector2.UnitY, Vector4.One));
        NativeBattleArenaFace second = new(new(0, 0, 2), new(16, -16, 16), new(16, 16, 16), new(-16, 16, 16), Surface(Vector2.UnitX, Vector2.One, Vector2.UnitY, new(0.5f, 0.5f, 0.5f, 1)));
        return new(new([near, first, second]), new(NativeBattleForeignModelGeometry.Identity, 16, 16, 16), 32, 32);
    }

    private static void AssertRgb(Rgba32 actual, byte red, byte green, byte blue)
    {
        Assert.Equal(red, actual.R);
        Assert.Equal(green, actual.G);
        Assert.Equal(blue, actual.B);
    }

    private sealed class HostPass(ITriangleRenderer target) : ITriangleRenderer
    {
        public TriangleMaterial Material { get; private set; }

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Material = material with { DepthComparison = TriangleDepthComparison.GreaterEqual };
            target.Draw(a, b, c, Material);
        }

        public void End()
        {
        }
    }
}
