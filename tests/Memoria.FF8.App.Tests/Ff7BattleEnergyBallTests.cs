using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleEnergyBallTests
{
    [Fact]
    public void OriginalEnergyBallTreeMatchesNativeMatrixPacketsAndRandomStream()
    {
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleEnergyBall(new(), 0, 0x10, 5);
        Ff7BattlePrimaryState? restored = null;
        int random = 0;
        int restoredRandom = 0;
        int meshes = 0;
        int lights = 0;
        int blasts = 0;
        int peak = 0;
        List<(int Frame, Ff7BattlePrimaryPositionedSound Cue)> sounds = [];
        Ff7BattlePrimaryContext context = Context(new(0, -500, 0), new(900, -800, 1800), () => ++random);
        for (int frame = 0; frame <= 58; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            if (restored is not null)
            {
                restored = Ff7BattlePrimaryEffects.Advance(restored, context with { NextCrt = () => ++restoredRandom });
                Assert.Equal(random, restoredRandom);
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
            }
            Ff7BattlePrimaryEnergyBallMeshDraw[] models = state.Draws.OfType<Ff7BattlePrimaryEnergyBallMeshDraw>().ToArray();
            Ff7BattlePrimaryEnergyBallSpriteDraw[] sprites = state.Draws.OfType<Ff7BattlePrimaryEnergyBallSpriteDraw>().ToArray();
            meshes += models.Length;
            lights += sprites.Count(draw => draw.Resource == 20);
            blasts += sprites.Count(draw => draw.Resource == 21);
            peak = Math.Max(peak, state.OccupiedSlotCount);
            sounds.AddRange(state.PositionedSounds.Select(cue => (frame, cue)));
            Assert.Equal(frame == 35 ? (ushort)0x10 : (ushort)0, state.ImpactTargets);
            Assert.Empty(state.WaveSparks);
            if (frame == 1)
            {
                Assert.Equal(new Ff7BattleSearchScopeBasis(new(7632, 0, -1518), new(148, 7744, 746), new(1510, -761, 7594)), models[0].Basis);
                Assert.Equal((byte)12, models[0].Alpha);
            }
            if (frame == 20)
            {
                Ff7BattlePrimaryEnergyBallMeshDraw ring = Assert.Single(models, draw => draw.Model == 2);
                Assert.Equal(new Ff7BattleSearchScopeBasis(new(10983, -33, -5496), new(-5433, 1809, -10869), new(837, 12150, 1599)), ring.Basis);
                Assert.Equal(new Ff7BattleSearchScopeBasis(new(7326, 0, -3662), new(0, 0, 0), new(538, 8102, 1078)), Assert.Single(models, draw => draw.Model == 7).Basis);
            }
            if (frame == 28)
            {
                Assert.Equal(new[] { new Ff7BattleBraverPoint(317, -1382, 1219), new Ff7BattleBraverPoint(320, -1379, 1222) }, sprites.Where(draw => draw.Resource == 21).Select(draw => draw.Position));
                restored = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
                restoredRandom = random;
            }
            if (frame == 30)
            {
                Assert.Equal(new Ff7BattleBraverPoint(899, -799, 1799), Assert.Single(models, draw => draw.Model == 17).Position);
            }
        }
        Assert.Equal(106, random);
        Assert.Equal(31, peak);
        Assert.Equal(216, meshes);
        Assert.Equal(52, lights);
        Assert.Equal(480, blasts);
        Assert.Equal(new[] { (0, new Ff7BattlePrimaryPositionedSound(0x1C8, 0, 64)), (35, new Ff7BattlePrimaryPositionedSound(0xFA, 4, 64)) }, sounds);
        Assert.False(state.HasCallbacks);
        Assert.Equal((short)0, state.StageTransparency);
    }

    [Fact]
    public void InstalledEnergyBallUsesOriginalRsdTexturesAndSpritePagesAfterRestore()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII Energy Ball presentation checks.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic);
        Vector3 source = scene.Enemies[0].Placement.Position + new Vector3(0, -600, 0);
        Vector3 target = source + new Vector3(900, 0, 900);
        int random = 0;
        Ff7BattlePrimaryContext context = Context(source, target, NextRandom);
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleEnergyBall(new(), 0, 0x10);
        for (int frame = 0; frame <= 57; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            if (frame is 1 or 21 or 30 or 44)
            {
                state = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { PrimaryEffects = state with { Draws = [] } });
                Color[] baseline = GpuTestDevice.Read(gpu.Target);
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { PrimaryEffects = state });
                Assert.True(baseline.Zip(GpuTestDevice.Read(gpu.Target)).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 2, $"No original Energy Ball pixels at callback {frame}.");
            }
            else if (frame is >= 20 and <= 35)
            {
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []) { PrimaryEffects = state });
            }
        }

        int NextRandom()
        {
            random++;
            return random ^ (random >> 1);
        }
    }

    [Fact]
    public void NativeModelColorWeightPreservesMeshCoverageAndVertexAlpha()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII Energy Ball color checks.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic);
        Vector3 source = scene.Enemies[0].Placement.Position + new Vector3(0, -600, 0);
        Ff7BattlePrimaryContext context = Context(source, source + new Vector3(900, 0, 900), () => throw new InvalidOperationException());
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleEnergyBall(new(), 0, 0x10);
        state = Ff7BattlePrimaryEffects.Advance(Ff7BattlePrimaryEffects.Advance(state, context), context);
        Ff7BattlePrimaryEnergyBallMeshDraw mesh = state.Draws.OfType<Ff7BattlePrimaryEnergyBallMeshDraw>().First();
        TriangleCapture baseline = Capture([]);
        (TriangleVertex Vertex, TriangleMaterial Material)[] lit = Capture([mesh with { Alpha = 255 }]).Packets.Skip(baseline.Packets.Count).ToArray();
        (TriangleVertex Vertex, TriangleMaterial Material)[] dark = Capture([mesh with { Alpha = 0 }]).Packets.Skip(baseline.Packets.Count).ToArray();
        Assert.NotEmpty(lit);
        Assert.Equal(lit.Length, dark.Length);
        Assert.Contains(lit, packet => packet.Vertex.Color.X > 0 || packet.Vertex.Color.Y > 0 || packet.Vertex.Color.Z > 0);
        for (int i = 0; i < lit.Length; i++)
        {
            Assert.Equal(lit[i].Material, dark[i].Material);
            Assert.Equal(lit[i].Vertex.Color.W, dark[i].Vertex.Color.W);
            Assert.Equal(new Vector4(0, 0, 0, lit[i].Vertex.Color.W), dark[i].Vertex.Color);
        }

        TriangleCapture Capture(IReadOnlyList<Ff7BattlePrimaryDraw> draws)
        {
            TriangleCapture capture = new();
            renderer.Render(capture, 320, 240, new(scene.Cameras[0], []) { PrimaryEffects = state with { Draws = draws } });
            return capture;
        }
    }

    private sealed class TriangleCapture : ITriangleRenderer
    {
        public List<(TriangleVertex Vertex, TriangleMaterial Material)> Packets { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Packets.Add((a, material));
        }

        public void End()
        {
        }
    }

    private static Ff7BattlePrimaryContext Context(Vector3 source, Vector3 target, Func<int> random)
    {
        return new((_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), random, JointPoint: (actor, selector) => (actor, selector) switch
        {
            (0, 9) => source,
            (4, 0) => target,
            _ => throw new InvalidOperationException("Unexpected native Energy Ball joint.")
        });
    }
}
