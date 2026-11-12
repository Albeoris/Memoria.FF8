using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleShadowTests
{
    [Fact]
    public void OriginalHostShadowUsesItsResourceHeaderGroundAndSharedDepthBeforeBodies()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root), "Select original FFVII resources.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        Ff7BattleShadowResources shadow = Ff7BattleShadowResources.Load(installation, formation.LocationId);
        Assert.Equal(17, shadow.Vertices.Length);
        Assert.Equal(16, shadow.Faces.Length);
        Assert.Equal(-4, shadow.GroundHeight);
        Assert.Equal(-10, Ff7BattleShadowResources.Load(installation, 1).GroundHeight);
        Ff7BattleRenderer renderer = new(archive, formation.LocationId, shadow: shadow);
        Ff7BattleActorVisual actor = new("rt", new(0, 0, 1700), 0, 0, 0, 0);
        byte[] header = archive.ReadEntry(Assert.Single(archive.Find("rtab")));
        float scaleX = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6)) / 4096f;
        float scaleZ = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(8)) / 4096f;
        short offset = BinaryPrimitives.ReadInt16LittleEndian(header.AsSpan(10));
        Vector3[] expected = shadow.Vertices.Select(vertex => vertex * new Vector3(scaleX, 1, scaleZ) + new Vector3(0, -4, 1700 + (offset == -1 ? 0 : offset))).ToArray();
        Assert.Equal(expected, renderer.CaptureActorShadow(actor));
        Vector3 shift = new(111, -800, -222);
        Assert.Equal(expected.Select(vertex => vertex + new Vector3(shift.X, 0, shift.Z)), renderer.CaptureActorShadow(actor with { PresentationTransform = Matrix4x4.CreateTranslation(shift) }));
        Assert.Equal(expected.Select(vertex => vertex + new Vector3(73, 0, 115)), renderer.CaptureActorShadow(actor with { ModelModifiers = new(ExtraEnabled: true, ExtraX: 73, ExtraY: -900, ExtraZ: 115) }));
        Assert.Equal(renderer.CaptureActorShadow(actor), renderer.CaptureActorShadow(actor with { ClipFrame = renderer.GetClipFrameCount("rt", 0) / 2 }));
        Assert.Empty(renderer.CaptureActorShadow(actor with { ShadowSuppressed = true }));
        Assert.Empty(renderer.CaptureActorShadow(actor with { ModelModifiers = new() { Visible = false } }));
        Capture stage = new();
        SceneCameraPlacement camera = SceneBattleData.Read(formation).Cameras[0];
        renderer.Render(stage, 640, 480, new(camera, []));
        Capture drawn = new();
        renderer.Render(drawn, 640, 480, new(camera, [actor]));
        var actorDraws = drawn.Draws.Skip(stage.Draws.Count).ToArray();
        Assert.True(actorDraws.Length > shadow.Faces.Length);
        Assert.All(actorDraws.Take(shadow.Faces.Length), draw =>
        {
            Assert.Equal(TriangleBlend.Alpha, draw.Material.Blend);
            Assert.Equal(TriangleDepthComparison.GreaterEqual, draw.Material.DepthComparison);
            Assert.False(draw.Material.DepthWrite);
            Assert.Equal(new Vector4(0, 0, 0, 96 / 255f), draw.A.Color);
        });
        Assert.Equal(TriangleBlend.Replace, actorDraws[shadow.Faces.Length].Material.Blend);
        Capture repeat = new();
        renderer.Render(repeat, 640, 480, new(camera, [actor]));
        Assert.Equal(drawn.Draws, repeat.Draws);
        byte[] corrupt = File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleShadow).Path);
        BinaryPrimitives.WriteUInt16LittleEndian(corrupt.AsSpan(152), ushort.MaxValue);
        Assert.Throws<InvalidDataException>(() => Ff7BattleShadowResources.Read(corrupt));
        Assert.Throws<InvalidDataException>(() => Ff7BattleShadowResources.Read(corrupt.AsSpan(0, 12)));
    }

    [Fact]
    public void OriginalHostAndMappedGuestShadowsDarkenTheActualArenaWithoutChangingBodyGeometry()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7) && !string.IsNullOrWhiteSpace(ff8), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        Ff7BattleRenderer renderer = new(archive, formation.LocationId, shadow: Ff7BattleShadowResources.Load(installation, formation.LocationId));
        Ff7BattleActorVisual actor = new("rt", new(0, 0, 1700), 0, 0, 0, 0);
        using GameAssets game = new(ff8!);
        using BattleAssets assets = new(ff8!);
        BattleModel body = assets.LoadPlayer(0);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = 0 });
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(new(0, 0)
        {
            Transform = new(new(0, 0, 2200), new(0, 0, 2200), 0, 0, 4096),
            LinkedTransform = new(default, default),
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
        }, 0, 0, ref random);
        model = NativeBattleSequenceMachine.Advance(NativeBattleGraphicsModels.SelectSequence(model, 1), resource, new(0, 0)).Model;
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        model = model with { Flags2 = (ushort)(model.Flags2!.Value & ~4), Color44 = (rules.SummonColors!.Normal & 0xFFFFFFu) | 0x32000000u, GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, resource.PoseSource!) };
        CrossoverGunbladeHostPlacement placement = new(new(0, 0, 1700), 0);
        CrossoverFf8BattleActorVisual guest = new(body, null, model, placement.Matrix) { ShadowSource = rules.ShadowSource };
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        List<int> hostPixels = [];
        List<int> guestPixels = [];
        foreach (SceneCameraPlacement camera in SceneBattleData.Read(formation).Cameras.Distinct())
        {
            hostPixels.Add(Darkened(new(camera, [actor with { ShadowSuppressed = true }]), new(camera, [actor])));
            guestPixels.Add(Darkened(new(camera, []) { Guest = guest with { ShadowSuppressed = true } }, new(camera, []) { Guest = guest }));
        }
        Assert.True(hostPixels.Max() > 16, $"The native shadow must survive actual arena depth: {string.Join(',', hostPixels)}.");
        Assert.True(guestPixels.Max() > 16, $"The mapped native shadow must survive actual arena depth: {string.Join(',', guestPixels)}.");

        int Darkened(Ff7BattleRenderFrame disabled, Ff7BattleRenderFrame enabled)
        {
            renderer.Render(gpu, 640, 480, disabled);
            Microsoft.Xna.Framework.Color[] baseline = GpuTestDevice.Read(gpu.Target);
            renderer.Render(gpu, 640, 480, enabled);
            Microsoft.Xna.Framework.Color[] drawn = GpuTestDevice.Read(gpu.Target);
            int darkened = 0;
            for (int index = 0; index < baseline.Length; index++)
            {
                Assert.True(drawn[index].R <= baseline[index].R && drawn[index].G <= baseline[index].G && drawn[index].B <= baseline[index].B, "An ordinary shadow must not brighten the stage or alter body geometry.");
                if (drawn[index].R < baseline[index].R || drawn[index].G < baseline[index].G || drawn[index].B < baseline[index].B)
                {
                    darkened++;
                }
            }
            return darkened;
        }
    }

    [Fact]
    public void OriginalGuestShadowReusesTheNativePoseBoundsFanAndSuppressionFlags()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root), "Select original FFVIII resources.");
        using GameAssets game = new(root!);
        using BattleAssets assets = new(root!);
        BattleModel body = assets.LoadPlayer(0);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = 0 });
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(new(0, 0)
        {
            Transform = new(new(0, 0, 2200), new(0, 0, 2200), 0, 0, 4096),
            LinkedTransform = new(default, default),
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
        }, 0, 0, ref random);
        model = NativeBattleSequenceMachine.Advance(NativeBattleGraphicsModels.SelectSequence(model, 1), resource, new(0, 0)).Model;
        model = model with { Flags0 = 2, Flags2 = 0, Color44 = 0x32404040, GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, resource.PoseSource!) };
        NativeBattleShadowSource source = game.ReadNativeBattleGraphicsRules().ShadowSource!;
        CrossoverFf8BattleActorVisual visual = new(body, null, model, Matrix4x4.Identity) { ShadowSource = source };
        Capture capture = Draw(visual);
        Assert.Equal(8, capture.Draws.Count);
        Assert.All(capture.Draws, draw =>
        {
            Assert.Equal(TriangleBlend.Subtract, draw.Material.Blend);
            Assert.Equal(TriangleDepthComparison.GreaterEqual, draw.Material.DepthComparison);
            Assert.False(draw.Material.DepthWrite);
            Assert.Equal(new Vector4(64 / 255f, 64 / 255f, 64 / 255f, 1), draw.A.Color);
            Assert.Equal(new Vector4(0, 0, 0, 1), draw.B.Color);
            Assert.Equal(new Vector4(0, 0, 0, 1), draw.C.Color);
        });
        Assert.Equal(capture.Draws, Draw(visual).Draws);
        Assert.Empty(Draw(visual with { ShadowSuppressed = true }).Draws);
        Assert.Empty(Draw(visual with { Model = model with { Flags0 = 0x22 } }).Draws);
        Assert.Empty(Draw(visual with { Model = model with { Flags2 = 0x20 } }).Draws);
        Assert.Empty(Draw(visual with { Model = model with { Color44 = 0x32000000 } }).Draws);

        static Capture Draw(CrossoverFf8BattleActorVisual visual)
        {
            Capture capture = new();
            CrossoverFf8BattleActorRenderer.DrawShadow(capture, visual, value => new(value.X, value.Z - 2200, 10000 - value.Y), 640, 480, 400);
            return capture;
        }
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<(TriangleVertex A, TriangleVertex B, TriangleVertex C, TriangleMaterial Material)> Draws { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Draws.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Draws.Add((a, b, c, material));
        }

        public void End()
        {
        }
    }
}
