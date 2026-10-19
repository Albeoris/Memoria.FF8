using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeFatedCircle162ResourcesTests
{
    [Fact]
    public void OriginalFatedCircleResourcesDecodeTheirActualDirectoriesAndTextureIngress()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleFatedCircle162Resources resources = game.ReadNativeFatedCircle162Resources();
        resources.Validate();
        Assert.Equal(537, resources.TextureFile.FileId);
        Assert.Equal("MAG161_A.DAT", resources.TextureFile.Name);
        Assert.False(resources.TextureFile.Data.IsDefaultOrEmpty);
        Assert.Equal(3u, resources.ReadDword(0xDA0100));
        Assert.Equal(5, resources.Sprites.Length);
        Assert.Equal(16, resources.Meshes.Length);
        Assert.DoesNotContain(resources.Meshes, mesh => mesh.Address == 0xDA8728);
        Assert.Equal(0xFFFD063Cu, resources.ReadDword(0xDA8728));
        Assert.All(resources.ColdGlobals, region => Assert.All(region.Data, value => Assert.Equal(0, value)));
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(resources.StaticData, 0x1688);
        (textures, _) = textures.EnqueueTim505E30(new(resources.TextureFile.Name, resources.TextureFile.Data));
        textures.Validate();
    }
}
