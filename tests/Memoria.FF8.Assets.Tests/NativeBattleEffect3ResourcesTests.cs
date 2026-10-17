using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect3ResourcesTests
{
    [Fact]
    public void ExplicitEffect3SelectionReadsItsActualEmbeddedTextureAndEightSpriteBanks()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        Assert.Null(assets.ReadNativeEffectCatalog()[2].Effect3Resources);
        var catalog = assets.ReadNativeEffectCatalogForEffects([3]);
        NativeBattleEffectBinding binding = catalog[2];
        Assert.Equal(0x700C10u, binding.Entry);
        Assert.True(binding.InitializerIsRet);
        Assert.Null(binding.InitializerAsset);
        NativeBattleEffect3Resources resources = binding.Effect3Resources!;
        Assert.Equal(165920, resources.Texture.Data.Length);
        Assert.Equal(0x590, resources.Animation.Data.Length);
        Assert.Equal(0x493E6u, resources.Sound);
        Assert.Equal(0x808080u, resources.FlashColor);
        Assert.Equal(8, resources.Sprites.Length);
        Assert.Equal(new[] { 7, 7, 9, 9, 9, 9, 19, 13 }, resources.Sprites.Select(resource => resource.Bytes[8] | resource.Bytes[9] << 8));
        foreach (NativeBattleModuleResource resource in resources.Sprites)
        {
            resource.Validate();
        }
        var upload = NativeBattleEffectTextureState.FromColdProcess().EnqueueTim505E30(resources.Texture);
        Assert.Equal(resources.Texture.Data.Length, upload.NextOffset);
        Assert.Equal(2, upload.State.Drain505D20().Uploads.Length);
        Assert.Single(TimFile.Read(resources.Texture.Data.AsSpan()).Images);
        NativeBattleEffectTextureState registered = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(resources.Animation);
        Assert.Equal(8, registered.Animation!.StreamOffset);
        Assert.Equal(0x144, registered.Animation.ModelTableOffset);
        Assert.Null(assets.ReadNativeEffectCatalog()[2].Effect3Resources);
    }
}
