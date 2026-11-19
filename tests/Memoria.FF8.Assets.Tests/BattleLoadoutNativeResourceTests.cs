using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleLoadoutNativeResourceTests
{
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
