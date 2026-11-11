using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleWaveTests
{
    [Fact]
    public void OriginalWaveTreeRetainsExactPacketsSlotsAndRandomOrderAcrossJson()
    {
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.ScheduleWave(new(), 0, 0x10, 5);
        Ff7BattleSecondaryState secondary = new();
        Ff7BattlePrimaryState? restoredPrimary = null;
        Ff7BattleSecondaryState? restoredSecondary = null;
        int random = 0;
        int restoredRandom = 0;
        int peakPrimary = 0;
        int peakSecondary = 0;
        int meshes = 0;
        int explosions = 0;
        int opaque = 0;
        int fading = 0;
        int sparks = 0;
        List<(int Frame, Ff7BattlePrimaryPositionedSound Cue)> sounds = [];
        Ff7BattlePrimaryContext context = Context(new(0, -500, 0), new(900, -800, 1800), () => ++random);
        for (int frame = 0; frame <= 54; frame++)
        {
            (primary, secondary) = Advance(primary, secondary, context);
            if (restoredPrimary is not null && restoredSecondary is not null)
            {
                (restoredPrimary, restoredSecondary) = Advance(restoredPrimary, restoredSecondary, context with { NextCrt = () => ++restoredRandom });
                Assert.Equal(random, restoredRandom);
                Assert.Equal(JsonSerializer.Serialize(primary), JsonSerializer.Serialize(restoredPrimary));
                Assert.Equal(JsonSerializer.Serialize(secondary), JsonSerializer.Serialize(restoredSecondary));
            }
            Ff7BattlePrimaryWaveMeshDraw[] models = primary.Draws.OfType<Ff7BattlePrimaryWaveMeshDraw>().ToArray();
            Ff7BattlePrimaryWaveSpriteDraw[] sprites = primary.Draws.OfType<Ff7BattlePrimaryWaveSpriteDraw>().ToArray();
            peakPrimary = Math.Max(peakPrimary, primary.OccupiedSlotCount);
            peakSecondary = Math.Max(peakSecondary, secondary.Slots.Count(slot => slot is not null));
            meshes += models.Length;
            explosions += sprites.Count(draw => draw.Resource == 13);
            opaque += sprites.Count(draw => draw.Resource == 11);
            fading += sprites.Count(draw => draw.Resource == 12);
            sparks += secondary.WaveSparks.Count;
            sounds.AddRange(primary.PositionedSounds.Select(cue => (frame, cue)));
            Assert.Equal(frame == 35 ? (ushort)0x10 : (ushort)0, primary.ImpactTargets);
            Assert.Equal(frame is >= 10 and <= 39 ? (short)(((frame - 10) & 1) * 16 - 8) : (short)0, primary.CameraFocusYDelta);
            if (frame == 0)
            {
                Ff7BattlePrimaryWaveMeshDraw beam = Assert.Single(models, draw => draw.Model == 1);
                Assert.Equal(new Ff7BattleSearchScopeBasis(new(7326, 0, -3662), new(0, 0, 0), new(538, 8102, 1078)), beam.Basis);
                Assert.Equal(127, beam.Alpha);
            }
            if (frame == 6)
            {
                Assert.Equal(new Ff7BattleBraverPoint(899, -799, 1799), Assert.Single(models, draw => draw.Model == 3).Position);
            }
            if (frame == 7)
            {
                Assert.Equal(new Ff7BattleBraverPoint(301, -1398, 1203), sprites[0].Position);
                Assert.Equal(new Ff7BattleBraverPoint(304, -1395, 1206), sprites[1].Position);
            }
            if (frame == 10)
            {
                Assert.Empty(secondary.WaveSparks);
                Assert.Equal(6, primary.WaveSparks.Count);
            }
            if (frame == 11)
            {
                Assert.Equal(6, secondary.WaveSparks.Count);
                Assert.All(secondary.WaveSparks, draw => Assert.Equal(0, draw.Frame));
            }
            if (frame == 21)
            {
                Ff7BattlePrimaryWaveMeshDraw center = models.First(draw => draw.Model == 7);
                Assert.Equal(new Ff7BattleSearchScopeBasis(new(3484, 0, -6522), new(0, 7396, 0), new(6522, 0, 3484)), center.Basis);
                restoredPrimary = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(primary))!;
                restoredSecondary = JsonSerializer.Deserialize<Ff7BattleSecondaryState>(JsonSerializer.Serialize(secondary))!;
                restoredRandom = random;
            }
        }
        Assert.Equal(254, random);
        Assert.Equal(37, peakPrimary);
        Assert.Equal(48, peakSecondary);
        Assert.Equal(155, meshes);
        Assert.Equal(224, explosions);
        Assert.Equal(460, opaque);
        Assert.Equal(140, fading);
        Assert.Equal(1056, sparks);
        Assert.Equal(new[] { (0, new Ff7BattlePrimaryPositionedSound(0xFA, 0, 64)), (10, new Ff7BattlePrimaryPositionedSound(0x176, 4, 64)) }, sounds);
        Assert.False(primary.HasCallbacks);
        Assert.False(secondary.HasCallbacks);
        Assert.Equal(0, primary.StageTransparency);
    }

    [Fact]
    public void InstalledWaveMeshesFragmentsAndSecondarySpritesRenderAfterColdRestore()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII Wave presentation checks.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic, grenadeTexture: Ff7BattleGrenadeResources.Load(installation));
        Vector3 source = scene.Enemies[0].Placement.Position + new Vector3(0, -600, 0);
        Vector3 target = source + new Vector3(900, 0, 900);
        int random = 0;
        Ff7BattlePrimaryContext context = Context(source, target, () => ++random);
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.ScheduleWave(new(), 0, 0x10);
        Ff7BattleSecondaryState secondary = new();
        for (int frame = 0; frame <= 39; frame++)
        {
            (primary, secondary) = Advance(primary, secondary, context);
            if (frame is 6 or 12 or 25 or 35)
            {
                primary = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(primary))!;
                secondary = JsonSerializer.Deserialize<Ff7BattleSecondaryState>(JsonSerializer.Serialize(secondary))!;
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { PrimaryEffects = primary with { Draws = [] } });
                Color[] baseline = GpuTestDevice.Read(gpu.Target);
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { PrimaryEffects = primary, SecondaryEffects = secondary });
                Assert.True(baseline.Zip(GpuTestDevice.Read(gpu.Target)).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 2, $"No original Wave pixels at callback {frame}.");
                if (frame == 25)
                {
                    renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { PrimaryEffects = primary with { Draws = [] }, SecondaryEffects = secondary });
                    Assert.True(baseline.Zip(GpuTestDevice.Read(gpu.Target)).Any(pair => pair.First.PackedValue != pair.Second.PackedValue), "Native Wave secondary sparks did not render independently.");
                }
            }
        }
    }

    private static Ff7BattlePrimaryContext Context(Vector3 source, Vector3 target, Func<int> random)
    {
        return new((_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), random, JointPoint: (actor, selector) => (actor, selector) switch
        {
            (0, 9) => source,
            (4, 0) => target,
            _ => throw new InvalidOperationException("Unexpected native Wave joint.")
        });
    }

    private static (Ff7BattlePrimaryState Primary, Ff7BattleSecondaryState Secondary) Advance(Ff7BattlePrimaryState primary, Ff7BattleSecondaryState secondary, Ff7BattlePrimaryContext context)
    {
        secondary = Ff7BattleSecondaryEffects.Advance(secondary, (_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), () => throw new InvalidOperationException());
        primary = Ff7BattlePrimaryEffects.Advance(primary, context);
        foreach (Ff7BattleBraverPoint spark in primary.WaveSparks)
        {
            secondary = Ff7BattleSecondaryEffects.ScheduleWaveSpark(secondary, spark);
        }
        return (primary, secondary);
    }
}
