using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleBeamGunTests
{
    [Fact]
    public void NativeBeamGunFlashShotsAndImpactUseOriginalResources()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select the original FFVII installation.");
        Ff7Installation installation = Ff7Installation.Discover(directory!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[311];
        SceneCameraPlacement camera = SceneBattleData.Read(formation).Cameras[0];
        Ff7BattleRenderer renderer = new(archive, formation.LocationId, magic);
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleBeamGun(new(), 4, 1);
        int random = 0;
        Vector3 target = new(900, -800, 1800);
        Ff7BattlePrimaryContext context = new((_, _, _) => target, _ => default, _ => 350, () => ++random, JointPoint: (slot, _) => slot == 4 ? new Vector3(0, -650, 0) : target, JointSelector: (_, _) => 0);
        renderer.Render(gpu, 320, 240, new(camera, []));
        var baseline = GpuTestDevice.Read(gpu.Target);
        HashSet<int> visible = [];
        for (int tick = 0; tick < 35 && visible.Count < 3; tick++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            for (int family = 0; family < 3; family++)
            {
                if (visible.Contains(family))
                {
                    continue;
                }
                Ff7BattlePrimaryDraw[] packets = state.Draws.Where(draw => family switch
                {
                    0 => draw is Ff7BattlePrimaryBeamGunMeshDraw,
                    1 => draw is Ff7BattlePrimaryBeamGunSpriteDraw { FlashBasis: not null },
                    _ => draw is Ff7BattlePrimaryBeamGunSpriteDraw { FlashBasis: null }
                }).ToArray();
                if (packets.Length == 0)
                {
                    continue;
                }
                SceneCameraPlacement view = camera;
                var background = baseline;
                if (family == 0 && packets[0] is Ff7BattlePrimaryBeamGunMeshDraw shot)
                {
                    Vector3 position = shot.Position.Position;
                    view = new(position + new Vector3(40, -20, -200), position);
                    renderer.Render(gpu, 320, 240, new(view, []));
                    background = GpuTestDevice.Read(gpu.Target);
                }
                renderer.Render(gpu, 320, 240, new(view, []) { PrimaryEffects = state with { Draws = packets } });
                if (background.Zip(GpuTestDevice.Read(gpu.Target)).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 8)
                {
                    visible.Add(family);
                }
            }
        }
        Assert.Equal(new[] { 0, 1, 2 }, visible.Order());
    }
}
