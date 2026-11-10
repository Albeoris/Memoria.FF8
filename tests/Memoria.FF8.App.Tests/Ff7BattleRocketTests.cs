using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleRocketTests
{
    [Fact]
    public void OriginalRocketClockMatchesNativeTrajectoryAndAll618RandomDraws()
    {
        Ff7BattleRocketState state = Ff7BattleRocketEffect.Start(0x10);
        Ff7BattleRocketState? restored = null;
        int random = 0;
        int restoredRandom = 0;
        int missiles = 0;
        int points = 0;
        int exhaust = 0;
        int smoke = 0;
        int explosions = 0;
        for (int frame = 0; frame <= 40; frame++)
        {
            Ff7BattleRocketUpdate update = Ff7BattleRocketEffect.Advance(state, Source, Target, () => ++random);
            state = update.State;
            if (restored is not null)
            {
                restored = Ff7BattleRocketEffect.Advance(restored, Source, Target, () => ++restoredRandom).State;
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
                Assert.Equal(random, restoredRandom);
            }
            Assert.Equal(frame == 20 ? 0x10 : 0, update.ImpactTargets);
            missiles += state.Missiles.Count;
            points += state.Points.Count;
            exhaust += state.Sprites.Count(sprite => sprite.Kind == 0);
            smoke += state.Sprites.Count(sprite => sprite.Kind == 1);
            explosions += state.Sprites.Count(sprite => sprite.Kind == 2);
            if (frame == 0)
            {
                Assert.Equal((short)302, Assert.Single(state.Missiles).Yaw);
                Assert.Equal(new Ff7BattleBraverPoint(15, -455, 30), state.Targets[0].Projectile!.Position);
                Assert.Equal(new Ff7BattleBraverPoint(-44, -500, -89), Assert.Single(state.Sprites).Position);
                Assert.Equal(new Ff7BattleBraverPoint(-243, -698, -286), state.Points[0]);
                Assert.Equal(30, random);
            }
            if (frame == 5)
            {
                Assert.Equal(new Ff7BattleBraverPoint(90, -305, 180), state.Targets[0].Projectile!.Position);
                Assert.Equal((short)20, state.Targets[0].Projectile!.Lift);
                Assert.Equal(180, random);
            }
            if (frame == 6)
            {
                Assert.Equal(new Ff7BattleBraverPoint(150, -335, 300), state.Targets[0].Projectile!.Position);
                Assert.Equal(new Ff7BattleBraverPoint(46, -305, 91), state.Sprites[0].Position);
                Assert.Equal(13, state.Sprites.Count);
            }
            if (frame == 10)
            {
                restored = JsonSerializer.Deserialize<Ff7BattleRocketState>(JsonSerializer.Serialize(state))!;
                restoredRandom = random;
            }
            if (frame == 19)
            {
                Assert.Equal(new Ff7BattleBraverPoint(930, -725, 1860), state.Targets[0].Projectile!.Position);
                Assert.Equal(new Ff7BattleBraverPoint(826, -695, 1651), state.Sprites[0].Position);
                Assert.Equal(600, random);
            }
            if (frame == 23)
            {
                Assert.Equal(new Ff7BattleBraverPoint[] { new(1001, -698, 1903), new(1004, -695, 1906), new(1007, -692, 1909), new(1010, -689, 1912), new(1013, -686, 1915), new(1016, -683, 1918) }, state.Explosions.Select(explosion => explosion.Position));
                Assert.Equal(618, random);
            }
        }
        Assert.True(state.Completed);
        Assert.Equal(20, missiles);
        Assert.Equal(200, points);
        Assert.Equal(20, exhaust);
        Assert.Equal(168, smoke);
        Assert.Equal(96, explosions);
        Assert.Equal(618, random);

        static Vector3 Source() => new(0, -500, 0);
        static Vector3 Target(int slot) => new(900, -800, 1800);
    }

    [Fact]
    public void GroupRocketsKeepFourCallbackStaggerAndColdContinuation()
    {
        Ff7BattleRocketState state = Ff7BattleRocketEffect.Start(7);
        Ff7BattleRocketState? restored = null;
        int random = 0;
        int restoredRandom = 0;
        int sources = 0;
        int impacts = 0;
        for (int frame = 0; frame <= 48; frame++)
        {
            Ff7BattleRocketUpdate update = Ff7BattleRocketEffect.Advance(state, Source, Target, () => ++random);
            state = update.State;
            impacts |= update.ImpactTargets;
            if (restored is not null)
            {
                restored = Ff7BattleRocketEffect.Advance(restored, () => throw new InvalidOperationException(), Target, () => ++restoredRandom).State;
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
                Assert.Equal(random, restoredRandom);
            }
            if (frame == 12)
            {
                restored = JsonSerializer.Deserialize<Ff7BattleRocketState>(JsonSerializer.Serialize(state))!;
                restoredRandom = random;
            }
            if (frame is 20 or 24 or 28)
            {
                Assert.Equal(1 << ((frame - 20) / 4), update.ImpactTargets);
            }
        }
        Assert.True(state.Completed);
        Assert.Equal(3, sources);
        Assert.Equal(7, impacts);
        Assert.Equal(1854, random);

        Vector3 Source()
        {
            sources++;
            return new(0, -500, 0);
        }

        static Vector3 Target(int slot) => new(slot * 300, -400, 1800);
    }

    [Fact]
    public void InstalledMissileExhaustSmokeExplosionAndNaturalDepthPointsRender()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII rocket presentation checks.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Vector3 source = scene.Enemies[0].Placement.Position + new Vector3(0, -600, 0);
        Vector3 target = source + new Vector3(1800, 0, 1800);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic);
        renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);
        Ff7BattleRocketState state = Ff7BattleRocketEffect.Start(0x10);
        int random = 0;
        for (int frame = 0; frame < 40; frame++)
        {
            state = Ff7BattleRocketEffect.Advance(state, () => source, _ => target, () => ++random).State;
            if (frame is 0 or 10 or 24)
            {
                state = JsonSerializer.Deserialize<Ff7BattleRocketState>(JsonSerializer.Serialize(state))!;
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { Rockets = state });
                Assert.True(baseline.Zip(GpuTestDevice.Read(gpu.Target)).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 2, $"No original Rocket Launcher packets at callback {frame}.");
            }
            if (frame == 0)
            {
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { Rockets = state with { Missiles = [], Sprites = [] } });
                Assert.True(baseline.Zip(GpuTestDevice.Read(gpu.Target)).Any(pair => pair.First.PackedValue != pair.Second.PackedValue), "Native one-pixel rocket points did not render independently.");
            }
        }
    }
}
