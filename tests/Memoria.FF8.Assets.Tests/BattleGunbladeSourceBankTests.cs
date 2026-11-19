using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleGunbladeSourceBankTests
{
    [Theory]
    [InlineData(0, 6, BattleLimitFamily.Renzokuken)]
    [InlineData(6, 0, BattleLimitFamily.FireCross)]
    public void Source_weapon_bank_mods_are_compiled_before_transfer_and_invalidate_only_the_changed_revision(int sourceCharacter, int style, BattleLimitFamily family)
    {
        SourceBankOverrides mods = new(sourceCharacter);
        using BattleAssets assets = new(TestResources.GameDirectory, modelOverrides: mods);
        int weapon = assets.WeaponDefinitions.First(item => item.CharacterId == style).Id;
        BattleCombatantDefinition actor = new() { Id = "recipient", IsPlayer = true, ModelId = sourceCharacter, WeaponId = weapon, WeaponProfile = assets.ReadWeaponProfile(weapon) };
        NativeBattleGraphicsResource equipment = assets.ReadNativeGraphicsResource(actor);
        NativeBattleGraphicsResource first = assets.ReadNativeLimitPresentationResource(actor, family, equipment);
        NativeBattleAnimationBankOverride firstBank = first.AnimationBankOverrides.First(item => item.Linked);
        BattleModel original = mods.Originals[firstBank.Bank];
        Assert.Equal(Convert.ToHexString(SHA256.HashData(original.NativePoseSource!.Animations.AsSpan())), firstBank.SourceFingerprint);
        int sourceWeaponId = assets.WeaponDefinitions.First(item => item.CharacterId == sourceCharacter).Id;
        BattleModel source = assets.ResolvePlayerRenderWeapon(sourceCharacter, sourceWeaponId, assets.LoadPlayer(sourceCharacter))!;
        BattleModel recipient = assets.ResolvePlayerRenderWeapon(actor.ModelId, weapon, assets.LoadPlayer(actor.ModelId))!;
        NativeBattlePoseRetargeter compiler = BattleLinkedWeaponRigProfiles.CreateGunbladeCompiler(source, recipient);
        NativeBattlePoseSource expected = compiler.Compile(SourceBankOverrides.Edit(original.NativePoseSource, mods.Revision));
        Assert.Equal(expected.Animations.ToArray(), firstBank.Animations.ToArray());
        NativeBattleGraphicsResource repeated = assets.ReadNativeLimitPresentationResource(actor, family, equipment);
        Assert.Same(firstBank, repeated.AnimationBankOverrides.Single(item => item.Linked && item.Bank == firstBank.Bank));
        mods.Revision++;
        NativeBattleGraphicsResource second = assets.ReadNativeLimitPresentationResource(actor, family, equipment);
        NativeBattleAnimationBankOverride secondBank = second.AnimationBankOverrides.Single(item => item.Linked && item.Bank == firstBank.Bank);
        Assert.Equal(firstBank.SourceFingerprint, secondBank.SourceFingerprint);
        Assert.False(firstBank.Animations.AsSpan().SequenceEqual(secondBank.Animations.AsSpan()));
        expected = compiler.Compile(SourceBankOverrides.Edit(original.NativePoseSource, mods.Revision));
        Assert.Equal(expected.Animations.ToArray(), secondBank.Animations.ToArray());
        Assert.Equal(equipment.LinkedGeometry, second.LinkedGeometry);
    }

    [Fact]
    public void A_changed_source_bank_skeleton_is_rejected_even_when_its_animation_revision_was_cached()
    {
        SourceBankOverrides mods = new(0);
        using BattleAssets assets = new(TestResources.GameDirectory, modelOverrides: mods);
        int weapon = assets.WeaponDefinitions.First(item => item.CharacterId == 6).Id;
        BattleCombatantDefinition actor = new() { Id = "recipient", IsPlayer = true, ModelId = 0, WeaponId = weapon, WeaponProfile = assets.ReadWeaponProfile(weapon) };
        NativeBattleGraphicsResource equipment = assets.ReadNativeGraphicsResource(actor);
        _ = assets.ReadNativeLimitPresentationResource(actor, BattleLimitFamily.Renzokuken, equipment);
        Assert.NotEmpty(mods.Originals);
        mods.ChangeSkeleton = true;
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => assets.ReadNativeLimitPresentationResource(actor, BattleLimitFamily.Renzokuken, equipment));
        Assert.Contains("calibrated weapon skeleton", exception.Message);
    }

    [Fact]
    public void Body_bank_cache_recalibrates_geometry_changes_with_an_unchanged_native_pose_source()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleModel source = assets.LoadPlayer(0);
        BattleModel recipient = assets.LoadPlayer(6);
        HashSet<int> feet = BattleHumanoidRigProfiles.SemanticPairs(0, source, 6, recipient).Where(pair => pair.Joint is "foot-a" or "foot-b").Select(pair => pair.Recipient).ToHashSet();
        BattleMesh[] meshes = recipient.Meshes.Select(mesh => mesh with
        {
            Vertices = mesh.Vertices.Select(vertex => feet.Contains(vertex.Bone)
                ? vertex with { Position = vertex.Position - new System.Numerics.Vector3(0, 0.5f, 0), NativePosition = vertex.NativePosition!.Value with { Y = checked((short)(vertex.NativePosition.Value.Y + 1024)) } }
                : vertex).ToArray()
        }).ToArray();
        BattleModel changed = recipient with { Meshes = meshes, NativeControlMeshes = meshes, NativeRenderGeometry = NativeBattleGeometryMetadata.FromMeshes(meshes), NativeGeometryBytes = null };
        Assert.Same(recipient.NativePoseSource, changed.NativePoseSource);
        NativeBattlePoseRetargeter originalCompiler = BattleHumanoidRigProfiles.CreateRetargeter(0, source, 6, recipient);
        NativeBattlePoseRetargeter changedCompiler = BattleHumanoidRigProfiles.CreateRetargeter(0, source, 6, changed);
        NativeBattlePoseSource motion = source.NativePoseSource!;
        string revision = Convert.ToHexString(SHA256.HashData(motion.Animations.AsSpan()));
        MethodInfo compile = typeof(BattleAssets).GetMethod("CompileLoadoutAnimationBank", BindingFlags.Instance | BindingFlags.NonPublic)!;
        NativeBattleAnimationBankOverride first = (NativeBattleAnimationBankOverride)compile.Invoke(assets, [0, source, recipient, originalCompiler, motion, "geometry-calibration", null, revision, null, null])!;
        NativeBattleAnimationBankOverride second = (NativeBattleAnimationBankOverride)compile.Invoke(assets, [0, source, changed, changedCompiler, motion, "geometry-calibration", null, revision, null, null])!;
        NativeBattlePoseSource expected = changedCompiler.Compile(motion);
        Assert.False(first.Animations.AsSpan().SequenceEqual(expected.Animations.AsSpan()));
        Assert.Equal(expected.Animations.ToArray(), second.Animations.ToArray());
    }

    private sealed class SourceBankOverrides(int character) : IModelOverrideSource
    {
        public int Revision { get; set; } = 1;
        public bool ChangeSkeleton { get; set; }
        public Dictionary<string, BattleModel> Originals { get; } = [];

        public FieldModel Apply(string modelId, FieldModel original) => original;
        public BattleModel Apply(string modelId, BattleModel original) => original;
        public BattleStage Apply(string modelId, BattleStage original) => original;
        public WorldStaticModel Apply(string modelId, WorldStaticModel original) => original;

        public BattleModel ApplyBattleBank(string modelId, string bank, BattleModel original)
        {
            if (!modelId.StartsWith($"battle/d{character:X}w", StringComparison.OrdinalIgnoreCase))
            {
                return original;
            }
            Originals[bank] = original;
            NativeBattlePoseSource changed = Edit(original.NativePoseSource!, Revision);
            if (ChangeSkeleton)
            {
                byte[] skeleton = changed.Skeleton.ToArray();
                skeleton[4] ^= 1;
                changed = changed with { Skeleton = skeleton.ToImmutableArray() };
            }
            return original with { NativePoseSource = changed };
        }

        public static NativeBattlePoseSource Edit(NativeBattlePoseSource original, int revision)
        {
            var frames = NativeBattlePoseDecoder.DecodeFrames(original.Animations.AsSpan(), original.BoneCount);
            var changed = frames.Select(animation => animation.Select(pose =>
            {
                var bones = pose.Bones.ToBuilder();
                NativeBattleVector angles = bones[0].Rotation;
                bones[0] = bones[0] with { Rotation = angles with { Y = (short)((angles.Y + revision * 16) & 4095) } };
                return pose with { Bones = bones.ToImmutable() };
            }).ToImmutableArray()).ToArray();
            return original with { Animations = NativeBattleAnimationEncoder.Encode(changed) };
        }
    }
}
