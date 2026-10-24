using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeRenzokuken334337ResourcesTests
{
    [Theory]
    [InlineData(334, 6, false)]
    [InlineData(335, 4, true)]
    [InlineData(336, 6, true)]
    [InlineData(337, 6, false)]
    public void OriginalResourcesRetainTheirDistinctSpritesMeshesAndOffsetOwners(int effect, int meshes, bool staticOffsets)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRenzokuken160Resources resources = game.ReadNativeRenzokuken334337Resources((ushort)effect);
        Assert.Equal(effect, resources.ActorResource.MagicId);
        Assert.Single(resources.ActorResource.TargetResources);
        Assert.Equal(meshes, resources.Meshes.Length);
        Assert.All(resources.Meshes, mesh => Assert.False(mesh.Bytes.IsDefaultOrEmpty));
        Assert.Equal(2, resources.SpecialSprites.Length);
        Assert.All(resources.SpecialSprites, sprite => Assert.False(sprite.Bytes.IsDefaultOrEmpty));
        Assert.False(resources.Animation.Data.IsDefaultOrEmpty);
        Assert.Equal(24, resources.SpiralColors.Length);
        Assert.Equal(resources.ActorResource.Timeline.InputThresholds.Length, resources.ContactLeadFrames.Length);
        Assert.Equal(staticOffsets, resources.ContactOffsets is not null);
        Assert.Equal(8, resources.StreakAgeOffsets.Length);
        Assert.Equal(8, resources.StreakPaletteOffsets.Length);
    }
}
