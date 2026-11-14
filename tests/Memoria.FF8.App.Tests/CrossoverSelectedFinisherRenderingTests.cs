using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossoverSelectedFinisherRenderingTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public void Native_selected_child_renders_host_scene_without_changing_execution_and_releases_for_next_magic(byte id, bool overkill)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using CrossoverArenaSurfaceCapture capture = new(() => new GpuTriangleRenderer(device.Device));
        using CrossoverCompositeActionTests.Fixture fixture = new("Fire", 0, 4, sourceHP: 1, nativeProcess: true, allFinishers: true, arenaCapture: capture);
        int callbacks = 0;
        HashSet<(int Width, int Height)> visible = [];
        CrossoverCompositeActionTests.Result result = fixture.ExecuteSelectedFinisher(id, overkill, observe: (renderer, host, provider) =>
        {
            if (host.Action?.GuestContinuation is null || provider.EffectVisual is not { } effect || effect.Ordering is not { Packets.Count: > 0 })
            {
                return;
            }
            if (callbacks++ % 32 != 0)
            {
                return;
            }
            string state = JsonSerializer.Serialize(host.Capture());
            string random = JsonSerializer.Serialize(provider.CurrentCrtRandom);
            capture.Prepare(effect.Ordering.Packets.Select(packet => packet.PilotGeometry?.ForeignSurface?.Material).OfType<INativeBattleForeignMaterial>());
            Ff7BattleRenderFrame frame = CrossoverAllGuardianRenderingTests.Frame(host, provider);
            foreach ((int width, int height) in new[] { (320, 240), (640, 360) })
            {
                CrossoverAllGuardianRenderingTests.ValidatingRenderer validation = new(gpu);
                renderer.Render(validation, width, height, frame);
                Microsoft.Xna.Framework.Color[] full = GpuTestDevice.Read(gpu.Target);
                renderer.Render(gpu, width, height, frame with { GuestEffectLayers = [], SuppressArenaPrimitives = new HashSet<NativeBattleArenaPrimitiveId>() });
                Microsoft.Xna.Framework.Color[] control = GpuTestDevice.Read(gpu.Target);
                int changed = full.Where((pixel, index) => pixel.R != control[index].R || pixel.G != control[index].G || pixel.B != control[index].B).Count();
                if (validation.Triangles > 0 && changed > 16)
                {
                    visible.Add((width, height));
                }
            }
            Assert.Equal(state, JsonSerializer.Serialize(host.Capture()));
            Assert.Equal(random, JsonSerializer.Serialize(provider.CurrentCrtRandom));
        });
        Assert.Equal(2, visible.Count);
        Assert.True(callbacks > 0);
        fixture.FollowWithFire();
        capture.Reset();
        Assert.Equal(0, capture.RetainedImages);
        Assert.Equal(0, capture.RetainedImageBytes);
        output.WriteLine($"Selected child{id}: {result.Contacts.Length} ordered contacts; {callbacks} eligible callbacks; both host viewports changed; source resources and following Fire completed.");
    }
}
