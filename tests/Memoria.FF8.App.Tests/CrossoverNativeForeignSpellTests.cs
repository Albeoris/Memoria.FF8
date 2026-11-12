using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverNativeForeignSpellTests
{
    [Theory]
    [InlineData("Blizzaga", 4, true)]
    [InlineData("Demi", 4, true)]
    [InlineData("Cure", 1, true)]
    [InlineData("Cura", 1, true)]
    [InlineData("Curaga", 1, true)]
    [InlineData("Protect", 1, true)]
    [InlineData("Shell", 1, true)]
    [InlineData("Sleep", 4, true)]
    [InlineData("Silence", 4, true)]
    [InlineData("Haste", 1, true)]
    [InlineData("Slow", 4, true)]
    [InlineData("Life", 1, true)]
    [InlineData("Full-life", 1, true)]
    [InlineData("Full-cure", 1, true)]
    [InlineData("Quake", 4, false)]
    public void OriginalSpellExecutesAgainstActualPosedHostGeometryAndItsAvailableStageContract(string spell, int target, bool nativeTriangles)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? hostRoot = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(hostRoot), "Select both original game installations.");
        using GameAssets game = new(root!);
        using BattleAssets battle = new(root!);
        BattleNativeAction action = Assert.Single(battle.NativeActions, action => action.Command == 2 && action.Name == spell);
        Ff7Installation installation = Ff7Installation.Discover(hostRoot!);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7BattleSession host = new(formation, original.Progress with { Party = [0, 1, 255] }, KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path)), executable.NativeMateriaStatModifiers, 1, requestFlags: 4, animations: animations);
        Ff7BattleRenderer renderer = new(archive, formation.LocationId);
        Ff7BattleActorState selected = host.Actors.Single(actor => actor.Actor.Slot == target);
        Ff7BattlePlacement hostPlacement = host.RenderActorPlacement(target);
        (int clip, int frame) = host.RenderActorPose(target);
        Ff7BattleActorVisual visual = new(Ff7BattleAnimationResources.ModelPrefix(selected.Identity, target >= 4), hostPlacement.Position, hostPlacement.Yaw * MathF.Tau / 4096, clip, frame, target < 3 ? host.WeaponModel(target) : selected.Identity == 16 ? 0 : null, host.RenderActorLimit(target)) { ModelModifiers = host.ActorModelOutput(target) };
        Ff7BattleActorGeometry geometry = renderer.CaptureActorGeometry(visual);
        Assert.NotEmpty(geometry.Faces);
        Assert.All(geometry.Faces, face => Assert.NotNull(face.Surface));
        Assert.NotEmpty(geometry.SkeletonPoints);
        Assert.NotEmpty(geometry.Bones);
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(host.RenderActorPlacement(0).Position), host.RenderActorPlacement(0).Yaw);
        CrossoverNativeForeignTarget foreign = CrossoverNativeForeignTarget.Read(host, target, geometry, placement.Matrix);
        NativeBattleForeignActor actorGeometry = foreign.ToActor(3);
        Assert.Equal(geometry.Bones.Length, actorGeometry.BoneCount);
        NativeBattleVector firstBone = actorGeometry.ReadBone(0, 0);
        Assert.InRange(Vector3.Distance(new(firstBone.X, firstBone.Y, firstBone.Z), Vector3.Transform(geometry.Bones[0].Start, foreign.HostToOrigin)), 0, 1);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(game.ReadNewGameCharacterVitals(), battle, 0);
        NativeBattleGraphicsResource resource = battle.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = kit.Weapon });
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(new(0, 0) { Transform = new(new(0, 0, 2200), new(0, 0, 2200), 0, 0, 4096), LinkedTransform = new(default, default), SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() } }, 0, 0, ref random);
        model = NativeBattleSequenceMachine.Advance(NativeBattleGraphicsModels.SelectSequence(model, 1), resource, new(0, 0)).Model;
        model = model with { Flags2 = (ushort)(model.Flags2!.Value & ~4), GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, resource.PoseSource!) };
        CrossoverNativeMagicPresentation presentation = new(game, resource, battle);
        CrossoverNativeMagicPresentationState state = presentation.Begin(model, target, NativeCrtRandomState.FromColdProcess(), new(3, 4, 0, 0, 0, 83, 0), action, random);
        NativeBattleCameraProjection camera = CrossoverNativeSpriteRenderer.Camera(host.RenderCameraOutput, placement.Matrix);
        CrossoverNativeSpriteRenderer compositor = new(battle.ReadNativePrimitiveTables(), game.ReadNativeEffectMeshes(), game.ReadNativeBattleGraphicsRules().SummonColors!);
        CountRenderer triangles = new();
        int contacts = 0;
        HashSet<uint> sounds = [];
        for (int callback = 0; callback < 4096 && !state.Complete; callback++)
        {
            CrossoverNativeMagicPresentationStep step = presentation.Advance(state, () => foreign.ToActor(3).Anchor, () => foreign.Transform, camera, () => foreign);
            state = step.State;
            contacts += step.ContactTargets.Length;
            foreach (NativeBattleModuleSound sound in step.EffectSounds)
            {
                sounds.Add(sound.Sound);
            }
            Assert.Equal((short)0, state.ViewTranslationY);
            if ((callback & 7) == 0 && state.Ordering is { } ordering && state.Memory is { } memory)
            {
                compositor.DrawOrdering(triangles, ordering, state.Textures, 320, 240, CrossoverGunbladeHostPlacement.OriginToHostScale, (reference, count) => reference.Bank == "20DFAB8" ? memory.Arena!.Read(reference.ByteOffset, count) : (memory.PrimitiveProcess! with { PacketBank = reference.Bank, PacketBuffer = reference.Buffer }).ReadCommonPacket(reference.ByteOffset, count));
            }
        }
        Assert.True(state.Complete, $"{spell} did not complete its original presentation.");
        Assert.Equal(1, contacts);
        Assert.Equal(1, state.PublishedContacts);
        if (nativeTriangles)
        {
            Assert.True(triangles.Count > 0, $"{spell} did not render its native effect packets.");
        }
        else
        {
            NativeBattleQuake38Resources resources = Assert.Single(game.ReadNativeEffectCatalogForEffects([action.MagicId]), binding => binding.Effect == action.MagicId).Quake38Resources!;
            Assert.NotNull(resources);
            Assert.Contains(resources.RumbleSound, sounds);
            Assert.Contains(resources.QuakeSound, sounds);
            Assert.Equal(0, triangles.Count);
        }
    }

    private sealed class CountRenderer : ITriangleRenderer
    {
        public int Count { get; private set; }

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Count++;
        }

        public void End()
        {
        }
    }
}
