using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeRenzokukenVariantResourcesTests
{
    [Theory]
    [InlineData(141, 5, 4)]
    [InlineData(259, 6, 4)]
    [InlineData(332, 7, 5)]
    [InlineData(333, 8, 5)]
    public void OriginalVariantResourcesDecodeTheirOwnActorSpritesMeshesAndContactBanks(int effect, int contacts, int meshes)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRenzokuken160Resources resources = effect switch
        {
            141 => game.ReadNativeRenzokuken141Resources(),
            259 => game.ReadNativeRenzokuken259Resources(),
            332 => game.ReadNativeRenzokuken332Resources(),
            333 => game.ReadNativeRenzokuken333Resources(),
            _ => throw new InvalidOperationException()
        };
        Assert.Equal(effect, resources.ActorResource.MagicId);
        Assert.Equal(contacts, resources.ActorResource.Timeline.InputThresholds.Length);
        Assert.Equal(contacts, resources.ContactLeadFrames.Length);
        Assert.False(resources.Animation.Data.IsDefaultOrEmpty);
        Assert.Equal(3, resources.ParticleSprites.Length);
        Assert.All(resources.ParticleSprites, sprite => Assert.False(sprite.Bytes.IsDefaultOrEmpty));
        Assert.Equal(meshes, resources.Meshes.Length);
        Assert.All(resources.Meshes, mesh => Assert.False(mesh.Bytes.IsDefaultOrEmpty));
        Assert.Equal(meshes, resources.Meshes.Select(mesh => mesh.Address).Distinct().Count());
        Assert.Equal(24, resources.SpiralColors.Length);
        Assert.Equal(8, resources.StreakAgeOffsets.Length);
        Assert.Equal(8, resources.StreakPaletteOffsets.Length);
        if (effect is 332 or 333)
        {
            Assert.Equal(contacts, resources.ContactOffsets!.Value.Length);
        }
        else
        {
            Assert.Null(resources.ContactOffsets);
        }
    }
}
