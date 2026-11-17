using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class GpuTriangleRendererTests
{
    [Fact]
    public void Software_rendering_requires_an_explicit_diagnostic_option()
    {
        Assert.False(LaunchOptions.Parse([]).SoftwareRenderer);
        Assert.False(LaunchOptions.Parse(["--control-dir", Path.GetTempPath(), "--mute-audio"]).SoftwareRenderer);
        Assert.True(LaunchOptions.Parse(["--software-renderer"]).SoftwareRenderer);
    }

    [Theory]
    [InlineData(TriangleBlend.Replace, 100)]
    [InlineData(TriangleBlend.Alpha, 70)]
    [InlineData(TriangleBlend.Add, 140)]
    [InlineData(TriangleBlend.AlphaAdd, 90)]
    [InlineData(TriangleBlend.HalfAdd, 70)]
    [InlineData(TriangleBlend.Subtract, 0)]
    [InlineData(TriangleBlend.QuarterAdd, 65)]
    public void Hardware_blend_equations_preserve_destination_and_alpha(TriangleBlend blend, int expected)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        renderer.Begin(16, 16, new Vector4(40f / 255, 40f / 255, 40f / 255, 1));
        Quad(renderer, new(null, blend), new Vector4(100f / 255, 100f / 255, 100f / 255, 0.5f));
        renderer.End();
        foreach (Color pixel in GpuTestDevice.Read(renderer.Target))
        {
            Assert.InRange((int)pixel.R, expected - 1, expected + 1);
            Assert.Equal(pixel.R, pixel.G);
            Assert.Equal(pixel.R, pixel.B);
            Assert.Equal(255, pixel.A);
        }
    }

    [Fact]
    public void Transparent_texels_discard_before_depth_and_translucency_does_not_write_depth()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        PixelImage transparent = new(1, 1, [new Rgba32(255, 0, 0, 0)]);
        renderer.Begin(16, 16, new Vector4(0, 0, 0, 1));
        Quad(renderer, new(transparent), Vector4.One, 0.1f);
        Quad(renderer, new(null, TriangleBlend.Alpha, DepthWrite: false), new Vector4(1, 0, 0, 0.5f), 0.2f);
        Quad(renderer, new(null), new Vector4(0, 1, 0, 1), 0.8f);
        renderer.End();
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(Color.Lime, pixel));
    }

    [Fact]
    public void Ordered_material_runs_share_an_upload_and_readback_splits_the_stream()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device) { ObserveTriangleBatches = true };
        PixelImage red = new(1, 1, [new Rgba32(128, 0, 0, 255)]);
        PixelImage green = new(1, 1, [new Rgba32(0, 64, 0, 255)]);
        renderer.Begin(16, 16, new Vector4(0, 0, 0, 1));
        Quad(renderer, new(red), Vector4.One);
        Quad(renderer, new(green, TriangleBlend.Add), Vector4.One);
        Assert.True(renderer.TryCaptureArgb(out uint[] capture));
        Assert.All(capture, pixel => Assert.Equal(0xFF804000u, pixel));
        Assert.Equal(1, renderer.TriangleBatchMetrics.VertexUploads);
        Assert.Equal(2, renderer.TriangleBatchMetrics.Draws);
        Assert.Equal(1, renderer.TriangleBatchMetrics.MaterialChanges);
        Assert.Equal(1, renderer.TriangleBatchMetrics.Capture);

        Quad(renderer, new(null, TriangleBlend.Add), new Vector4(0, 0, 32f / 255, 1));
        renderer.End();
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(new Color(128, 64, 32, 255), pixel));
        Assert.Equal(2, renderer.TriangleBatchMetrics.VertexUploads);
        Assert.Equal(3, renderer.TriangleBatchMetrics.Draws);
        Assert.Equal(1, renderer.TriangleBatchMetrics.End);
        Assert.Equal(1, renderer.PresentationVersion);
    }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(9, 2)]
    public void Ordered_static_texture_slots_share_a_draw_until_the_ninth_image(int count, int expectedDraws)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device) { ObserveTriangleBatches = true };
        renderer.Begin(16, 16, new Vector4(0, 0, 0, 1));
        for (int index = 0; index < count; index++)
        {
            PixelImage image = new(1, 1, [new Rgba32((byte)(20 * index), 0, 0)]);
            Quad(renderer, new(image), Vector4.One);
        }
        renderer.End();
        Assert.Equal(expectedDraws, renderer.FrameDrawCalls);
        Assert.Equal(count == 9 ? 1 : 0, renderer.TriangleBatchMetrics.TextureCapacity);
        Assert.Equal(1, renderer.TriangleBatchMetrics.VertexUploads);
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(new Color(20 * (count - 1), 0, 0, 255), pixel));
    }

    [Theory]
    [InlineData(TriangleSampler.PointWrap)]
    [InlineData(TriangleSampler.CutoutLinearClamp)]
    public void Mixed_texture_dimensions_and_sampling_match_ordered_single_binding_submissions(TriangleSampler sampler)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer batched = new(device.Device);
        using GpuTriangleRenderer separate = new(device.Device);
        PixelImage[] images = Enumerable.Range(0, 8).Select(index =>
        {
            int width = 1 << (index % 3);
            int height = 1 << (index % 2);
            return new PixelImage(width, height, Enumerable.Range(0, width * height).Select(pixel => new Rgba32((byte)(16 * index), (byte)(32 * pixel), (byte)(255 - 16 * index), pixel % 3 == 0 ? (byte)0 : (byte)255)).ToArray());
        }).ToArray();
        Render(batched, false);
        Render(separate, true);
        Assert.Equal(1, batched.FrameDrawCalls);
        Assert.Equal(8, separate.FrameDrawCalls);
        Assert.Equal(GpuTestDevice.Read(separate.Target), GpuTestDevice.Read(batched.Target));

        void Render(GpuTriangleRenderer renderer, bool capture)
        {
            renderer.Begin(16, 16, new Vector4(0, 0, 0, 1));
            for (int index = 0; index < images.Length; index++)
            {
                TriangleMaterial material = new(images[index]) { Sampler = sampler };
                float left = index * 2;
                TriangleVertex a = new(new(left, 0), 0.5f, 1, new(-0.25f, 0), Vector4.One);
                TriangleVertex b = new(new(left + 2, 0), 0.5f, 1, new(1.25f, 0), Vector4.One);
                TriangleVertex c = new(new(left, 16), 0.5f, 1, new(-0.25f, 1), Vector4.One);
                TriangleVertex d = new(new(left + 2, 16), 0.5f, 1, new(1.25f, 1), Vector4.One);
                renderer.Draw(a, b, c, material);
                renderer.Draw(b, d, c, material);
                if (capture)
                {
                    Assert.True(renderer.TryCaptureArgb(out _));
                }
            }
            renderer.End();
        }
    }

    [Fact]
    public void Eight_overlapping_partial_alpha_textures_match_forced_single_binding_order()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer batched = new(device.Device);
        using GpuTriangleRenderer separate = new(device.Device);
        PixelImage[] images = Enumerable.Range(0, 8)
            .Select(index => new PixelImage(1, 1, [new Rgba32((byte)(80 + index * 20), (byte)(240 - index * 20), (byte)(index % 2 == 0 ? 100 : 200), 32)]))
            .ToArray();
        Render(batched, false);
        Render(separate, true);
        Color[] actual = GpuTestDevice.Read(batched.Target);
        Assert.Equal(GpuTestDevice.Read(separate.Target), actual);
        Assert.Equal(1, batched.FrameDrawCalls);
        Assert.Equal(8, separate.FrameDrawCalls);

        Vector3 expected = new(17, 29, 43);
        foreach (PixelImage image in images)
        {
            Rgba32 pixel = image.Pixels[0];
            expected = Vector3.Lerp(expected, new(pixel.R, pixel.G, pixel.B), pixel.A / 255f);
            expected = new(MathF.Round(expected.X), MathF.Round(expected.Y), MathF.Round(expected.Z));
        }
        Assert.All(actual, pixel =>
        {
            Assert.InRange(pixel.R, (int)expected.X - 2, (int)expected.X + 2);
            Assert.InRange(pixel.G, (int)expected.Y - 2, (int)expected.Y + 2);
            Assert.InRange(pixel.B, (int)expected.Z - 2, (int)expected.Z + 2);
            Assert.Equal(255, pixel.A);
        });

        void Render(GpuTriangleRenderer renderer, bool capture)
        {
            renderer.Begin(16, 16, new Vector4(17 / 255f, 29 / 255f, 43 / 255f, 1));
            foreach (PixelImage image in images)
            {
                Quad(renderer, new(image, TriangleBlend.Alpha, DepthWrite: false), Vector4.One);
                if (capture)
                {
                    Assert.True(renderer.TryCaptureArgb(out _));
                }
            }
            renderer.End();
        }
    }

    [Theory]
    [InlineData(1, 1024)]
    [InlineData(8, 4)]
    [InlineData(8, 1)]
    public void Texture_binding_capacity_respects_cache_limits_and_transient_images(int entries, long bytes)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device, entries, bytes);
        renderer.Begin(16, 16, new Vector4(0, 0, 0, 1));
        for (int index = 0; index < 3; index++)
        {
            Quad(renderer, new(new PixelImage(1, 1, [new Rgba32((byte)(index * 100), 0, 0)])), Vector4.One);
        }
        renderer.End();
        Assert.Equal(3, renderer.FrameDrawCalls);
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(new Color(200, 0, 0, 255), pixel));
        for (int slot = 0; slot < 8; slot++)
        {
            Assert.Null(device.Device.Textures[slot]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Texture_stream_abort_releases_pending_images_and_restores_all_nine_caller_bindings(bool submit)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        using Texture2D existing = new(device.Device, 1, 1);
        for (int slot = 0; slot < 9; slot++)
        {
            device.Device.Textures[slot] = existing;
            device.Device.SamplerStates[slot] = SamplerState.PointClamp;
        }
        renderer.Begin(16, 16, Vector4.Zero);
        for (int index = 0; index < 8; index++)
        {
            Quad(renderer, new(new PixelImage(1, 1)), Vector4.One);
        }
        if (submit)
        {
            Assert.True(renderer.TryCaptureArgb(out _));
        }
        renderer.Abort();
        for (int slot = 0; slot < 9; slot++)
        {
            Assert.Same(existing, device.Device.Textures[slot]);
            Assert.Same(SamplerState.PointClamp, device.Device.SamplerStates[slot]);
        }
        Array retained = (Array)typeof(GpuTriangleRenderer).GetField("bindings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        Assert.All(retained.Cast<object?>(), image => Assert.Null(image));
    }

    [Fact]
    public void Texture_batching_keeps_every_other_material_field_as_a_boundary()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        TriangleMaterial basis = new(new PixelImage(1, 1, [new Rgba32(255, 255, 255)]));
        TriangleMaterial[] variants =
        [
            basis with { Blend = TriangleBlend.Add },
            basis with { DepthComparison = TriangleDepthComparison.Always },
            basis with { DepthWrite = false },
            basis with { MinimumAlpha = 0 },
            basis with { Sampler = TriangleSampler.PointClamp },
            basis with { DynamicTexture = true },
            basis with { TextureSelectsAlpha = false },
            basis with { MaximumAlpha = 0.75f },
            basis with { AlphaMultiplier = 0.5f },
            basis with { ActorOpacity = 0.5f },
            basis with { ActorOpacityReplacesAlpha = true },
            basis with { ColorWrite = false },
            basis with { PreserveTargetAlpha = false },
            basis with { PerspectiveDepth = true },
            basis with { CentroidTextureSampling = true },
            basis with { ClipRectangle = new Vector4(0, 0, 8, 16) },
            basis with { DepthMapping = TriangleDepthMapping.Positive },
            basis with { DepthScale = 0.5f },
            basis with { FogColor = Vector3.One },
            basis with { FogStart = 1 },
            basis with { FogEnd = 10 },
            basis with { MinimumReciprocalW = 0.5f },
            basis with { FieldLighting = FieldLightingPass.Model }
        ];
        foreach (TriangleMaterial variant in variants)
        {
            renderer.Begin(16, 16, new Vector4(0, 0, 0, 1));
            Quad(renderer, basis, Vector4.One);
            Quad(renderer, variant, Vector4.One);
            Quad(renderer, basis, Vector4.One);
            renderer.End();
            Assert.Equal(3, renderer.FrameDrawCalls);
        }
    }

    [Fact]
    public void Multi_texture_batches_keep_line_capture_and_invalidation_boundaries()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device) { ObserveTriangleBatches = true };
        PixelImage red = new(1, 1, [new Rgba32(255, 0, 0)]);
        PixelImage green = new(1, 1, [new Rgba32(0, 255, 0)]);
        renderer.Begin(16, 16, new Vector4(0, 0, 0, 1));
        Quad(renderer, new(red), Vector4.One);
        Quad(renderer, new(green), Vector4.One);
        TriangleVertex a = new(new(0, 0), 0.1f, 1, Vector2.Zero, Vector4.One);
        renderer.DrawLine(a, a with { Position = new(16, 16) }, new(red));
        Assert.True(renderer.TryCaptureArgb(out _));
        Assert.Equal(2, renderer.FrameDrawCalls);
        Assert.Equal(1, renderer.TriangleBatchMetrics.PrimitiveKindChanges);
        green.Pixels[0] = new(0, 0, 255);
        renderer.InvalidateTexture(green);
        Quad(renderer, new(green), Vector4.One, 0);
        renderer.End();
        Assert.Equal(3, renderer.FrameDrawCalls);
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(Color.Blue, pixel));
    }

    [Fact]
    public void Dynamic_image_is_sampled_before_later_mutation_at_a_material_boundary()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device) { ObserveTriangleBatches = true };
        PixelImage image = new(1, 1, [new Rgba32(255, 0, 0, 255)]);
        TriangleMaterial dynamicMaterial = new(image) { DynamicTexture = true };
        renderer.Begin(16, 16, new Vector4(0, 0, 0, 1));
        Quad(renderer, dynamicMaterial, Vector4.One, 0.25f);
        Quad(renderer, new(null), Vector4.One, 0.75f);
        image.Pixels[0] = new Rgba32(0, 0, 255, 255);
        renderer.End();
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(Color.Red, pixel));
        Assert.Equal(2, renderer.TriangleBatchMetrics.VertexUploads);

        renderer.Begin(16, 16, new Vector4(0, 0, 0, 1));
        Quad(renderer, dynamicMaterial, Vector4.One);
        renderer.End();
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(Color.Blue, pixel));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stream_capacity_drains_without_changing_submission_order(bool changeMaterial)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device) { ObserveTriangleBatches = true };
        TriangleMaterial first = new(null);
        TriangleMaterial second = first with { Sampler = TriangleSampler.PointClamp };
        int quadCount = changeMaterial ? 2049 : 5462;
        renderer.Begin(16, 16, new Vector4(0, 0, 0, 1));
        for (int index = 0; index < quadCount - 1; index++)
        {
            Quad(renderer, changeMaterial && (index & 1) != 0 ? second : first, new Vector4(1, 0, 0, 1));
        }
        Quad(renderer, first, new Vector4(0, 1, 0, 1));
        renderer.End();
        Assert.Equal(2, renderer.TriangleBatchMetrics.VertexUploads);
        Assert.Equal(quadCount * 6, renderer.TriangleBatchMetrics.Vertices);
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(Color.Lime, pixel));
    }

    [Fact]
    public void Dynamic_texture_refresh_resize_and_abort_restore_the_callers_render_target()
    {
        using GpuTestDevice device = new();
        using RenderTarget2D outer = device.CreateTarget(12, 10);
        device.Device.SetRenderTarget(outer);
        using GpuTriangleRenderer renderer = new(device.Device);
        PixelImage texture = new(1, 1, [new Rgba32(255, 0, 0, 255)]);
        foreach (Rgba32 color in new[] { new Rgba32(255, 0, 0, 255), new Rgba32(0, 255, 0, 255) })
        {
            texture.Pixels[0] = color;
            renderer.Begin(16, 16, Vector4.One);
            Quad(renderer, new(texture) { DynamicTexture = true }, Vector4.One);
            renderer.End();
            Assert.Same(outer, device.Device.GetRenderTargets()[0].RenderTarget);
            device.Device.SetRenderTarget(null);
            Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(new Color(color.R, color.G, color.B, color.A), pixel));
            device.Device.SetRenderTarget(outer);
        }
        renderer.Begin(20, 18, Vector4.One);
        renderer.Abort();
        Assert.Same(outer, device.Device.GetRenderTargets()[0].RenderTarget);
        Assert.Equal(12, device.Device.Viewport.Width);
        renderer.Begin(8, 8, Vector4.One);
        renderer.End();
        Assert.Equal(8, renderer.Target.Width);
    }

    [Fact]
    public void FramebufferReadbackReusesOnlyPrivateScratchAndReturnsIndependentCurrentPixelsAcrossResize()
    {
        using GpuTestDevice device = new();
        using RenderTarget2D outer = device.CreateTarget(12, 10);
        device.Device.SetRenderTarget(outer);
        GpuTriangleRenderer renderer = new(device.Device);
        FieldInfo scratch = typeof(GpuTriangleRenderer).GetField("captureColors", BindingFlags.Instance | BindingFlags.NonPublic)!;
        try
        {
            renderer.Begin(16, 8, new Vector4(0, 0, 0, 1));
            Quad(renderer, new(null), new Vector4(1, 0, 0, 1));
            Assert.True(renderer.TryCaptureArgb(out uint[] first));
            Color[] firstScratch = Assert.IsType<Color[]>(scratch.GetValue(renderer));
            Assert.All(first, pixel => Assert.Equal(0xFFFF0000u, pixel));

            Quad(renderer, new(null), new Vector4(0, 1, 0, 1));
            Assert.True(renderer.TryCaptureArgb(out uint[] second));
            Assert.Same(firstScratch, scratch.GetValue(renderer));
            Assert.NotSame(first, second);
            Assert.All(first, pixel => Assert.Equal(0xFFFF0000u, pixel));
            Assert.All(second, pixel => Assert.Equal(0xFF00FF00u, pixel));
            renderer.End();
            Assert.Same(outer, device.Device.GetRenderTargets()[0].RenderTarget);

            renderer.Begin(8, 16, new Vector4(0, 0, 1, 1));
            Assert.Empty(Assert.IsType<Color[]>(scratch.GetValue(renderer)));
            Assert.True(renderer.TryCaptureArgb(out uint[] resized));
            Assert.Equal(128, resized.Length);
            Assert.NotSame(firstScratch, scratch.GetValue(renderer));
            Assert.All(resized, pixel => Assert.Equal(0xFF0000FFu, pixel));
            Assert.All(second, pixel => Assert.Equal(0xFF00FF00u, pixel));
            renderer.End();
            Assert.Same(outer, device.Device.GetRenderTargets()[0].RenderTarget);
        }
        finally
        {
            renderer.Dispose();
        }
        Assert.Empty(Assert.IsType<Color[]>(scratch.GetValue(renderer)));
    }

    [Fact]
    public void Native_ordering_table_uses_hardware_depth_and_original_translucent_submission_order()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        NativeBattleLogicalResolvedCommand QuadPacket(byte opcode, uint color, int bucket, byte pass)
        {
            uint Point(int x, int y) => (ushort)x | unchecked((uint)y << 16);
            NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, opcode, [(uint)opcode << 24 | color, Point(0, 0), Point(320, 0), Point(0, 216), Point(320, 216)], 1, [], null) { OrderingBucket = bucket };
            return new(command, new(0), pass);
        }
        NativeBattlePrimitiveFrame frame = new(NativeBattleLogicalOrdering.Clear(), [QuadPacket(0x28, 0x000064, 100, 1), QuadPacket(0x28, 0x640000, 200, 1), QuadPacket(0x2A, 0xFFFFFF, 50, 2), QuadPacket(0x2A, 0xFFFFFF, 50, 2)], [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
        BattleRenderer renderer = new(320, 216);
        byte[] cpu = renderer.RenderNativePrimitiveFrame(frame, null).ToArray();
        renderer.TriangleRenderer = gpu;
        renderer.RenderNativePrimitiveFrame(frame, null);
        Color[] hardware = GpuTestDevice.Read(gpu.Target);
        for (int i = 0; i < hardware.Length; i++)
        {
            Assert.InRange(Math.Abs(hardware[i].R - cpu[i * 4]), 0, 2);
            Assert.InRange(Math.Abs(hardware[i].G - cpu[i * 4 + 1]), 0, 2);
            Assert.InRange(Math.Abs(hardware[i].B - cpu[i * 4 + 2]), 0, 2);
        }
    }

    [Fact]
    public void Admitted_native_battle_renders_on_hardware_without_changing_its_next_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_PERFORMANCE_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional pre-battle encounter save is required.");
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = BattleHostLoopPerformanceTests.RestorePerformanceWorld(session);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleSession control = battle.ForkForContinuation();
        NativeBattlePreparedPresentation prepared = Assert.IsType<NativeBattlePreparedPresentation>(battle.PreparedPresentation);
        string before = JsonSerializer.Serialize(battle.Capture());
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        BattleScenePresentation scene = new(world.BattleResources, battle.Capture().Definition);
        byte[] software = scene.Render(battle, 960, 720).ToArray();
        scene.Render(battle, 960, 720, gpu);
        Color[] hardware = GpuTestDevice.Read(gpu.Target);
        long presentedVersion = gpu.PresentationVersion;
        RenderTarget2D presentedTarget = gpu.Target;
        scene.Render(battle, 960, 720, gpu);
        Assert.Equal(presentedVersion, gpu.PresentationVersion);
        Assert.Same(presentedTarget, gpu.Target);
        Assert.Equal(hardware, GpuTestDevice.Read(gpu.Target));
        gpu.Begin(960, 720, Vector4.Zero);
        gpu.End();
        scene.Render(battle, 960, 720, gpu);
        Assert.Equal(presentedVersion + 2, gpu.PresentationVersion);
        Assert.Equal(hardware, GpuTestDevice.Read(gpu.Target));
        scene.Render(battle, 480, 360, gpu);
        Assert.Equal(presentedVersion + 3, gpu.PresentationVersion);
        Assert.Equal(480, gpu.Target.Width);
        scene.Render(battle, 960, 720, gpu);
        Assert.Equal(presentedVersion + 4, gpu.PresentationVersion);
        Assert.Equal(hardware, GpuTestDevice.Read(gpu.Target));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.Same(prepared, battle.PreparedPresentation);
        Assert.Equal(software, scene.Render(battle, 960, 720));
        long error = 0;
        int largeDifferences = 0;
        for (int i = 0; i < hardware.Length; i++)
        {
            int delta = Math.Max(Math.Abs(hardware[i].R - software[i * 4]), Math.Max(Math.Abs(hardware[i].G - software[i * 4 + 1]), Math.Abs(hardware[i].B - software[i * 4 + 2])));
            error += delta;
            if (delta > 8)
            {
                largeDifferences++;
            }
        }
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE");
        if (evidence is not null)
        {
            evidence = Path.Combine(evidence, "gpu-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"));
            Directory.CreateDirectory(evidence);
            using FileStream image = new(Path.Combine(evidence, "gpu-battle.png"), FileMode.CreateNew);
            gpu.Target.SaveAsPng(image, 960, 720);
        }
        Stopwatch timer = Stopwatch.StartNew();
        for (int i = 0; i < 60; i++)
        {
            scene.Render(battle, 960, 720, gpu);
        }
        double submissionMilliseconds = timer.Elapsed.TotalMilliseconds / 60;
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, "gpu-render-comparison", new { Adapter = device.Device.Adapter.Description, Width = 960, Height = 720, MeanMaximumChannelError = error / (double)hardware.Length, LargeDifferencePixels = largeDifferences, Pixels = hardware.Length, SubmissionMilliseconds = submissionMilliseconds, battle.Frame, NativeCallback = battle.Presentation?.Graphics?.Callback });
        Assert.InRange(error / (double)hardware.Length, 0, 2);
        Assert.InRange(largeDifferences / (double)hardware.Length, 0, 0.02);
        world.ApplyBattleInput(new(BattleInputKind.Tick));
        control.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(control.Capture()));
        Assert.Equal(battle.Presentation!.Graphics.Callback, control.Presentation!.Graphics.Callback);
        Assert.Equal(scene.Render(battle, 960, 720), new BattleScenePresentation(world.BattleResources, control.ReadDefinition()).Render(control, 960, 720));
        session.SetLiveBattle(battle);
        session.SetEventState(world.Events.Capture());
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, "gpu-battle-next-callback", session.State);
    }

    private static void Quad(ITriangleRenderer renderer, TriangleMaterial material, Vector4 color, float depth = 0.5f)
    {
        TriangleVertex a = new(Vector2.Zero, depth, 1, Vector2.Zero, color);
        TriangleVertex b = new(new Vector2(16, 0), depth, 1, Vector2.UnitX, color);
        TriangleVertex c = new(new Vector2(0, 16), depth, 1, Vector2.UnitY, color);
        TriangleVertex d = new(new Vector2(16, 16), depth, 1, Vector2.One, color);
        renderer.Draw(a, b, c, material);
        renderer.Draw(b, d, c, material);
    }
}
