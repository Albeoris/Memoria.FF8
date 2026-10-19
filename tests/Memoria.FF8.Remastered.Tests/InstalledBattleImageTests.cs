using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Remastered.Tests;

public sealed class InstalledBattleImageTests
{
    [Theory]
    [InlineData("c0m007", "c0m007", 2)]
    [InlineData("c0m034", "c0m034", 4)]
    [InlineData("c0m084", "c0m084", 5)]
    [InlineData("c0m121", "c0m121", 5)]
    [InlineData("c0m126", "c0m126", 6)]
    [InlineData("c0m055", "mag900", 1)]
    [InlineData("c0m056", "mag089", 2)]
    [InlineData("c0m061", "mag200", 2)]
    public void MonsterMaterialsUseTheNativeImageIdentityAndAllSourceSlots(string modelName, string imageName, int count)
    {
        string directory = InstalledDirectory();
        using RemasteredGameDataSource source = new(directory);
        ResourceService resources = new(source);
        using BattleAssets assets = new(directory, resources);
        BattleModel model = assets.LoadModel(modelName + ".dat");
        ResourceTextureList textures = Assert.IsType<ResourceTextureList>(model.Textures);
        Assert.Equal(count, textures.Count);
        for (int slot = 0; slot < count; slot++)
        {
            TextureValue texture = textures.GetTexture(slot);
            Assert.StartsWith("remastered:", texture.Provenance);
            Assert.Equal(128, texture.LogicalWidth);
            Assert.Equal(128, texture.LogicalHeight);
            Assert.Null(texture.Variants);
            Assert.Equal(384, texture.Image.Width);
            Assert.Equal(384, texture.Image.Height);
            string path = $"textures/battle.fs/hd_new/{imageName}_{slot}.png";
            using ZzzArchive archive = new(Path.Combine(directory, "main.zzz"));
            using Stream image = archive.Open(path);
            using MemoryStream bytes = new();
            image.CopyTo(bytes);
            DecodedTextureImage decoded = TextureImageDecoder.Decode(bytes.ToArray(), path);
            for (int row = 0; row < 384; row++)
            {
                Assert.Equal(decoded.Image.Pixels.AsSpan(row * decoded.Image.Width, 384).ToArray(), texture.Image.Pixels.AsSpan(row * 384, 384).ToArray());
            }
        }
    }

    [Theory]
    [InlineData(0, 0, "d0c000.dat")]
    [InlineData(5, 28, "d5c011.dat")]
    public void WeaponLoadingRoutesItsNativeMaterialThroughTheReplacementProvider(int characterId, int weaponId, string body)
    {
        string directory = InstalledDirectory();
        using BattleAssets assets = new(directory);
        BattleModel character = assets.LoadModel(body);
        BattleModel model = assets.LoadWeapon(characterId, weaponId, character);
        ResourceTextureList textures = Assert.IsType<ResourceTextureList>(model.Textures);
        TextureValue texture = textures.GetTexture(0);
        Assert.StartsWith("remastered:", texture.Provenance);
        Assert.Equal(128, texture.LogicalWidth);
        Assert.Equal(32, texture.LogicalHeight);
        Assert.Equal(384, texture.Image.Width);
        Assert.Equal(96, texture.Image.Height);
        Assert.Null(texture.Variants);
        Assert.Single(model.TextureRegions);
    }

    [Fact]
    public void FullAtlasRowsCropToShortCharacterMaterials()
    {
        string directory = InstalledDirectory();
        using BattleAssets assets = new(directory);
        BattleModel model = assets.LoadModel("dac021.dat");
        ResourceTextureList textures = Assert.IsType<ResourceTextureList>(model.Textures);
        Assert.StartsWith("remastered:", textures.GetTexture(1).Provenance);
        Assert.Equal(288, textures[1].Height);
    }

    [Fact]
    public void JapaneseMonsterMaterialUsesItsLocalizedImage()
    {
        string directory = InstalledDirectory();
        using RemasteredGameDataSource source = new(directory, "ja");
        ResourceService resources = new(source);
        ResourceKey owner = new("battle/c0m034.dat");
        BattleModel model = source.ReadModel(owner, resources.Read(owner));
        TextureBuildContext context = new(new TextureKey(owner, "texture/0"), source.Descriptor, 0, "classic-battle", 128, 128, () => model.Textures[0]);
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(source.Resolve(context));
        using ZzzArchive archive = new(Path.Combine(directory, "main.zzz"));
        const string path = "textures/battle.fs/hd_new/c0m034_0_jp.png";
        using Stream image = archive.Open(path);
        using MemoryStream bytes = new();
        image.CopyTo(bytes);
        Assert.Equal(TextureImageDecoder.Decode(bytes.ToArray(), path).Image.Pixels, replacement.Image.Pixels);
    }

    private static string InstalledDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        Assert.SkipUnless(File.Exists(Path.Combine(directory!, "main.zzz")) && File.Exists(Path.Combine(directory!, "other.zzz")), "The selected installation does not contain both required ZZZ archives.");
        return directory!;
    }
}
