using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleTailLaserTests
{
    [Fact]
    public void OriginalTailLaserUsesItsMeshAndBothSpritePools()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select the original FFVII installation.");
        Ff7Installation installation = Ff7Installation.Discover(directory!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[324];
        SceneCameraPlacement camera = SceneBattleData.Read(formation).Cameras[0];
        Ff7BattleRenderer renderer = new(archive, formation.LocationId, magic, cureTexture: Ff7BattleCureResources.Load(installation));
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.ScheduleTailLaser(new(), new(900, -900, 1100), new(0, 0, -1050), 3);
        Ff7BattleSecondaryState secondary = new();
        int random = 0;
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new InvalidOperationException(), _ => default, _ => 0, () => ++random, JointPoint: (slot, _) => slot == 0 ? new(-1200, -450, -1000) : new(1200, -500, -1100));
        int visibleSamples = 0;
        for (int tick = 0; tick < 54; tick++)
        {
            secondary = Ff7BattleSecondaryEffects.Advance(secondary, context.ActorPoint, context.ActorRotation, context.NextCrt);
            primary = Ff7BattlePrimaryEffects.Advance(primary, context);
            foreach (Ff7BattleTailLaserSecondary callback in primary.TailLaserSecondaries.Cast<Ff7BattleTailLaserSecondary>())
            {
                secondary = Ff7BattleSecondaryEffects.ScheduleTailLaser(secondary, callback);
            }
            if (tick is not (0 or 2 or 6 or 16 or 24 or 35 or 45))
            {
                continue;
            }
            renderer.Render(gpu, 320, 240, new(camera, []) { PrimaryEffects = primary with { Draws = [] } });
            var baseline = GpuTestDevice.Read(gpu.Target);
            renderer.Render(gpu, 320, 240, new(camera, []) { PrimaryEffects = primary, SecondaryEffects = secondary });
            if (baseline.Zip(GpuTestDevice.Read(gpu.Target)).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 8)
            {
                visibleSamples++;
            }
        }
        Assert.True(visibleSamples >= 5, $"Only {visibleSamples} Tail Laser charge, beam or impact samples changed the original arena.");
    }
}
