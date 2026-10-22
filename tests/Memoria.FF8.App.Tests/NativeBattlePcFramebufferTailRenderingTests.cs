using System.Numerics;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class NativeBattlePcFramebufferTailRenderingTests
{
    [Fact]
    public void Already_projected_native_packets_accept_a_coincident_camera_without_reprojecting_or_changing_pixels()
    {
        BattleRenderer renderer = new(8, 8);
        byte[] expected = renderer.RenderNativePrimitiveFrame(Frame(false), null, BattleCamera.NativeInitial).ToArray();
        BattleCamera coincident = new(new(0, 0, 24), new(0, 0, 24));
        byte[] actual = renderer.RenderNativePrimitiveFrame(Frame(false), null, coincident).ToArray();
        Assert.Equal(expected, actual);
        Assert.False(renderer.TryProject(Vector3.Zero, out _));
    }

    [Fact]
    public void Capture_occurs_after_ordinary_triangles_and_before_the_final_quad_and_End()
    {
        CaptureBackend backend = new() { Source = Enumerable.Repeat(0xFFFF0000u, 64).ToArray() };
        BattleRenderer renderer = new(8, 8) { TriangleRenderer = backend };
        renderer.RenderNativePrimitiveFrame(Frame(true), null, capabilities: Capabilities());
        Assert.Equal(new[] { "Begin", "Draw", "Draw", "Capture", "Draw", "Draw", "End" }, backend.Events);
        var tail = backend.Draws[2];
        Assert.Equal(new Vector2(-0.5f, -0.5f), tail.Vertex.Position);
        Assert.Equal(BitConverter.Int32BitsToSingle(0x3AC49BA6), tail.Vertex.Depth);
        Assert.Equal(TriangleBlend.Alpha, tail.Material.Blend);
        Assert.Equal(TriangleSampler.PointClamp, tail.Material.Sampler);
        Assert.False(tail.Material.DepthWrite);
        Assert.Equal(0f, tail.Material.MinimumAlpha);
        Assert.Equal((byte)85, tail.Material.Texture![0, 0].A);
        Assert.Equal((byte)255, tail.Material.Texture[0, 0].R);
        Assert.Equal((ushort)0x7C00, renderer.PcFramebufferCapture.RequirePixels()[0]);
    }

    [Fact]
    public void Expanded_target_captures_only_the_projection_crop_and_places_the_tail_in_that_same_crop()
    {
        uint[] source = Enumerable.Repeat(0xFF0000FFu, 16 * 12).ToArray();
        for (int y = 2; y < 10; y++)
        {
            for (int x = 4; x < 12; x++)
            {
                source[y * 16 + x] = x < 8 ? 0xFFFF0000u : 0xFF00FF00u;
            }
        }
        CaptureBackend backend = new() { Source = source };
        BattleRenderer renderer = new(16, 12, new PixelBounds(4, 2, 8, 8)) { TriangleRenderer = backend };
        renderer.RenderNativePrimitiveFrame(Frame(true), null, capabilities: Capabilities());
        var tail = backend.Draws[2];
        Assert.Equal(new Vector2(3.5f, 1.5f), tail.Vertex.Position);
        Assert.Equal(new Vector2(10.5f, 1.5f), backend.Draws[3].Vertex.Position);
        Assert.Equal(new Vector2(3.5f, 8.5f), tail.ThirdVertex.Position);
        Assert.Equal(new Vector2(10.5f, 8.5f), backend.Draws[3].SecondVertex.Position);
        Assert.Equal((ushort)0x7C00, renderer.PcFramebufferCapture.RequirePixels()[0]);
        Assert.Equal((ushort)0x03E0, renderer.PcFramebufferCapture.RequirePixels()[255]);
        Assert.Equal(tail.Material.Texture![0, 0], tail.Material.Texture[0, 255]);
        Assert.Equal((byte)255, tail.Material.Texture[0, 0].R);
        Assert.Equal((byte)255, tail.Material.Texture[255, 255].G);
        Assert.Equal((byte)0, tail.Material.Texture[255, 255].B);
    }

    [Fact]
    public void Failed_capture_reuses_previous_texture_and_unknown_first_texture_stays_unsupported()
    {
        CaptureBackend backend = new() { Source = Enumerable.Repeat(0xFFFF0000u, 64).ToArray() };
        BattleRenderer renderer = new(8, 8) { TriangleRenderer = backend };
        renderer.RenderNativePrimitiveFrame(Frame(true), null, capabilities: Capabilities());
        NativeBattlePcFramebufferCaptureState retained = renderer.PcFramebufferCapture;
        backend.LocksSucceeded = false;
        renderer.RenderNativePrimitiveFrame(Frame(true), null, capabilities: Capabilities());
        Assert.Same(retained, renderer.PcFramebufferCapture);
        Assert.Equal((byte)255, backend.Draws[2].Material.Texture![0, 0].R);
        BattleRenderer cold = new(8, 8) { TriangleRenderer = backend };
        Assert.Throws<NotSupportedException>(() => cold.RenderNativePrimitiveFrame(Frame(true), null, capabilities: Capabilities()));
        Assert.Throws<NotSupportedException>(() => new BattleRenderer(8, 8).RenderNativePrimitiveFrame(Frame(true), null));
    }

    [Fact]
    public void CPU_tail_captures_rendered_pixels_quantizes_WORDs_and_reprojects_the_256_texture()
    {
        BattleRenderer renderer = new(8, 8);
        byte[] before = renderer.RenderNativePrimitiveFrame(Frame(false), null).ToArray();
        byte[] after = renderer.RenderNativePrimitiveFrame(Frame(true), null, capabilities: Capabilities()).ToArray();
        Assert.Equal((byte)160, before[4 * (2 * 8 + 3)]);
        Assert.Equal((byte)106, after[4 * (2 * 8 + 3)]);
        Assert.Equal((byte)158, after[4 * (2 * 8 + 1)]);
        Assert.Equal((byte)255, after[4 * (2 * 8 + 3) + 3]);
        Assert.Equal((ushort)(19 << 10), renderer.PcFramebufferCapture.RequirePixels()[0]);
    }

    [Fact]
    public void GPU_capture_flushes_the_active_target_and_matches_the_CPU_tail_within_UNORM_rounding()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        BattleRenderer hardware = new(8, 8) { TriangleRenderer = gpu };
        BattleRenderer software = new(8, 8);
        byte[] expected = software.RenderNativePrimitiveFrame(Frame(true), null, capabilities: Capabilities()).ToArray();
        hardware.RenderNativePrimitiveFrame(Frame(true), null, capabilities: Capabilities());
        Color[] actual = GpuTestDevice.Read(gpu.Target);
        Assert.Equal(software.PcFramebufferCapture.RequirePixels().ToArray(), hardware.PcFramebufferCapture.RequirePixels().ToArray());
        for (int index = 0; index < actual.Length; index++)
        {
            Assert.InRange(Math.Abs(actual[index].R - expected[index * 4]), 0, 1);
            Assert.Equal(expected[index * 4 + 1], actual[index].G);
            Assert.Equal(expected[index * 4 + 2], actual[index].B);
            Assert.Equal(expected[index * 4 + 3], actual[index].A);
        }
    }

    private static NativeBattlePrimitiveCapabilities Capabilities() => NativeBattlePrimitiveCapabilities.PrimaryRgba8() with { PcFramebufferCaptureProfile = NativeBattlePcFramebufferCaptureProfile.PrimaryRgba8() with { SurfaceWidth = 8, SurfaceHeight = 8 } };

    private static NativeBattlePrimitiveFrame Frame(bool tail)
    {
        NativeBattleLogicalCommand tile = new(new("tile", 0, 0), 1, 0x60, [0x600000A0, 0, 160u | 216u << 16], 1, [], null) { OrderingBucket = 100 };
        return new(NativeBattleLogicalOrdering.Clear(), [new(tile, new(0), 1)], [])
        {
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess(),
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastPcRenderTail = tail ? new(1, new(0, 0, 8, 8)) : null
        };
    }

    private sealed class CaptureBackend : ITriangleRenderer, IFramebufferCaptureRenderer
    {
        public uint[] Source { get; init; } = [];
        public bool LocksSucceeded { get; set; } = true;
        public List<string> Events { get; } = [];
        public List<(TriangleVertex Vertex, TriangleVertex SecondVertex, TriangleVertex ThirdVertex, TriangleMaterial Material)> Draws { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Events.Clear();
            Draws.Clear();
            Events.Add("Begin");
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Events.Add("Draw");
            Draws.Add((a, b, c, material));
        }

        public bool TryCaptureArgb(out uint[] pixels)
        {
            Events.Add("Capture");
            pixels = LocksSucceeded ? Source.ToArray() : [];
            return LocksSucceeded;
        }

        public void End()
        {
            Events.Add("End");
        }
    }
}
