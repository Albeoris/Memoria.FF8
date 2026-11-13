using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossoverInvocationCaptureTests(ITestOutputHelper output)
{
    [Fact]
    public void Original_wave_consumes_a_real_host_scene_image_at_multiple_display_sizes()
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR")) && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")), "Select both original game installations.");
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using CrossoverArenaSurfaceCapture capture = new(() => new GpuTriangleRenderer(device.Device));
        bool sampled = false;
        bool rendered = false;
        new CrossoverParticipantResourceHostTests(output).Run("Ultra Waves", 3, 4, capture, Observe);
        Assert.True(sampled);
        Assert.True(rendered);
        capture.Reset();
        Assert.Equal(0, capture.RetainedImages);
        Assert.Equal(0, capture.RetainedImageBytes);

        void Observe(Ff7BattleRenderer renderer, Ff7BattleSession host, CrossoverGuestHostActions provider)
        {
            if (rendered || provider.EffectVisual is not { Ordering: { } ordering } visual)
            {
                return;
            }
            INativeBattleForeignMaterial[] materials = ordering.Packets.Select(packet => packet.PilotGeometry?.ForeignSurface?.Material).OfType<INativeBattleForeignMaterial>().ToArray();
            foreach (NativeBattleArenaCaptureMaterial request in materials.OfType<NativeBattleArenaCaptureMaterial>())
            {
                NativeBattleForeignRenderMaterial material = capture.Resolve(request);
                PixelImage image = Assert.IsType<PixelImage>(material.Material.Texture);
                Assert.Equal((320, 216, 384, 256), (request.Width, request.Height, request.NativeX, request.NativeY));
                Assert.Contains(image.Pixels, pixel => pixel.A != 0 && (pixel.R != 0 || pixel.G != 0 || pixel.B != 0));
                sampled = true;
            }
            if (!sampled)
            {
                return;
            }
            capture.Prepare(materials);
            var camera = host.GuestRenderCameraOutput;
            foreach ((int width, int height) in new[] { (320, 240), (640, 360) })
            {
                renderer.Render(gpu, width, height, new(camera, []));
                Microsoft.Xna.Framework.Color[] baseline = GpuTestDevice.Read(gpu.Target);
                renderer.Render(gpu, width, height, new(camera, []) { GuestEffects = visual });
                Microsoft.Xna.Framework.Color[] effect = GpuTestDevice.Read(gpu.Target);
                Assert.True(baseline.Where((pixel, index) => pixel != effect[index]).Take(17).Count() > 16);
                Assert.Equal(camera, host.GuestRenderCameraOutput);
                renderer.Render(gpu, width, height, new(camera, []));
                Assert.Equal(baseline, GpuTestDevice.Read(gpu.Target));
            }
            rendered = true;
        }
    }
}
