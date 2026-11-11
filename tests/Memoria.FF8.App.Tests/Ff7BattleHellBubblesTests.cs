using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using System.Numerics;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleHellBubblesTests
{
    [Fact]
    public void OriginalHellBubblesTexturesMeshAndRibbonRasterizeIndependently()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select the original FFVII installation.");
        Ff7Installation installation = Ff7Installation.Discover(directory!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[341];
        SceneCameraPlacement camera = SceneBattleData.Read(formation).Cameras[0];
        Ff7BattleRenderer renderer = new(archive, formation.LocationId, magic);
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleHellBubbles(new(), 4, 1);
        int random = 1;
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new InvalidOperationException(), _ => default, _ => 0, NextCrt, JointPoint: (slot, _) => slot == 4 ? new Vector3(0, -650, 0) : new(900, -800, 1800));
        renderer.Render(gpu, 320, 240, new(camera, []));
        var baseline = GpuTestDevice.Read(gpu.Target);
        HashSet<int> visible = [];
        for (int tick = 0; tick < 70 && visible.Count < 4; tick++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            for (int family = 0; family < 4; family++)
            {
                if (visible.Contains(family))
                {
                    continue;
                }
                Ff7BattlePrimaryDraw[] packets = state.Draws.Where(draw => family switch
                {
                    0 => draw is Ff7BattlePrimaryBubbleSpriteDraw { Burst: false },
                    1 => draw is Ff7BattlePrimaryBubbleSpriteDraw { Burst: true },
                    2 => draw is Ff7BattlePrimaryBubbleCloudDraw,
                    3 => draw is Ff7BattlePrimaryBubbleRibbonDraw,
                    _ => false
                }).ToArray();
                if (packets.Length == 0)
                {
                    continue;
                }
                renderer.Render(gpu, 320, 240, new(camera, []) { PrimaryEffects = state with { Draws = packets } });
                if (baseline.Zip(GpuTestDevice.Read(gpu.Target)).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 16)
                {
                    visible.Add(family);
                }
            }
        }
        Assert.Equal(new[] { 0, 1, 2, 3 }, visible.Order());

        int NextCrt()
        {
            random = unchecked(random * 214013 + 2531011);
            return random >> 16 & 0x7FFF;
        }
    }
}
