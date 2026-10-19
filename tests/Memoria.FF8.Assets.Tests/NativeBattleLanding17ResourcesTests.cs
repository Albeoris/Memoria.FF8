using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleLanding17ResourcesTests
{
    [Fact]
    public void Ret_initializer_keeps_its_entry_owned_resources_in_the_materialized_catalog()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([17])[16];
        Assert.True(binding.InitializerIsRet);
        NativeBattleLanding17Resources resources = Assert.IsType<NativeBattleLanding17Resources>(binding.Landing17Resources);
        Assert.Same(game.ReadNativeLanding17Resources(), resources);
        Assert.Equal(0x294, resources.Camera.Data.Length);
        Assert.Equal(6, resources.NodeFlags.Length);
        Assert.True(resources.Spark.SpriteFrame(12).NextOffset < 0);
        resources.Vibration.Validate();
        Assert.Equal(0, resources.Vibration.Offsets[0]);
        Assert.Equal(0x100, resources.Vibration.Offsets[1]);
    }
}
