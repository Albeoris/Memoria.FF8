using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleLimitPreparationTests
{
    [Fact]
    public void E0ReadsLivePublishedSourceOnceAndCreatesWrapperInCurrentSecondaryPass()
    {
        Ff7BattleSecondaryState state = Ff7BattleSecondaryEffects.ScheduleE0(new(), 2, 7);
        state = JsonSerializer.Deserialize<Ff7BattleSecondaryState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(1, Assert.IsType<Ff7BattleLimitPreparationEntry>(state.Slots[0]).Age);
        int reads = 0;
        state = Ff7BattleSecondaryEffects.Advance(state, (actor, bone, offset) =>
        {
            Assert.Equal(2, actor);
            Assert.Equal(7, bone);
            Assert.Equal(Vector3.Zero, offset);
            reads++;
            return new(1200, -500, 1800);
        }, _ => throw new InvalidOperationException(), () => throw new InvalidOperationException());
        Assert.Equal(1, reads);
        Assert.Equal(new ushort[] { 0x24 }, state.SoundIds);
        Assert.Null(state.Slots[0]);
        Assert.Equal(1, Assert.IsType<Ff7BattleLimitPreparationEmitter>(state.Slots[1]).Age);
        Assert.Equal(1, Assert.IsType<Ff7BattleLimitPreparationMesh>(state.Slots[2]).Age);
        Ff7BattleLimitPreparationDraw draw = Assert.Single(state.LimitPreparations);
        Assert.Equal(new Ff7BattleBraverPoint(1200, -500, 1800), draw.Position);
        Assert.Equal(0, draw.Frame);
        state = Ff7BattleSecondaryEffects.Advance(state, (_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), () => throw new InvalidOperationException());
        Assert.Empty(state.SoundIds);
        Assert.Equal(1, Assert.Single(state.LimitPreparations).Frame);
    }

    [Fact]
    public void NativeSecondaryEmitterRetainsFourSixteenFrameChildrenThroughJson()
    {
        Ff7BattleSecondaryState state = Ff7BattleSecondaryEffects.ScheduleLimitPreparation(new(), new(1200, -500, 1800));
        Ff7BattleSecondaryState? restored = null;
        int packets = 0;
        for (int frame = 0; frame <= 28; frame++)
        {
            state = Advance(state);
            if (restored is not null)
            {
                restored = Advance(restored);
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
            }
            packets += state.LimitPreparations.Count;
            Assert.All(state.DrawOrder, draw => Assert.Equal(Ff7BattleSecondaryDrawKind.LimitPreparation, draw.Kind));
            if (frame == 8)
            {
                Assert.Equal(new[] { 8, 4, 0 }, state.LimitPreparations.Select(draw => draw.Frame));
                Assert.Equal(0x3000, state.LimitPreparations[0].TransverseScale);
                Assert.Equal(0x6000, state.LimitPreparations[0].VerticalScale);
                Assert.Equal(0, state.LimitPreparations[0].Transparency);
                restored = JsonSerializer.Deserialize<Ff7BattleSecondaryState>(JsonSerializer.Serialize(state))!;
            }
            if (frame == 27)
            {
                Ff7BattleLimitPreparationDraw final = Assert.Single(state.LimitPreparations);
                Assert.Equal(15, final.Frame);
                Assert.Equal(3584, final.Transparency);
                Assert.False(state.HasCallbacks);
            }
        }
        Assert.Equal(64, packets);

        static Ff7BattleSecondaryState Advance(Ff7BattleSecondaryState current)
        {
            return Ff7BattleSecondaryEffects.Advance(current, (_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), () => throw new InvalidOperationException());
        }
    }

    [Fact]
    public void OriginalPreparationMeshAndTextureRenderWithColdSecondaryState()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII Limit preparation presentation checks.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic);
        renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);
        Ff7BattleSecondaryState state = Ff7BattleSecondaryEffects.ScheduleLimitPreparation(new(), scene.Enemies[0].Placement.Position);
        for (int frame = 0; frame <= 27; frame++)
        {
            state = Ff7BattleSecondaryEffects.Advance(state, (_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), () => throw new InvalidOperationException());
            if (frame is 4 or 8 or 15 or 24 or 27)
            {
                state = JsonSerializer.Deserialize<Ff7BattleSecondaryState>(JsonSerializer.Serialize(state))!;
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { SecondaryEffects = state });
                Assert.True(baseline.Zip(GpuTestDevice.Read(gpu.Target)).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 2, $"No original Limit preparation mesh pixels at callback {frame}.");
            }
        }
    }
}
