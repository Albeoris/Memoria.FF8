using System.Diagnostics;
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
    public void Standalone_native_battle_renders_on_hardware_without_changing_its_next_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_PERFORMANCE_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional standalone battle fixture is required.");
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = NativeBattleProductionEffectAdmissionTests.RestoreWorld(session, TestResources.GameDirectory);
        BattleSession battle = world.Battle!;
        string before = JsonSerializer.Serialize(battle.Capture());
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        BattleScenePresentation scene = new(world.BattleResources, battle.Capture().Definition, true);
        byte[] software = scene.Render(battle, 960, 720).ToArray();
        scene.Render(battle, 960, 720, gpu);
        Color[] hardware = GpuTestDevice.Read(gpu.Target);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
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
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, "gpu-render-comparison", new { Adapter = device.Device.Adapter.Description, Width = 960, Height = 720, MeanMaximumChannelError = error / (double)hardware.Length, LargeDifferencePixels = largeDifferences, Pixels = hardware.Length, SubmissionMilliseconds = submissionMilliseconds, battle.Frame, NativeCallback = battle.NativeGraphics?.Callback });
        Assert.InRange(error / (double)hardware.Length, 0, 2);
        Assert.InRange(largeDifferences / (double)hardware.Length, 0, 0.02);
        using GameWorld restored = NativeBattleProductionEffectAdmissionTests.RestoreWorld(new GameSession(session.State), TestResources.GameDirectory);
        TimeSpan callback = TimeSpan.FromSeconds(1d / 30);
        battle.AdvanceNativeGraphicsPresentation(callback);
        restored.Battle!.AdvanceNativeGraphicsPresentation(callback);
        world.ApplyBattleInput(new(BattleInputKind.Tick));
        restored.ApplyBattleInput(new(BattleInputKind.Tick));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Battle.Capture()));
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
