using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleDestroyedTests
{
    [Theory]
    [InlineData(320, 240)]
    [InlineData(400, 225)]
    public void OriginalBlueScreenPulseCoversTheSelectedViewport(int width, int height)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select the original FFVII installation.");
        Ff7Installation installation = Ff7Installation.Discover(directory!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneCameraPlacement camera = SceneBattleData.Read(formation).Cameras[0];
        Ff7BattleRenderer renderer = new(archive, formation.LocationId, magic);
        renderer.Render(gpu, width, height, new(camera, []));
        var background = GpuTestDevice.Read(gpu.Target);
        renderer.Render(gpu, width, height, new(camera, []) { Destroyed = new(0x10, 0, []) });
        var pulse = GpuTestDevice.Read(gpu.Target);
        foreach (int index in new[] { 0, width - 1, width * (height - 1), width * height - 1, width * height / 2 })
        {
            Assert.Equal(background[index].R, pulse[index].R);
            Assert.Equal(background[index].G, pulse[index].G);
            Assert.InRange((int)pulse[index].B, Math.Max(0, Math.Min(255, background[index].B + 32) - 1), Math.Min(255, background[index].B + 33));
        }
    }

    [Fact]
    public void OriginalDestroyedSpritesRasterizeAndResumeThroughoutTheirNativeLifetime()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select the original FFVII installation.");
        Ff7Installation installation = Ff7Installation.Discover(directory!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData data = SceneBattleData.Read(formation);
        SceneBattleEnemy enemy = data.Enemies[0];
        string model = Ff7BattleRenderer.EnemyModelPrefix(enemy.Placement.EnemyId);
        Ff7BattlePlacement placement = new(enemy.Placement.Position, Ff7BattlePlacementTables.EnemyYaw(formation.Layout, enemy.Placement.Position));
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation));
        Ff7BattleRenderer renderer = new(archive, formation.LocationId, magic);
        Ff7BattleDestroyedState state = Ff7BattleDestroyedEffect.Start(0x10);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7BattleRng random = new(kernel[2].Data.Span, new(new byte[8], CrtSeed: 1));
        int visibleFrames = 0;
        while (!state.Completed)
        {
            state = Ff7BattleDestroyedEffect.Advance(state, _ => animations.BoneCount(model), (_, bone) => animations.BonePosition(model, 0, 0, placement, bone), random.NextCrt).State;
            Ff7BattleDestroyedState saved = JsonSerializer.Deserialize<Ff7BattleDestroyedState>(JsonSerializer.Serialize(state))!;
            renderer.Render(gpu, 320, 240, new(data.Cameras[0], []) { Destroyed = state });
            var first = GpuTestDevice.Read(gpu.Target);
            renderer.Render(gpu, 320, 240, new(data.Cameras[0], []) { Destroyed = saved });
            var cold = GpuTestDevice.Read(gpu.Target);
            Assert.Equal(first, cold);
            Ff7BattlePrimaryState? pulse = state.Primary is { } primary ? primary with { Draws = primary.Draws.Where(draw => draw is not Ff7BattlePrimaryDestroyedSpriteDraw).ToArray() } : null;
            renderer.Render(gpu, 320, 240, new(data.Cameras[0], []) { Destroyed = state with { Sprites = [], Primary = pulse } });
            var pulseOnly = GpuTestDevice.Read(gpu.Target);
            if (first.Zip(pulseOnly).Count(pair => pair.First != pair.Second) > 16)
            {
                visibleFrames++;
            }
        }
        Assert.True(visibleFrames >= 22, $"Destroyed drew visible original sprites in {visibleFrames} frames.");
    }
}
