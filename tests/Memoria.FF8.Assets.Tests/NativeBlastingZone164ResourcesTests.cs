using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBlastingZone164ResourcesTests
{
    [Fact]
    public void OriginalBlastingZoneResourcesRetainActualStaticAndStreamedInputs()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleBlastingZone164Resources resources = game.ReadNativeBlastingZone164Resources();
        resources.Validate();
        Assert.Equal(3u, resources.ReadDword(0xD88AA0));
        Assert.Equal(new short[] { 538, 539 }, resources.Files.Select(file => file.FileId));
        Assert.Equal(new[] { "MAG163_A.DAT", "MAG163_B.DAT" }, resources.Files.Select(file => file.Name));
        Assert.Equal(19, resources.Meshes.Length);
        Assert.Equal(3, resources.Sprites.Length);
        Assert.All(resources.ColdGlobals, region => Assert.All(region.Data, value => Assert.Equal(0, value)));
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        foreach (NativeBattleEffectFileAsset file in resources.Files)
        {
            (textures, _) = textures.EnqueueTim505E30(new(file.Name, file.Data));
        }
        textures.Validate();
    }
}
