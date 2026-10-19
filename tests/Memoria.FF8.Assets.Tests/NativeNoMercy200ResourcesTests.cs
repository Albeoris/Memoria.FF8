using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeNoMercy200ResourcesTests
{
    [Fact]
    public void OriginalNoMercyRetainsItsActualDataAndTextureSources()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleNoMercy200Resources resources = game.ReadNativeNoMercy200Resources();
        resources.Validate();
        Assert.Equal(3u, resources.Dword(0xE2B518));
        Assert.Equal(new short[] { 562, 563, 564 }, resources.Files.Select(file => file.FileId));
        Assert.Equal(0x493E0u, resources.Dword(0xE33D30));
        Assert.Equal(0x557C6u, resources.Dword(0xE33D34));
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        (textures, _) = textures.EnqueueTim505E30(resources.InitialTim);
        foreach (NativeBattleEffectFileAsset file in resources.Files)
        {
            (textures, _) = textures.EnqueueTim505E30(new(file.Name, file.Data));
        }
        textures.Validate();
    }
}
