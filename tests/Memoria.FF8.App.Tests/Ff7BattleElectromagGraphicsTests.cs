using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleElectromagGraphicsTests
{
    [Fact]
    public void NativeChildPacketsRetainPreincrementTransformColorAndLifetime()
    {
        Ff7BattlePrimaryCallback?[] slots = new Ff7BattlePrimaryCallback?[Ff7BattlePrimaryEffects.Capacity];
        Ff7BattleBraverPoint point = new(100, -200, 300);
        slots[0] = new Ff7BattlePrimaryElectromagCore(point) { Age = 1 };
        slots[1] = new Ff7BattlePrimaryElectromagSourceRing(point) { Age = 9 };
        slots[2] = new Ff7BattlePrimaryElectromagShell(point) { Age = 14 };
        slots[3] = new Ff7BattlePrimaryElectromagRock(point, 2, 0) { Age = 14 };
        slots[4] = new Ff7BattlePrimaryElectromagExplosion(point) { Age = 14 };
        Ff7BattlePrimaryState source = new() { Slots = slots };
        string saved = JsonSerializer.Serialize(source);
        Ff7BattlePrimaryState actual = Ff7BattlePrimaryEffects.Advance(source);
        Ff7BattlePrimaryState cold = Ff7BattlePrimaryEffects.Advance(JsonSerializer.Deserialize<Ff7BattlePrimaryState>(saved)!);
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(cold));
        Assert.Equal(saved, JsonSerializer.Serialize(source));
        Ff7BattleElectromagMeshDraw[] core = actual.Draws.OfType<Ff7BattleElectromagMeshDraw>().Where(draw => draw.Slot == 0).ToArray();
        Assert.Equal(new[] { 2, 3 }, core.Select(draw => draw.Resource));
        Assert.Equal(new byte[] { 240, 120 }, core.Select(draw => draw.Weight));
        Assert.All(core, draw => Assert.Equal(new Ff7BattleSearchScopeBasis(new(640, 0, 0), new(0, 640, 0), new(0, 0, 640)), draw.Basis));
        Ff7BattleElectromagSpriteDraw light = Assert.Single(actual.Draws.OfType<Ff7BattleElectromagSpriteDraw>());
        Assert.Equal((18, 0, 8192, (byte)60, true), (light.Resource, light.Frame, light.Scale, light.Brightness, light.Additive));
        Assert.Equal((byte)25, actual.Draws.OfType<Ff7BattleElectromagMeshDraw>().Single(draw => draw.Slot == 1).Weight);
        Assert.Equal((byte)88, actual.Draws.OfType<Ff7BattleElectromagMeshDraw>().Single(draw => draw.Slot == 2).Weight);
        Assert.Equal(11, actual.Draws.OfType<Ff7BattleElectromagMeshDraw>().Single(draw => draw.Slot == 3).Resource);
        Assert.All(actual.Slots.Skip(1).Take(4), Assert.Null);
        Assert.Equal((byte)255, Ff7BattleElectromagEffect.Weight(0, true));
        Assert.Equal((byte)0, Ff7BattleElectromagEffect.Weight(4096, true));
        Assert.Equal((byte)255, Ff7BattleElectromagEffect.Weight(128));
    }

    [Fact]
    public void NativeSecondaryPacketsPreserveBounceRotationFrameAndOpaqueToAdditiveSwitchAcrossColdRestore()
    {
        Ff7BattleSecondaryCallback callback = new Ff7BattleElectromagSpinningFragment(new(10, -10, 20), new(2, 30, 4), 2);
        for (int age = 0; age < 15; age++)
        {
            Ff7BattleSecondaryState snapshot = new() { Slots = [callback, .. new Ff7BattleSecondaryCallback?[Ff7BattleSecondaryEffects.Capacity - 1]] };
            Ff7BattleSecondaryCallback cold = JsonSerializer.Deserialize<Ff7BattleSecondaryState>(JsonSerializer.Serialize(snapshot))!.Slots[0]!;
            Ff7BattleElectromagSecondaryStep step = Ff7BattleElectromagEffect.AdvanceSecondary(callback);
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(Ff7BattleElectromagEffect.AdvanceSecondary(cold)));
            Ff7BattleElectromagSpriteDraw draw = Assert.Single(step.Draws);
            Assert.Equal(16, draw.Resource);
            Assert.Equal(2, draw.Frame);
            Assert.Equal(5120, draw.Scale);
            Assert.Equal(age >= 8, draw.Additive);
            Assert.Equal(unchecked((short)(age * 512)), draw.Roll);
            Assert.Equal((byte)((age < 7 ? 8 : 15 - age) << 4), draw.Brightness);
            if (age == 0)
            {
                Ff7BattleElectromagSpinningFragment next = Assert.IsType<Ff7BattleElectromagSpinningFragment>(step.Callback);
                Assert.Equal(new Ff7BattleBraverPoint(12, 0, 24), next.Position);
                Assert.Equal(new Ff7BattleBraverPoint(2, -10, 4), next.Velocity);
            }
            if (age == 14)
            {
                Assert.Null(step.Callback);
            }
            else
            {
                callback = step.Callback!;
            }
        }
        Ff7BattleElectromagSecondaryStep stationary = Ff7BattleElectromagEffect.AdvanceSecondary(new Ff7BattleElectromagFragment(new(1, 2, 3), 5));
        Assert.Equal(new Ff7BattleBraverPoint(1, -298, 3), Assert.IsType<Ff7BattleElectromagFragment>(stationary.Callback).Position);
        Assert.False(Assert.Single(stationary.Draws).Additive);
    }

    [Fact]
    public void OriginalElectromagResourcesRenderTheActualCallbackTreeAndColdMidpointOnTheSharedGpu()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for Electromag graphics.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic, Ff7BattleCureResources.Load(installation), Ff7BattleGrenadeResources.Load(installation));
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        uint randomState = 1;
        int randomCalls = 0;
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.ScheduleElectromag(new(), 4, 1, Context(NextRandom));
        Assert.Equal(new ushort[] { 371, 124 }, primary.PositionedSounds.Select(cue => cue.Sound));
        Assert.All(primary.PositionedSounds, cue => Assert.True(cue.PlayCached));
        Ff7BattleSecondaryState secondary = new();
        HashSet<int> meshes = [];
        HashSet<int> sprites = [];
        int draws = 0;
        int changedFrames = 0;
        for (int frame = 0; frame < 55; frame++)
        {
            primary = Ff7BattlePrimaryEffects.Advance(primary, Context(NextRandom));
            foreach (Ff7BattleSecondaryCallback child in primary.TailLaserSecondaries)
            {
                secondary = Ff7BattleSecondaryEffects.ScheduleCallback(secondary, child);
            }
            secondary = Ff7BattleSecondaryEffects.Advance(secondary, (_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), () => throw new InvalidOperationException("Electromag children must not add random draws."));
            meshes.UnionWith(primary.Draws.OfType<Ff7BattleElectromagMeshDraw>().Select(draw => draw.Resource));
            sprites.UnionWith(primary.Draws.OfType<Ff7BattleElectromagSpriteDraw>().Select(draw => draw.Resource));
            sprites.UnionWith(secondary.ElectromagSprites.Select(draw => draw.Resource));
            draws += primary.Draws.Count + secondary.DrawOrder.Count;
            if (frame is 1 or 7 or 16 or 28 or 36 or 45)
            {
                string primaryJson = JsonSerializer.Serialize(primary);
                string secondaryJson = JsonSerializer.Serialize(secondary);
                primary = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(primaryJson)!;
                secondary = JsonSerializer.Deserialize<Ff7BattleSecondaryState>(secondaryJson)!;
                Assert.Equal(primaryJson, JsonSerializer.Serialize(primary));
                Assert.Equal(secondaryJson, JsonSerializer.Serialize(secondary));
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { PrimaryEffects = primary with { Draws = [] }, SecondaryEffects = secondary with { DrawOrder = [] } });
                Color[] baseline = GpuTestDevice.Read(gpu.Target);
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { PrimaryEffects = primary, SecondaryEffects = secondary });
                if (baseline.Zip(GpuTestDevice.Read(gpu.Target)).Any(pair => pair.First.PackedValue != pair.Second.PackedValue))
                {
                    changedFrames++;
                }
            }
            else
            {
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { PrimaryEffects = primary, SecondaryEffects = secondary });
            }
        }
        Assert.Equal(245, randomCalls);
        Assert.Equal(Enumerable.Range(0, 15), meshes.Order());
        Assert.Equal(new[] { 15, 16, 18, 19 }, sprites.Order());
        Assert.True(draws > 1000);
        Assert.True(changedFrames >= 4, $"Original Electromag did not draw the expected distinct phases ({changedFrames}).");
        Assert.False(primary.HasCallbacks);
        Assert.False(secondary.HasCallbacks);

        int NextRandom()
        {
            randomCalls++;
            randomState = unchecked(randomState * 214013 + 2531011);
            return (int)(randomState >> 16 & 32767);
        }
    }

    private static Ff7BattlePrimaryContext Context(Func<int> random)
    {
        return new((_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), random, JointPoint: (actor, selector) => actor == 0 && selector == 0 ? new Vector3(300, -500, 1000) : throw new InvalidOperationException("Unexpected Electromag target joint."));
    }
}
