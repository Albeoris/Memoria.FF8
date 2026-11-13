using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleCrossSlashTests
{
    [Fact]
    public void OriginalCrossSlashDrawsEveryNativeSpriteFamilyAfterRestore()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII Cross-slash presentation checks.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic);
        Vector3 anchor = scene.Enemies[0].Placement.Position + new Vector3(0, -450, 0);
        Ff7BattlePrimaryContext context = new((_, _, _) => anchor, _ => default, _ => 200, () => throw new InvalidOperationException("Cross-slash uses no random draws."), JointPoint: (_, _) => anchor);
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.ScheduleCrossSlash(new(), 0x10);
        for (int frame = 0; frame < 60; frame++)
        {
            primary = Ff7BattlePrimaryEffects.Advance(primary, context);
            if (frame is 10 or 20 or 31)
            {
                Assert.Equal(frame == 10 ? 0 : frame == 20 ? 1 : 2, primary.Draws.OfType<Ff7BattlePrimaryCrossSlashDraw>().Last().Sprite);
                primary = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(primary))!;
                Ff7BattlePrimaryCrossSlashDraw sprite = primary.Draws.OfType<Ff7BattlePrimaryCrossSlashDraw>().Last();
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { PrimaryEffects = primary with { Draws = [] } });
                Color[] baseline = GpuTestDevice.Read(gpu.Target);
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { PrimaryEffects = primary with { Draws = [sprite] } });
                Assert.True(baseline.Zip(GpuTestDevice.Read(gpu.Target)).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 2, $"No original Cross-slash pixels for sprite {sprite.Sprite}.");
            }
        }
        Assert.False(primary.HasCallbacks);
    }
}
