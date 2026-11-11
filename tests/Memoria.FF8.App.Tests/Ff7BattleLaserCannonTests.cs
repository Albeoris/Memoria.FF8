using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;
using Vector3 = System.Numerics.Vector3;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleLaserCannonTests
{
    [Fact]
    public void OriginalClockPreservesBeamImpactFlareOccupancyAndSlotAddressPan()
    {
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleLaserCannon(new(), 4, 1);
        Ff7BattlePrimaryState? cold = null;
        Ff7BattleBraverPoint muzzle = new(0, -500, 0);
        Ff7BattleBraverPoint destination = new(900, -800, 1800);
        Ff7BattleBraverPoint contact = new(925, -805, 1825);
        int beamCount = 0;
        int impactCount = 0;
        for (int frame = 0; frame <= 15; frame++)
        {
            Ff7BattlePrimaryContext context = Context(muzzle.Position, frame >= 7 ? contact.Position : destination.Position);
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            if (cold is not null)
            {
                cold = Ff7BattlePrimaryEffects.Advance(cold, context);
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(cold));
            }

            if (frame is >= 3 and <= 10)
            {
                Ff7BattlePrimaryLaserCannonBeamDraw beam = Assert.Single(state.Draws.OfType<Ff7BattlePrimaryLaserCannonBeamDraw>());
                Assert.Equal(2, beam.Slot);
                Assert.Equal(muzzle, beam.Source);
                Assert.Equal(destination, beam.Target);
                Assert.Equal(frame - 3, beam.Frame);
                beamCount++;
            }
            if (frame is >= 7 and <= 14)
            {
                Ff7BattlePrimaryLaserCannonImpactDraw impact = Assert.Single(state.Draws.OfType<Ff7BattlePrimaryLaserCannonImpactDraw>());
                Assert.Equal(4, impact.Slot);
                Assert.Equal(contact, impact.Position);
                Assert.Equal(frame - 7, impact.Frame);
                impactCount++;
            }
            if (frame is >= 3 and < 10)
            {
                Assert.Equal(frame - 2, Assert.IsType<Ff7BattlePrimaryLaserCannonFlare>(state.Slots[3]).Age);
            }
            if (frame == 3)
            {
                Assert.Equal(new Ff7BattlePrimaryPositionedSound(0x3A, -1, 0x84, RawPan: true), Assert.Single(state.PositionedSounds));
                cold = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
            }
            else
            {
                Assert.Empty(state.PositionedSounds);
            }
            Assert.Equal(frame == 7 ? 1 : 0, state.ImpactTargets);
        }

        Assert.Equal(8, beamCount);
        Assert.Equal(8, impactCount);
        Assert.False(state.HasCallbacks);
    }

    [Fact]
    public void InstalledLaserCannonUsesOriginalBeamAndImpactTextures()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII Laser Cannon presentation checks.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[312];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic);
        SceneCameraPlacement camera = scene.Cameras[0];
        BattleCameraBasis basis = BattleCameraBasis.Create(camera);
        Vector3 source = camera.Focus - 500 * basis.Right + 400 * basis.Up;
        Vector3 target = camera.Focus + 500 * basis.Right + 300 * basis.Up;
        Ff7BattlePrimaryContext context = Context(source, target);
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleLaserCannon(new(), 4, 1);
        renderer.Render(gpu, 320, 240, new(camera, []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);

        for (int frame = 0; frame <= 14; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            if (frame is 4 or 8)
            {
                Ff7BattlePrimaryDraw draw = frame == 4
                    ? Assert.Single(state.Draws.OfType<Ff7BattlePrimaryLaserCannonBeamDraw>())
                    : Assert.Single(state.Draws.OfType<Ff7BattlePrimaryLaserCannonImpactDraw>());
                renderer.Render(gpu, 320, 240, new(camera, []) { PrimaryEffects = state with { Draws = [draw] } });
                Assert.True(baseline.Zip(GpuTestDevice.Read(gpu.Target)).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 2, $"No original Laser Cannon pixels at callback {frame}.");
            }
        }
    }

    [Fact]
    public void InstalledBeamMatchesOriginalScreenPacketAtFirstAndLastBands()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII Laser Cannon packet checks.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleRenderer renderer = new(battle, 10, magic);
        SceneCameraPlacement camera = new(Vector3.Zero, Vector3.UnitZ);
        TriangleCapture baseline = new();
        renderer.Render(baseline, 320, 240, new(camera, []));
        foreach (int frame in new[] { 0, 7 })
        {
            Ff7BattlePrimaryLaserCannonBeamDraw draw = new(new(-500, -200, 6200), new(500, -300, 7400), frame);
            TriangleCapture capture = new();
            renderer.Render(capture, 320, 240, new(camera, []) { PrimaryEffects = new() { Draws = [draw] } });
            var packets = capture.Packets.Skip(baseline.Packets.Count).ToArray();
            Assert.Equal(2, packets.Length);
            Assert.Equal(new Vector2(113, 19), packets[0].A.Position);
            Assert.Equal(new Vector2(123, 187), packets[0].B.Position);
            Assert.Equal(new Vector2(189, 15), packets[0].C.Position);
            Assert.Equal(new Vector2(199, 183), packets[1].B.Position);
            Assert.Equal(new Vector2(0, frame / 8f), packets[0].A.Uv);
            Assert.Equal(new Vector2(255f / 256, (frame * 32 + 31f) / 256), packets[1].B.Uv);
            Assert.All(packets.SelectMany(packet => new[] { packet.A, packet.B, packet.C }), vertex =>
            {
                Assert.Equal(1 / 6200f, vertex.Depth);
                Assert.Equal(1, vertex.ReciprocalW);
            });
            Assert.Equal(TriangleBlend.Add, packets[0].Material.Blend);
        }
    }

    private sealed class TriangleCapture : ITriangleRenderer
    {
        public List<(TriangleVertex A, TriangleVertex B, TriangleVertex C, TriangleMaterial Material)> Packets { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Packets.Add((a, b, c, material));
        }

        public void End()
        {
        }
    }

    private static Ff7BattlePrimaryContext Context(Vector3 source, Vector3 target) => new(
        (_, _, _) => throw new InvalidOperationException(),
        _ => throw new InvalidOperationException(),
        _ => throw new InvalidOperationException(),
        () => throw new InvalidOperationException("Original Laser Cannon has no CRT draw."),
        JointPoint: (actor, selector) => (actor, selector) switch
        {
            (4, 9) => source,
            (0, 0) => target,
            _ => throw new InvalidOperationException("Unexpected Laser Cannon joint selector.")
        });
}
