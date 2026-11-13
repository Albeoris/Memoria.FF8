using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleSummonTests
{
    [Fact]
    public void OriginalSummonModelsDrawTheirNativeMeshesAndClips()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII Choco/Mog presentation checks.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic);
        SceneCameraPlacement camera = new(new(4500, -2100, -4500), new(0, -350, 0));
        Ff7BattleRenderFrame frame = new(camera, []);
        renderer.Render(gpu, 320, 240, frame);
        Color[] baseline = GpuTestDevice.Read(gpu.Target);
        foreach (bool fat in new[] { false, true })
        {
            foreach (int clip in new[] { 0, 1 })
            {
                Ff7BattleSummonModelDraw model = new(fat, clip, 2, new(0, 0, 0), 0);
                renderer.Render(gpu, 320, 240, frame with { SummonModel = model });
                Color[] actual = GpuTestDevice.Read(gpu.Target);
                Assert.True(baseline.Zip(actual).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 20, $"No original summon geometry for alternate={fat} and clip {clip}.");
            }
        }
    }
}
