using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Remastered.Tests;

public sealed class BattleResourceRoutingTests
{
    [Fact]
    public void OwnedBattleSourceDetectsRemasteredAndRegistersItsModelTextures()
    {
        using BattleAssets assets = new(InstalledDirectory());
        BattleModel model = assets.LoadModel("d0c000.dat");
        Assert.NotEmpty(model.Meshes);
        ResourceTextureList textures = Assert.IsType<ResourceTextureList>(model.Textures);
        Assert.StartsWith("remastered:", textures.GetTexture(0).Provenance);
        Assert.Equal(384, textures[0].Width);
        Assert.Equal(384, textures[0].Height);
        Assert.Throws<NotImplementedException>(() => assets.ReadNativePrimitiveTables());
    }

    [Fact]
    public void BorrowedSourceSurvivesBattleDisposalAndNativeReadersRejectUnsupportedRuntime()
    {
        string directory = InstalledDirectory();
        using RemasteredGameDataSource source = new(directory);
        ResourceService resources = new(source);
        using (BattleAssets assets = new(directory, resources))
        {
            Assert.NotEmpty(assets.LoadModel("d0c000.dat").Meshes);
            Assert.True(resources.ResidentTextureCount > 0);
            Assert.Throws<NotImplementedException>(() => assets.ReadNativeBattleFile(0));
        }
        Assert.Equal(0, resources.ResidentTextureCount);
        Assert.NotEmpty(resources.Read(new ResourceKey("main/kernel.bin")));
        using GameAssets game = new(directory, resources: resources);
        Assert.NotNull(game.ReadNativeCommonEffectTexture());
        Assert.Throws<NotImplementedException>(() => game.ReadNativeBlizzardResources());
    }

    private static string InstalledDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        Assert.SkipUnless(File.Exists(Path.Combine(directory!, "main.zzz")) && File.Exists(Path.Combine(directory!, "other.zzz")), "The selected installation does not contain both required ZZZ archives.");
        return directory!;
    }
}
