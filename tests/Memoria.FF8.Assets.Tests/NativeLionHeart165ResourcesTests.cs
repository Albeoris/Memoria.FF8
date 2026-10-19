using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeLionHeart165ResourcesTests
{
    [Fact]
    public void OriginalLionHeartResourcesRetainActualStaticAndStreamedInputs()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleLionHeart165Resources resources = game.ReadNativeLionHeart165Resources();
        resources.Validate();
        Assert.Equal(3u, resources.ReadDword(0xD780C8));
        Assert.Equal(new short[] { 540, 541, 542 }, resources.Files.Select(file => file.FileId));
        Assert.Equal(new[] { "MAG164_A.DAT", "MAG164_B.DAT", "MAG164_C.DAT" }, resources.Files.Select(file => file.Name));
        Assert.Equal(16, resources.Meshes.Length);
        Assert.Equal(4, resources.Sprites.Length);
        Assert.All(resources.ColdGlobals, region => Assert.All(region.Data, value => Assert.Equal(0, value)));
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        foreach (NativeBattleEffectFileAsset file in resources.Files)
        {
            (textures, _) = textures.EnqueueTim505E30(new(file.Name, file.Data));
        }
        textures.Validate();
    }
}
