using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeLinkedWeaponPoseTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    public void Installed_linked_resource_retains_its_own_animation_source_and_frame_counts(int character)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        int weaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == character).Id;
        BattleModel body = assets.LoadPlayer(character);
        BattleModel weapon = assets.ResolveWeaponModel(character, weaponId, body)!;
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(new() { Id = "player", IsPlayer = true, ModelId = character, WeaponId = weaponId });
        Assert.Equal(body.NativePoseSource, resource.PoseSource);
        Assert.Equal(weapon.NativePoseSource, resource.LinkedPoseSource);
        Assert.Equal(body.Animations.Select(animation => (byte)animation.Frames.Count), resource.AnimationFrames);
        Assert.Equal(weapon.Animations.Select(animation => (byte)animation.Frames.Count), resource.LinkedAnimationFrames);
        Assert.Equal(resource.LinkedAnimationFrames, NativeBattlePoseDecoder.ReadFrameCounts(resource.LinkedPoseSource!.Animations.AsSpan()));
    }
}
