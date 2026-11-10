using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleSearchScopeTests
{
    [Fact]
    public void OriginalScopeRetainsFourEmittersTwoSpriteStagesAndCounterCarry()
    {
        Ff7BattleSearchScopeState state = Ff7BattleSearchScopeEffect.Start(0x30, new(0, -500, 0), new(0, -500, 2800));
        Assert.Equal(new Ff7BattleSearchScopeBasis(new(4096, 0, 0), new(0, 4096, 0), new(0, 0, 4095)), state.Basis);
        Assert.Equal(new Ff7BattleSearchScopeBasis(new(3663, 0, -1831), new(269, 4051, 539), new(1811, -603, 3623)), Ff7BattleSearchScopeEffect.Start(0x10, Vector3.Zero, new(900, -300, 1800)).Basis);
        Ff7BattleSearchScopeState? restored = null;
        int meshes = 0;
        int sprites = 0;
        int impacts = 0;
        for (int frame = 0; frame <= 51; frame++)
        {
            Ff7BattleSearchScopeUpdate update = Ff7BattleSearchScopeEffect.Advance(state);
            state = update.State;
            if (restored is not null)
            {
                restored = Ff7BattleSearchScopeEffect.Advance(restored).State;
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
            }
            meshes += state.Meshes.Count;
            sprites += state.Sprites.Count;
            impacts |= update.ImpactTargets;
            Assert.Equal(frame == 32 ? 0x30 : 0, update.ImpactTargets);
            if (frame == 0)
            {
                Assert.Equal(new short[] { 4096, 4072, 4032, 4024 }, state.Meshes.Select(mesh => mesh.Transparency));
                Assert.Equal(new BattleRotationWords(-1024, 0, 0), state.Meshes[0].Rotation);
                Assert.Equal(new BattleRotationWords(-1024, 0, 0), state.Meshes[2].Rotation);
                Assert.Equal(new byte[] { 0, 1, 3, 4 }, state.Meshes.Select(mesh => Ff7BattleSearchScopeEffect.Alpha(mesh.Transparency)));
            }
            if (frame == 14)
            {
                restored = JsonSerializer.Deserialize<Ff7BattleSearchScopeState>(JsonSerializer.Serialize(state))!;
            }
            if (frame == 1)
            {
                Assert.Equal(new BattleRotationWords(-1063, -49, 0), state.Meshes[0].Rotation);
                Assert.Equal(new short[] { 3968, 3976, 3904, 3928 }, state.Meshes.Select(mesh => mesh.Transparency));
            }
            if (frame == 25)
            {
                Assert.Equal(new BattleRotationWords(-985, 252, 0), state.Meshes[0].Rotation);
                Assert.Equal(new short[] { 3072, 3352, 3136, 3400 }, state.Meshes.Select(mesh => mesh.Transparency));
            }
            if (frame == 20)
            {
                Assert.Equal(new[] { 20, 20, 1, 1 }, state.Sprites.Take(4).Select(sprite => sprite.StartedAt));
            }
            if (frame == 31)
            {
                Assert.Equal(new BattleRotationWords(-985, 50, 0), state.Meshes[0].Rotation);
                Assert.Equal(new short[] { 3840, 3928, 3904, 3976 }, state.Meshes.Select(mesh => mesh.Transparency));
            }
        }
        Assert.Equal(128, meshes);
        Assert.Equal(1280, sprites);
        Assert.Equal(0x30, impacts);
        Assert.Equal((short)4, state.ScaleCounter);
        Assert.True(state.Completed);
        Ff7BattleSearchScopeState next = Ff7BattleSearchScopeEffect.Start(0x10, Vector3.Zero, new(0, 0, 2800), state.ScaleCounter);
        Assert.Equal((short)3968, Ff7BattleSearchScopeEffect.Advance(next).State.Meshes[0].Transparency);
    }

    [Fact]
    public void StageFadePreservesNativeFixedPointRemainderAndBusyAdmission()
    {
        Ff7BattleStageFadeState state = Ff7BattleStageFade.FadeTo(new(), 1024, 10);
        Assert.Equal(state, Ff7BattleStageFade.FadeTo(state, 0, 1));
        List<short> values = [];
        for (int frame = 0; frame < 10; frame++)
        {
            state = Ff7BattleStageFade.Advance(state);
            values.Add(state.Transparency);
            state = JsonSerializer.Deserialize<Ff7BattleStageFadeState>(JsonSerializer.Serialize(state))!;
        }
        Assert.Equal(new short[] { 102, 204, 307, 409, 511, 614, 716, 819, 921, 1023 }, values);
        Assert.Equal((byte)192, state.Alpha);
        Assert.Null(state.Ramp);
        state = Ff7BattleStageFade.FadeTo(state, 0, 10);
        for (int frame = 0; frame < 10; frame++)
        {
            state = Ff7BattleStageFade.Advance(state);
        }
        Assert.Equal((short)0, state.Transparency);
        Assert.Equal((byte)255, state.Alpha);
    }

    [Fact]
    public void OriginalScopeMeshesAndBothSpriteStagesRasterizeAfterColdRestore()
    {
        Ff7Installation installation = Installation();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[324];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Vector3 target = scene.Enemies[0].Placement.Position + new Vector3(0, -350, 0);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic);
        renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);
        renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { StageFade = new(Transparency: 4096) });
        Assert.True(GpuTestDevice.Read(gpu.Target).All(pixel => (pixel.PackedValue & 0x00FFFFFF) == 0), "Native fully transparent arena still produced visible stage pixels.");
        renderer.Render(gpu, 320, 240, new(scene.Cameras[0], [new("rt", scene.Enemies[0].Placement.Position, 0, 0, 0, 0)]) { StageFade = new(Transparency: 4096) });
        Assert.True(GpuTestDevice.Read(gpu.Target).Count(pixel => (pixel.PackedValue & 0x00FFFFFF) != 0) > 16, "Original stage transparency must preserve the independently drawn actor pass.");
        Ff7BattleSearchScopeState state = Ff7BattleSearchScopeEffect.Start(0x10, target + new Vector3(1800, -100, -1800), target);
        for (int frame = 0; frame <= 51; frame++)
        {
            state = Ff7BattleSearchScopeEffect.Advance(state).State;
            if (frame is 8 or 25 or 42)
            {
                state = JsonSerializer.Deserialize<Ff7BattleSearchScopeState>(JsonSerializer.Serialize(state))!;
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { SearchScope = state });
                Assert.True(baseline.Zip(GpuTestDevice.Read(gpu.Target)).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 8, $"Original Search Scope has no pixels at callback {frame}.");
            }
        }
        Assert.True(state.Completed);
    }

    [Fact]
    public void NativeFirstFlagWordFlipsOriginalSpriteUvs()
    {
        Ff7Installation installation = Installation();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        BattleBillboardQuad first = Assert.Single(BattleBillboard.Read(magic.ReadEntry(magic.Find("biribiri.s").Single()))[0]);
        BattleBillboardQuad flipped = Assert.Single(BattleBillboard.Read(magic.ReadEntry(magic.Find("b_flip.s").Single()))[0]);
        Assert.Equal((ushort)0x100, first.Flags);
        Assert.Equal((ushort)0x102, flipped.Flags);
        Assert.Equal((ushort)0, first.Flip);
        Assert.Equal((ushort)0, flipped.Flip);
        IReadOnlyList<IReadOnlyList<BattleBillboardQuad>> frames = BattleBillboard.Read(magic.ReadEntry(magic.Find("b_flip.s").Single()));
        PcTexture texture = PcTexture.Read(magic.ReadEntry(magic.Find("biribiri.tex").Single()));
        List<Color[]> images = [];
        for (int variant = 0; variant < 2; variant++)
        {
            IReadOnlyList<IReadOnlyList<BattleBillboardQuad>> selected = variant == 0 ? frames : frames.Select(quads => (IReadOnlyList<BattleBillboardQuad>)quads.Select(quad => quad with { Flags = (ushort)(quad.Flags & ~3) }).ToArray()).ToArray();
            Ff7BattleBillboardRenderer renderer = new(selected, [texture], _ => 0);
            gpu.Begin(320, 240, new(0, 0, 0, 1), 0);
            renderer.Draw(gpu, 0, new(0, 0, 3000), 320, 240, 320, 2);
            gpu.End();
            images.Add(GpuTestDevice.Read(gpu.Target));
        }
        Assert.True(images[0].Zip(images[1]).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 16, "SPT first-word vertical flip did not reach the GPU UV consumer.");
    }

    private static Ff7Installation Installation()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII scope presentation checks.");
        return Ff7Installation.Discover(root!);
    }
}
