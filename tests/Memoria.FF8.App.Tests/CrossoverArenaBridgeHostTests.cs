using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossoverArenaBridgeHostTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Tornado", 0x30, true)]
    [InlineData("Thundaga", 0x10, false)]
    public void OriginalActionCapturesTheHostSurfaceAndRestoresItsTemporaryGeometry(string name, ushort targets, bool deformation)
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR")) && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")), "Select both original game installations.");
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using CrossoverArenaSurfaceCapture capture = new(() => new GpuTriangleRenderer(device.Device));
        bool sampled = false;
        bool movedSurface = false;
        bool movedActor = false;
        int[] foreignActors = [];
        bool rendered = false;
        bool restored = false;
        NativeBattleEffectPresentation? effectOwner = null;
        Ff7BattleArenaGeometry? baselineArena = null;
        new CrossoverUniversalAbilityHostTests(output).Run(name, targets, arenaBridge: true, arenaCapture: capture, observe: Observe);
        Assert.True(sampled, "The original action must sample an actual retained host surface image.");
        Assert.True(rendered, "The original effect must render its captured surface through the host compositor.");
        Assert.True(restored);
        Assert.NotNull(effectOwner);
        Assert.Empty(effectOwner.ArenaReplacements);
        if (deformation)
        {
            Assert.True(movedSurface, "The coarse host floor must acquire real interior deformation vertices.");
            Assert.True(movedActor, "The original action must lift and rotate its selected host actors.");
            foreach (int actor in foreignActors)
            {
                NativeBattleActionMatrix placement = effectOwner.ForeignPlacement(actor);
                Assert.Equal((0, 0, 0), (placement.X, placement.Y, placement.Z));
                Assert.True(placement.Rotation.SequenceEqual(NativeBattleForeignModelGeometry.Identity.Rotation));
            }
        }
        Assert.True(capture.RetainedImages > 0);
        capture.Reset();
        Assert.Equal(0, capture.RetainedImages);
        Assert.Equal(0, capture.RetainedImageBytes);

        void Observe(Ff7BattleRenderer renderer, Ff7BattleSession host, CrossoverGuestHostActions provider)
        {
            baselineArena ??= renderer.CaptureArenaGeometry();
            Assert.Same(baselineArena, renderer.CaptureArenaGeometry());
            if (provider.ActiveAbility is not { Presentation.Effect: { } effect } active || provider.EffectVisual is not { Ordering: { } ordering } visual)
            {
                restored |= effectOwner is not null && provider.Invocation is null && provider.ArenaReplacements.IsEmpty;
                return;
            }
            effectOwner = effect;
            if (deformation)
            {
                foreignActors = active.Presentation.BoundTargets.Where(target => target.Contact.ActorIndex != 0).Select(target => (int)target.Contact.ActorIndex).ToArray();
                movedActor |= foreignActors.Any(actor =>
                {
                    NativeBattleActionMatrix placement = effect.ForeignPlacement(actor);
                    return placement.X != 0 || placement.Y != 0 || placement.Z != 0 || !placement.Rotation.SequenceEqual(NativeBattleForeignModelGeometry.Identity.Rotation);
                });
            }
            NativeBattlePilotVisualGeometry[] geometry = ordering.Packets.Select(packet => packet.PilotGeometry).OfType<NativeBattlePilotVisualGeometry>().ToArray();
            INativeBattleForeignMaterial[] materials = geometry.Select(pilot => pilot.ForeignSurface?.Material).OfType<INativeBattleForeignMaterial>().ToArray();
            foreach (NativeBattleArenaCaptureMaterial request in materials.OfType<NativeBattleArenaCaptureMaterial>())
            {
                NativeBattleForeignRenderMaterial material = capture.Resolve(request);
                PixelImage image = Assert.IsType<PixelImage>(material.Material.Texture);
                Assert.Contains(image.Pixels, pixel => pixel.A != 0 && (pixel.R != 0 || pixel.G != 0 || pixel.B != 0));
                sampled = true;
            }
            if (deformation && effect.ArenaReplacements.FirstOrDefault() is { } replacement)
            {
                float floor = replacement.Geometry.Faces[0].A.Y;
                HashSet<INativeBattleForeignMaterial> floorMaterials = replacement.Geometry.Faces.Select(face => face.Surface.Material).ToHashSet();
                movedSurface |= geometry.Any(pilot => pilot.ForeignSurface is { } surface && floorMaterials.Contains(surface.Material) && (Math.Abs(pilot.A.Y - floor) > 1 || Math.Abs(pilot.B.Y - floor) > 1 || Math.Abs(pilot.C.Y - floor) > 1));
            }
            if (rendered || !sampled || deformation && !movedSurface)
            {
                return;
            }
            capture.Prepare(materials);
            var camera = host.GuestRenderCameraOutput;
            HashSet<NativeBattleArenaPrimitiveId> suppressed = effect.ArenaReplacements.SelectMany(replacement => replacement.Geometry.Faces).Select(face => face.Id).ToHashSet();
            if (deformation)
            {
                Assert.NotEmpty(suppressed);
            }
            foreach ((int width, int height) in new[] { (320, 240), (640, 360) })
            {
                renderer.Render(gpu, width, height, new(camera, []));
                Microsoft.Xna.Framework.Color[] baseline = GpuTestDevice.Read(gpu.Target);
                renderer.Render(gpu, width, height, new(camera, []) { GuestEffects = visual, SuppressArenaPrimitives = suppressed });
                Microsoft.Xna.Framework.Color[] drawn = GpuTestDevice.Read(gpu.Target);
                Assert.True(baseline.Where((pixel, index) => pixel != drawn[index]).Take(17).Count() > 16);
                Assert.Equal(camera, host.GuestRenderCameraOutput);
                renderer.Render(gpu, width, height, new(camera, []));
                Assert.Equal(baseline, GpuTestDevice.Read(gpu.Target));
            }
            rendered = true;
        }
    }
}
