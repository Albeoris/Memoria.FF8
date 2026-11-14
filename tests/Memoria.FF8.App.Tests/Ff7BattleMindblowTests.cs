using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleMindblowTests
{
    [Fact]
    public void OriginalMindblowDrawsBothMeshLayersAndParticles()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII Mindblow presentation checks.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic);
        Vector3 anchor = new(0, -450, 0);
        uint seed = 1;
        Ff7BattlePrimaryContext context = new((_, _, _) => anchor, _ => default, _ => 180, NextCrt, JointPoint: (_, _) => anchor, WeaponPoint: (_, offset) => anchor + offset);
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.ScheduleMindblow(new(), 0, 0x10);
        bool mesh = false;
        bool impact = false;
        for (int callback = 0; callback < 45; callback++)
        {
            primary = Ff7BattlePrimaryEffects.Advance(primary, context);
            if (callback is 18 or 24)
            {
                primary = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(primary))!;
                Ff7BattlePrimaryDraw[] draws = callback == 18
                    ? primary.Draws.OfType<Ff7BattlePrimaryMindblowMeshDraw>().Cast<Ff7BattlePrimaryDraw>().ToArray()
                    : primary.Draws.OfType<Ff7BattlePrimaryMindblowParticleDraw>().Cast<Ff7BattlePrimaryDraw>().ToArray();
                Assert.NotEmpty(draws);
                if (callback == 18)
                {
                    Assert.Equal(2, draws.Length);
                    mesh = true;
                }
                else
                {
                    impact = true;
                }
                SceneCameraPlacement camera = callback == 18
                    ? new(anchor + new Vector3(100, -65, -400), anchor + new Vector3(0, 0, -180))
                    : new(anchor + new Vector3(4000, -1250, -3500), anchor);
                Ff7BattleRenderFrame frame = new(camera, []);
                renderer.Render(gpu, 320, 240, frame);
                Color[] baseline = GpuTestDevice.Read(gpu.Target);
                renderer.Render(gpu, 320, 240, frame with { PrimaryEffects = primary with { Draws = draws } });
                Assert.True(baseline.Zip(GpuTestDevice.Read(gpu.Target)).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 2, $"No original Mindblow pixels at callback {callback}.");
            }
        }
        Assert.True(mesh && impact);

        int NextCrt()
        {
            seed = unchecked(seed * 214013 + 2531011);
            return (int)(seed >> 16 & 32767);
        }
    }
}
