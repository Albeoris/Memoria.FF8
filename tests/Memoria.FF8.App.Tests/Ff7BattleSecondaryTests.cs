using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleSecondaryTests
{
    [Fact]
    public void NativePoolExecutesNewChildrenInOrderAndNeverReusesEarlierSlots()
    {
        Ff7BattleSecondaryState state = new();
        state = Ff7BattleSecondaryEffects.ScheduleSound(state, 0x123, 0);
        state = Ff7BattleSecondaryEffects.ScheduleGun(state, 0, -1, 400, 1, 0x81);
        int randomCalls = 0;
        state = Ff7BattleSecondaryEffects.Advance(state, Point, _ => default, Next);
        Assert.Equal(new ushort[] { 0x123 }, state.SoundIds);
        Assert.Empty(state.Muzzles);
        state = Ff7BattleSecondaryEffects.Advance(state, Point, _ => default, Next);
        Assert.Single(state.Muzzles);
        Assert.Single(state.Sparks);
        Assert.Empty(state.Casings);
        Assert.Empty(state.Dust);
        Assert.Equal(new Ff7BattleBraverPoint(100, -500, -200), state.Muzzles[0].Origin);
        Assert.Equal(1, randomCalls);
        state = Ff7BattleSecondaryEffects.Advance(state, Point, _ => default, Next);
        Assert.False(state.HasCallbacks);
        Assert.Empty(state.Muzzles);

        state = new();
        for (int index = 0; index < 59; index++)
        {
            state = Ff7BattleSecondaryEffects.ScheduleSound(state, (ushort)index, 0);
        }
        state = Ff7BattleSecondaryEffects.ScheduleGun(state, 0, -1, 0, 0, 0x81);
        Assert.Throws<InvalidOperationException>(() => Ff7BattleSecondaryEffects.Advance(state, Point, _ => default, Next));

        int Next() => ++randomCalls;
        static Vector3 Point(int actor, int bone, Vector3 point) => point + new Vector3(100, -500, 200);
    }

    [Fact]
    public void CasingDrawsBeforePhysicsAndRestoresThroughBothBounces()
    {
        Ff7BattleSecondaryState state = Ff7BattleSecondaryEffects.ScheduleGun(new(), 0, 0, 200, 0, 1);
        Ff7BattleSecondaryState? restored = null;
        int calls = 0;
        int restoredCalls = 0;
        int muzzles = 0;
        int sparkPackets = 0;
        int dustPackets = 0;
        int casingPackets = 0;
        for (int frame = 0; frame < 100; frame++)
        {
            state = Ff7BattleSecondaryEffects.Advance(state, Point, _ => default, () => ++calls);
            if (restored is not null)
            {
                restored = Ff7BattleSecondaryEffects.Advance(restored, Point, _ => default, () => ++restoredCalls);
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
                Assert.Equal(calls, restoredCalls);
            }
            if (frame == 0)
            {
                Assert.Equal(new Ff7BattleGunCasing(new(0, -300, 0), 0), Assert.Single(state.Casings));
                Ff7BattleGunCasingCallback casing = Assert.Single(state.Slots.OfType<Ff7BattleGunCasingCallback>());
                Assert.Equal(new Ff7BattleBraverPoint(-61, -448, 23), casing.Position);
                Assert.Equal(new Ff7BattleBraverPoint(-61, -118, 23), casing.Velocity);
                Assert.Equal(4, calls);
            }
            if (frame == 12)
            {
                Ff7BattleGunCasingCallback casing = Assert.Single(state.Slots.OfType<Ff7BattleGunCasingCallback>());
                Assert.Equal(new Ff7BattleBraverPoint(-793, -29, 299), casing.Position);
                Assert.Equal(new Ff7BattleBraverPoint(-26, -66, -4), casing.Velocity);
                Assert.Equal(1, casing.Bounces);
                Assert.Equal(7, calls);
            }
            if (frame == 18)
            {
                Assert.Empty(state.Slots.OfType<Ff7BattleGunCasingCallback>());
                Assert.Contains(state.Dust, dust => dust.Position == new Ff7BattleBraverPoint(-949, -7, 275) && dust.MinimumScale == 512 && dust.Frame == 0);
                Assert.Equal(10, calls);
            }
            muzzles += state.Muzzles.Count;
            sparkPackets += state.Sparks.Count;
            dustPackets += state.Dust.Count;
            casingPackets += state.Casings.Count;
            if (frame == 5)
            {
                restored = JsonSerializer.Deserialize<Ff7BattleSecondaryState>(JsonSerializer.Serialize(state))!;
                restoredCalls = calls;
            }
            if (!state.HasCallbacks)
            {
                break;
            }
        }
        Assert.False(state.HasCallbacks);
        Assert.Equal(10, calls);
        Assert.Equal(1, muzzles);
        Assert.Equal(1, sparkPackets);
        Assert.Equal(24, dustPackets);
        Assert.Equal(19, casingPackets);

        static Vector3 Point(int actor, int bone, Vector3 point) => point + new Vector3(0, -300, 0);
    }

    [Fact]
    public void HitmarkOwnersShareSlotsWithSoundAndPreserveFrozenContinuation()
    {
        Ff7BattleHitmarkDefinition definition = new(0, [new(0, 1, 10, 20, 4096, 0, 3), new(2, 1, -10, 20, 4096, 0, 2)]);
        Ff7BattleSecondaryState state = Ff7BattleSecondaryEffects.ScheduleSound(new(), 5, 2);
        state = Ff7BattleSecondaryEffects.ScheduleHitmark(state, 4, definition, new(100, -200, 300), 350, true);
        Ff7BattleSecondaryState old = new();
        for (int frame = 0; frame < 4; frame++)
        {
            state = Ff7BattleSecondaryEffects.Advance(state, (_, _, _) => throw new InvalidOperationException(), _ => default, () => throw new InvalidOperationException());
            Assert.Equal(frame == 2 ? new ushort[] { 5 } : [], state.SoundIds);
            Assert.Equal(frame == 2 ? 2 : 1, state.Hitmarks.Count);
            Assert.All(state.Hitmarks, mark => Assert.Equal(new Ff7BattleBraverPoint(100, -200, 300), mark.Anchor));
            if (frame == 1)
            {
                Ff7BattleHitmarkState previous = new(4, definition, frame, new(100, -200, 300), 350, true);
                old = Ff7BattleSecondaryEffects.ImportHitmark(new(), previous);
            }
            else if (frame >= 2)
            {
                old = Ff7BattleSecondaryEffects.Advance(old, (_, _, _) => throw new InvalidOperationException(), _ => default, () => throw new InvalidOperationException());
                Assert.Equal(JsonSerializer.Serialize(state.Hitmarks.OrderBy(mark => mark.Definition.Cues[0].OffsetX)), JsonSerializer.Serialize(old.Hitmarks.OrderBy(mark => mark.Definition.Cues[0].OffsetX)));
            }
            state = JsonSerializer.Deserialize<Ff7BattleSecondaryState>(JsonSerializer.Serialize(state))!;
        }
        Assert.False(state.HasCallbacks);
        Assert.Single(state.Hitmarks);
    }

    [Fact]
    public void InstalledGunMeshesSparkAndDustRenderAndRestoreIndependently()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII gun presentation checks.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic, Ff7BattleCureResources.Load(installation), Ff7BattleGrenadeResources.Load(installation));
        renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);
        Ff7BattleSecondaryState state = Ff7BattleSecondaryEffects.ScheduleGun(new(), 0, -1, 500, 0, 4);
        int random = 1;
        int changedFrames = 0;
        for (int frame = 0; frame < 80; frame++)
        {
            state = Ff7BattleSecondaryEffects.Advance(state, (_, _, point) => point + scene.Enemies[0].Placement.Position + new Vector3(0, -500, 0), _ => default, Next);
            renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { SecondaryEffects = state });
            Color[] image = GpuTestDevice.Read(gpu.Target);
            if (baseline.Zip(image).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 4)
            {
                changedFrames++;
            }
            if (frame == 4)
            {
                Ff7BattleSecondaryState restored = JsonSerializer.Deserialize<Ff7BattleSecondaryState>(JsonSerializer.Serialize(state))!;
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { SecondaryEffects = restored });
                Assert.Equal(image.Select(pixel => pixel.PackedValue), GpuTestDevice.Read(gpu.Target).Select(pixel => pixel.PackedValue));
                state = restored;
            }
            if (!state.HasCallbacks)
            {
                break;
            }
        }
        Assert.False(state.HasCallbacks);
        Assert.True(changedFrames >= 8, $"Only {changedFrames} callbacks rendered original gun packets.");

        int Next()
        {
            random = unchecked(random * 214013 + 2531011);
            return random >> 16 & 0x7FFF;
        }
    }
}
