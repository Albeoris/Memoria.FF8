using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleBigBomberTests
{
    [Fact]
    public void NativeBigBomberMuzzlesDebrisAndLayeredMeshesUseOriginalResources()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select the original FFVII installation.");
        Ff7Installation installation = Ff7Installation.Discover(directory!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[364];
        SceneCameraPlacement camera = SceneBattleData.Read(formation).Cameras[0];
        Ff7BattleRenderer renderer = new(archive, formation.LocationId, magic);
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleBigBomber(new(), 4, 1);
        int random = 0;
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new InvalidOperationException(), _ => default, _ => 0, () => ++random, JointPoint: (slot, selector) => slot == 4 ? new(100 + 20 * selector, -650, -50) : new(900, -800, 1800));
        renderer.Render(gpu, 320, 240, new(camera, []));
        var baseline = GpuTestDevice.Read(gpu.Target);
        HashSet<int> visible = [];
        for (int tick = 0; tick < 40 && visible.Count < 3; tick++)
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
                    0 => draw is Ff7BattlePrimaryBigBomberSpriteDraw { Sprite: 7 },
                    1 => draw is Ff7BattlePrimaryBigBomberSpriteDraw { Sprite: 5 or 6 },
                    _ => draw is Ff7BattlePrimaryBigBomberMeshDraw
                }).ToArray();
                if (packets.Length == 0)
                {
                    continue;
                }
                renderer.Render(gpu, 320, 240, new(camera, []) { PrimaryEffects = state with { Draws = packets } });
                if (baseline.Zip(GpuTestDevice.Read(gpu.Target)).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 8)
                {
                    visible.Add(family);
                }
            }
        }
        Assert.Equal(new[] { 0, 1, 2 }, visible.Order());
    }
}
