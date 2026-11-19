using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleLoadoutNativeResourceTests
{
    [Theory]
    [InlineData(0, 6, BattleLimitFamily.Renzokuken)]
    [InlineData(6, 0, BattleLimitFamily.FireCross)]
    public void Cross_gunblade_limits_keep_equipped_rig_and_source_weapon_clocks(int recipient, int style, BattleLimitFamily family)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        int weapon = assets.WeaponDefinitions.First(definition => definition.CharacterId == style).Id;
        BattleCombatantDefinition actor = new() { Id = "recipient", IsPlayer = true, ModelId = recipient, WeaponId = weapon, WeaponProfile = assets.ReadWeaponProfile(weapon) };
        NativeBattleGraphicsResource equipment = assets.ReadNativeGraphicsResource(actor);
        NativeBattleGraphicsResource action = assets.ReadNativeLimitPresentationResource(actor, family, equipment);
        int sourceCharacter = BattleLimitFamilyRules.SourceCharacter(family);
        int sourceWeapon = assets.WeaponDefinitions.First(definition => definition.CharacterId == sourceCharacter).Id;
        BattleModel source = assets.ResolvePlayerRenderWeapon(sourceCharacter, sourceWeapon, assets.LoadPlayer(sourceCharacter))!;
        Assert.Equal(equipment.LinkedPoseSource!.Skeleton, action.LinkedPoseSource!.Skeleton);
        Assert.Equal(NativeBattlePoseDecoder.ReadFrameCounts(source.NativePoseSource!.Animations.AsSpan()), action.LinkedAnimationFrames);
        Assert.NotEqual(source.NativePoseSource.Skeleton, action.LinkedPoseSource.Skeleton);
        Assert.Equal(equipment.LinkedGeometry, action.LinkedGeometry);
        Assert.Contains(action.AnimationBankOverrides, bank => bank.Linked);
        foreach (NativeBattleAnimationBankOverride bank in action.AnimationBankOverrides.Where(bank => bank.Linked))
        {
            NativeBattlePoseSource external = action.LinkedPoseSource with { Animations = bank.Animations };
            external.Validate();
            Assert.All(NativeBattlePoseDecoder.ReadFrameCounts(external.Animations.AsSpan()), count => Assert.True(count > 0));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(4)]
    [InlineData(10)]
    public void Every_installed_weapon_binds_to_the_receivers_actual_native_skeleton(int recipient)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleModel body = assets.LoadPlayer(recipient);
        foreach (BattleWeaponDefinition weapon in assets.WeaponDefinitions)
        {
            BattleCombatantDefinition actor = new() { Id = "recipient", IsPlayer = true, ModelId = recipient, WeaponId = weapon.Id, WeaponProfile = assets.ReadWeaponProfile(weapon.Id) };
            NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(actor);
            Assert.NotNull(resource.PoseSource);
            Assert.Equal(body.NativePoseSource!.Skeleton, resource.PoseSource.Skeleton);
            Assert.Equal(body.Bones.Count, resource.PoseSource.BoneCount);
            Assert.Equal((byte?)weapon.CharacterId, resource.CombatStyleModelId);
            Assert.NotNull(resource.Geometry);
            Assert.NotEmpty(resource.Geometry);
            Assert.Equal(resource.AnimationFrames, NativeBattlePoseDecoder.ReadFrameCounts(resource.PoseSource.Animations.AsSpan()));
            NativeBattleDecodedPose first = resource.PoseSource.Sample(0, 1, 0);
            Assert.Equal(body.Bones.Count, first.Bones.Length);
        }
    }
}
