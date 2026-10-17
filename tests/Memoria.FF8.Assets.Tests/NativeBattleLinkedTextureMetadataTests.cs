using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleLinkedTextureMetadataTests
{
    [Theory]
    [InlineData(0, 0, 0, false)]
    [InlineData(1, 7, 2, true)]
    public void Selected_weapon_pack_has_its_own_original_slot_and_geometry_shape(int character, int weapon, int physicalIndex, bool sharedSkeleton)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(new() { Id = "player", IsPlayer = true, ModelId = character, WeaponId = weapon });
        Assert.Equal(sharedSkeleton, resource.LinkedGeometryUsesBodySkeleton);
        Assert.NotNull(resource.LinkedTexturePack);
        Assert.NotNull(resource.LinkedGeometry);
        Assert.NotEmpty(resource.LinkedGeometry!.Value);
        NativeBattleModelTextureCache cache = NativeBattleModelTextureCache.FromColdProcess().Reset508260();
        NativeBattleModelTextureCacheLoad body = cache.LoadPlayer((byte)character, physicalIndex, resource.BodyTexturePack!);
        NativeBattleModelTextureCacheLoad linked = body.Cache.LoadLinkedWeapon((byte)character, physicalIndex, resource.LinkedTexturePack!);
        Assert.Equal((ushort)(1 << (physicalIndex + 12)), linked.Upload!.AllocatedSlots);
        Assert.All(linked.Binding.PaletteRows.Where(slot => slot.HasValue), slot => Assert.Equal((byte)(physicalIndex + 12), slot));
        Assert.NotEqual(body.ResourceIndex, linked.ResourceIndex);
        if (sharedSkeleton)
        {
            Assert.Single(resource.LinkedGeometry.Value);
            Assert.True(resource.Geometry!.Value.Length >= 2);
        }
    }

    [Fact]
    public void Enemy_loader_publishes_known_linked_pointer_absence_from_502670()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattleGraphicsResource resource = assets.ReadEnemyNativeGraphicsResource(61);
        Assert.NotNull(resource.Geometry);
        Assert.NotEmpty(resource.Geometry!.Value);
        Assert.NotNull(resource.LinkedGeometry);
        Assert.Empty(resource.LinkedGeometry!.Value);
        Assert.False(resource.LinkedGeometryUsesBodySkeleton);
    }
}
