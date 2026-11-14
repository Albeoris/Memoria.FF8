using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class OriginFloatRenderingTests
{
    [Fact]
    public void FloatMovesOriginalGuestBodyWeaponAndEffectBonesTogetherWhileItsShadowStaysOnTheArena()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7) && !string.IsNullOrWhiteSpace(ff8), "Select both original game installations.");
        using GameAssets game = new(ff8!);
        using BattleAssets assets = new(ff8!);
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals();
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(origin, assets, 0);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7Progress progress = original.Progress with { Party = [0, 1, 255] };
        Ff7BattleSession initial = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, animations: animations);
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), initial.Capture(), [new(0, 0, 0)], []);
        Ff7BattleGuestProjection projection = CrossoverGuestHostActions.Projection(kit);
        initial.BindGuestVitals(new(roster.EncounterId, new('A', 64), 0, 0, 0, origin.FindPhysicalRow(0), kit.HitPoints, (ushort)kit.MaximumHitPoints, projection.Level, projection.Luck, projection.Defense, projection.MagicDefense));
        Ff7BattleSnapshot fixture = initial.Capture();
        fixture = fixture with
        {
            Actors = fixture.Actors.Select(actor => actor with { Actor = actor.Actor with { Statuses = 0x400 } }).ToArray(),
            GuestOriginStatuses = fixture.Actors.Select(actor => new Ff7BattleGuestOriginStatus(actor.Actor.Slot, Ff7GuestBattleStatuses.OriginFloat)).ToArray()
        };
        Ff7BattleSession host = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: fixture, animations: animations);
        CrossoverGunbladeHostAttack attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.RenderCameraOutput, _ => 128, readHostImpactAnchor: host.GuestEffectTargetAnchor, readHostTargetBone: host.GuestEffectTargetBone);
        attack.PrepareStanding(host.VisualPlacement(0));
        CrossoverGuestHostActions provider = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets);
        host.BindGuestAttack(provider);
        host.Advance(waitForCommand: true);
        int offset = host.RenderActorFloatOffset(0);
        Assert.True(offset < 0);
        CrossoverFf8BattleActorVisual grounded = attack.Visual!;
        CrossoverFf8BattleActorVisual floating = provider.Visual!;
        Assert.NotNull(floating.Weapon);
        Assert.Equal(grounded.Model.Transform!.Vertical34, floating.Model.Transform!.Vertical34);
        Assert.Equal(offset, floating.ShadowGroundOffsetY);
        Ff7BattleActorGeometry groundGeometry = CrossoverFf8BattleActorRenderer.CaptureActorGeometry(grounded);
        Ff7BattleActorGeometry floatGeometry = CrossoverFf8BattleActorRenderer.CaptureActorGeometry(floating);
        Vector3 delta = new(0, offset, 0);
        Assert.Equal(groundGeometry.GuestFaces.Length, floatGeometry.GuestFaces.Length);
        for (int face = 0; face < groundGeometry.GuestFaces.Length; face++)
        {
            Near(groundGeometry.GuestFaces[face].A + delta, floatGeometry.GuestFaces[face].A);
            Near(groundGeometry.GuestFaces[face].B + delta, floatGeometry.GuestFaces[face].B);
            Near(groundGeometry.GuestFaces[face].C + delta, floatGeometry.GuestFaces[face].C);
        }
        for (int bone = 0; bone < groundGeometry.Bones.Length; bone++)
        {
            Near(groundGeometry.Bones[bone].Start + delta, floatGeometry.Bones[bone].Start);
            Near(groundGeometry.Bones[bone].End + delta, floatGeometry.Bones[bone].End);
        }
        Near(groundGeometry.GuestRoot!.Value + delta, floatGeometry.GuestRoot!.Value);
        Near(groundGeometry.GuestEffectAnchor!.Value + delta, floatGeometry.GuestEffectAnchor!.Value);
        Capture groundShadow = new();
        Capture floatShadow = new();
        Vector3 Camera(Vector3 point) => new(point.X, point.Z - 1700, 10000 - point.Y);
        CrossoverFf8BattleActorRenderer.DrawShadow(groundShadow, grounded, Camera, 640, 480, 400);
        CrossoverFf8BattleActorRenderer.DrawShadow(floatShadow, floating, Camera, 640, 480, 400);
        Assert.NotEmpty(groundShadow.Vertices);
        Assert.Equal(groundShadow.Vertices, floatShadow.Vertices);
        Ff7BattleRenderer renderer = new(archive, formation.LocationId, shadow: Ff7BattleShadowResources.Load(installation, formation.LocationId));
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.Render(gpu, 640, 480, new(host.RenderCameraOutput, []) { Guest = grounded });
        Microsoft.Xna.Framework.Color[] before = GpuTestDevice.Read(gpu.Target);
        renderer.Render(gpu, 640, 480, new(host.RenderCameraOutput, []) { Guest = floating });
        Microsoft.Xna.Framework.Color[] after = GpuTestDevice.Read(gpu.Target);
        Assert.True(before.Zip(after).Count(pixel => pixel.First != pixel.Second) > 32, "The original textured guest body and weapon must visibly hover in the actual arena.");
    }

    private static void Near(Vector3 expected, Vector3 actual)
    {
        Assert.True(Vector3.Distance(expected, actual) < 0.001f, $"Expected{expected}, actual{actual}.");
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<TriangleVertex> Vertices { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
        }

        public void End()
        {
        }
    }
}
