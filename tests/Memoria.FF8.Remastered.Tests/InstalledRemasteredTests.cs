using System.Buffers.Binary;
using System.Text;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Remastered.Tests;

public sealed class InstalledRemasteredTests
{
    [Fact]
    public void MountsSharedAndLocalizedResourcesThroughTheCommonService()
    {
        using RemasteredGameDataSource source = new(InstalledDirectory());
        ResourceService resources = new(source);
        Assert.Equal(GameDataEdition.Remastered, resources.Source.Edition);
        Assert.Same(source, resources.GetSource<IGameArchiveSource>());
        Assert.NotEmpty(resources.Read(new ResourceKey("main/kernel.bin")));
        byte[] maplist = resources.Read(new ResourceKey("field/mapdata/maplist"));
        Assert.Contains('\n', Encoding.ASCII.GetString(maplist));
        Assert.NotEmpty(resources.Read(new ResourceKey("field/main_chr/d000.mch")));
        Assert.NotEmpty(resources.Read(new ResourceKey("magic/mag085_b.dat")));
        using var main = source.OpenArchive("main");
        Assert.Contains(main.Entries, entry => entry.RelativePath.EndsWith("kernel.bin", StringComparison.OrdinalIgnoreCase));
        using var field = source.OpenArchive("field");
        Assert.NotEmpty(field.Entries);
    }

    [Fact]
    public void DecodesReferencedFieldGeometryThroughTheReleaseSpecificModelSource()
    {
        using RemasteredGameDataSource source = new(InstalledDirectory());
        ResourceService resources = new(source);
        string map = FieldName(resources, 179);
        ResourceKey owner = new($"field/{map}/chara.one");
        IReadOnlyList<FieldModel> models = source.ReadModels(owner, resources.Read(owner), name => resources.Read(new ResourceKey($"field/main_chr/{name}.mch")));
        FieldModel model = Assert.Single(models, model => model.Name.StartsWith("d000", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(model.Bones);
        Assert.Equal(1250, model.Vertices.Count);
        Assert.Equal(2371, model.Faces.Count);
        Assert.NotEmpty(model.Faces);
        Assert.NotEmpty(model.Textures);
        Assert.NotEmpty(model.Animations);
        Assert.Equal(model.Vertices.Count, model.Pose(0, 0).Length);
    }

    [Theory]
    [InlineData("d000", 104d)]
    [InlineData("d002", 75d)]
    public void FieldModelCoordinatesUseTheirNativePrecisionWithoutScalingTheSkeleton(string name, double divisor)
    {
        using RemasteredGameDataSource source = new(InstalledDirectory());
        ResourceService resources = new(source);
        string map = FieldName(resources, 179);
        ResourceKey owner = new($"field/{map}/chara.one");
        IReadOnlyList<FieldModel> models = source.ReadModels(owner, resources.Read(owner), model => resources.Read(new ResourceKey($"field/main_chr/{model}.mch")));
        FieldModel model = Assert.Single(models, model => model.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
        byte[] original = resources.Read(new ResourceKey($"field/main_chr/{name}.mch"));
        int pointer = 0;
        while (BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(pointer)) != uint.MaxValue)
        {
            pointer += 4;
        }
        int header = BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(pointer + 4));
        int vertices = header + BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(header + 36));
        int bones = header + BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(header + 32));
        Assert.Equal((float)(BinaryPrimitives.ReadInt16LittleEndian(original.AsSpan(vertices)) / divisor), model.Vertices[0].Position.X);
        Assert.Equal((float)(BinaryPrimitives.ReadInt16LittleEndian(original.AsSpan(vertices + 2)) / divisor), model.Vertices[0].Position.Y);
        Assert.Equal((float)(BinaryPrimitives.ReadInt16LittleEndian(original.AsSpan(vertices + 4)) / divisor), model.Vertices[0].Position.Z);
        Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(original.AsSpan(bones + 64 + 8)), model.Bones[1].Length);
        Assert.Equal(-58, model.Bones[1].Length);
    }

    [Fact]
    public void FieldLoadKeepsTextureOwnerEncodedBytesAvailableThroughTheService()
    {
        string directory = InstalledDirectory();
        using RemasteredGameDataSource source = new(directory);
        ResourceService resources = new(source);
        using GameAssets assets = new(directory, resources: resources);
        FieldAssets field = assets.LoadField(179);
        ResourceKey owner = new($"field/{field.Name}/chara.one");
        Assert.NotEmpty(resources.Read(owner));
        Assert.NotEmpty(field.Models);
        Assert.Contains(field.Models, model => model.Textures is ResourceTextureList && model.Textures.Count != 0);
        Assert.True(resources.ResidentTextureCount > 0);
        FieldModel squall = Assert.Single(field.Models, model => model.Name.StartsWith("d000", StringComparison.OrdinalIgnoreCase));
        ResourceTextureList textures = Assert.IsType<ResourceTextureList>(squall.Textures);
        TextureValue texture = textures.GetTexture(0);
        Assert.StartsWith("remastered:", texture.Provenance);
        Assert.Equal(128, texture.LogicalWidth);
        Assert.Equal(128, texture.LogicalHeight);
        Assert.Equal(384, texture.Image.Width);
        Assert.Equal(384, texture.Image.Height);
        Assert.NotNull(texture.Variants);
        Assert.Equal(384, texture.Variants["eyes-closed"].Width);
        resources.Prepare(null).Commit();
        Assert.Equal(texture.Image.Pixels, textures.GetTexture(0).Image.Pixels);
        Assert.Equal(Memoria.FF8.Runtime.NativeFieldRuntimeProfile.RemasteredEn, assets.NativeRuntimeProfile);
    }

    [Fact]
    public void ShortFieldImageRetainsNativeThreefoldUvScaleAndEyePlanes()
    {
        using RemasteredGameDataSource source = new(InstalledDirectory());
        ResourceService resources = new(source);
        ResourceKey owner = new("field/bghoke_2/chara.one");
        IReadOnlyList<FieldModel> models = source.ReadModels(owner, resources.Read(owner), name => resources.Read(new ResourceKey($"field/main_chr/{name}.mch")));
        int index = Enumerable.Range(0, models.Count).Single(index => models[index].Name.StartsWith("d019", StringComparison.OrdinalIgnoreCase));
        FieldModel model = models[index];
        Assert.Equal(128, model.Textures[1].Height);
        float maxV = model.Faces.Where(face => face.Texture == 1).SelectMany(face => face.TextureCoordinates).Max(uv => uv.Y);
        Assert.Equal(127, maxV);
        TextureBuildContext context = new(new TextureKey(owner, $"model/{index}/texture/1"), source.Descriptor, 0, "classic-model", 128, 128, () => model.Textures[1]);
        TextureReplacement texture = Assert.IsType<TextureReplacement>(source.Resolve(context));
        Assert.Equal(128, texture.LogicalWidth);
        Assert.Equal(128, texture.LogicalHeight);
        Assert.Equal(384, texture.Image.Width);
        Assert.Equal(384, texture.Image.Height);
        Assert.Equal(384, texture.Variants!["eyes-closed"].Width);
        Assert.Equal(384, texture.Variants["eyes-closed"].Height);
        using ZzzArchive main = new(Path.Combine(InstalledDirectory(), "main.zzz"));
        using Stream image = main.Open("textures/field.fs/field_hd_new/d019_0.png");
        using MemoryStream bytes = new();
        image.CopyTo(bytes);
        DecodedTextureImage encoded = TextureImageDecoder.Decode(bytes.ToArray(), "d019_0.png");
        for (int row = 0; row < 288; row++)
        {
            Assert.Equal(encoded.Image.Pixels.AsSpan(row * 768, 384).ToArray(), texture.Image.Pixels.AsSpan(row * 384, 384).ToArray());
            Assert.Equal(encoded.Image.Pixels.AsSpan(row * 768 + 384, 384).ToArray(), texture.Variants["eyes-closed"].Pixels.AsSpan(row * 384, 384).ToArray());
        }
        Assert.All(texture.Image.Pixels.Skip(384 * 288), pixel => Assert.Equal(default, pixel));
        Assert.All(texture.Variants["eyes-closed"].Pixels.Skip(384 * 288), pixel => Assert.Equal(default, pixel));
    }

    [Theory]
    [InlineData(229, "bgroad_6", true)]
    [InlineData(232, "bgroom_1", false)]
    public void OpeningBackgroundsResolveLocalizedOrSharedMapAndImagePairs(int fieldId, string name, bool localized)
    {
        using RemasteredGameDataSource source = new(InstalledDirectory());
        ResourceService resources = new(source);
        Assert.Equal(name, FieldName(resources, fieldId));
        foreach (string extension in new[] { "map", "mim" })
        {
            ResourceKey logical = new($"field/{name}/{name}.{extension}");
            ResourceKey selected = new($"field/{name}/{name}{(localized ? "_en" : "")}.{extension}");
            Assert.NotEmpty(resources.Read(logical));
            Assert.Equal(resources.Read(selected), resources.Read(logical));
        }
        using GameAssets assets = new(InstalledDirectory(), resources: resources);
        Assert.Equal(fieldId, assets.LoadField(fieldId).Id);
    }

    [Fact]
    public void GuestImageExposesInitialSerializedBytesAndRejectsUnmappedReads()
    {
        string path = Path.Combine(InstalledDirectory(), "FFVIII_EN.exe.dat");
        Assert.SkipUnless(File.Exists(path), "The selected installation does not contain the English serialized guest image.");
        RemasteredGuestImage image = new(path);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(image.Read(0xB8EEA0, 4)));
        Assert.Throws<InvalidDataException>(() => image.Read(0, 4));
        Assert.Throws<InvalidDataException>(() => image.Read(uint.MaxValue, 4));
    }

    [Fact]
    public void LoadsBattleModelAndStaticHighResolutionRowsWithoutReleaseKnowledgeInConsumers()
    {
        string directory = InstalledDirectory();
        using RemasteredGameDataSource source = new(directory);
        ResourceService resources = new(source);
        using BattleAssets assets = new(directory, resources);
        BattleModel model = assets.LoadModel("d0c000.dat");
        Assert.NotEmpty(model.Meshes);
        ResourceTextureList textures = Assert.IsType<ResourceTextureList>(model.Textures);
        Assert.Equal(2, textures.Count);
        Assert.Equal(384, textures[0].Width);
        Assert.Equal(384, textures[0].Height);
        Assert.Equal(384, textures[1].Width);
        Assert.Equal(288, textures[1].Height);
        Assert.Equal(128, textures.GetTexture(1).LogicalWidth);
        Assert.Equal(96, textures.GetTexture(1).LogicalHeight);
        Assert.NotNull(textures.GetTexture(1).Variants);
        Assert.Equal(288, textures.GetTexture(1).Variants!["eyes-closed"].Height);
        Assert.NotEmpty(model.Pose(0, 0));
    }

    [Fact]
    public void ResolvesAuthenticatedNamedTablesAndExtractedExecutableEffects()
    {
        using RemasteredGameDataSource source = new(InstalledDirectory());
        ResourceService resources = new(source);
        Assert.Equal(256, resources.Read(new ResourceKey("tables/field-random")).Length);
        Assert.Equal(256, resources.Read(new ResourceKey("tables/world-random")).Length);
        Assert.Equal(8, resources.Read(new ResourceKey("tables/encounter-weights")).Length);
        Assert.Equal(512, resources.Read(new ResourceKey("tables/cosine")).Length);
        Assert.Equal(412, resources.Read(new ResourceKey("tables/vibration")).Length);
        Assert.Equal(320, resources.Read(new ResourceKey("text/draw-points")).Length);
        Assert.Equal(1200, resources.Read(new ResourceKey("text/card-names")).Length);
        Assert.Equal(1416, resources.Read(new ResourceKey("text/card-names-alternate")).Length);
        Assert.Equal(360, resources.Read(new ResourceKey("text/card-descriptions")).Length);
        Assert.Equal(15272, resources.Read(new ResourceKey("text/enemy-scan")).Length);
        Assert.Equal(35576, resources.Read(new ResourceKey("magic/mag085_b.dat")).Length);
    }

    [Fact]
    public void EngineAssetsUseFactoryProvidersAndFailExplicitlyAtUnsupportedRuntimeContracts()
    {
        string directory = InstalledDirectory();
        using GameAssets assets = new(directory);
        Assert.NotEmpty(assets.ReadResource(new ResourceKey("main/kernel.bin")));
        Assert.NotEmpty(assets.LoadField(179).Models);
        Assert.Equal(assets.ReadResource(new ResourceKey("tables/field-random"))[0], assets.ReadNativeShakeTables().Random(0));
        assets.ReadNativeVibrationBank().Validate();
        Assert.Throws<NotImplementedException>(() => assets.ReadNativeExecutable());
        Assert.Throws<NotImplementedException>(() => assets.ReadNativeEncounterTables());
        Assert.NotNull(assets.ReadNewGameCharacterVitals());
    }

    [Theory]
    [InlineData("runtime/native-executable")]
    [InlineData("runtime/unknown")]
    public void UnsupportedEditionResourcesHaveExplicitBoundaries(string key)
    {
        using RemasteredGameDataSource source = new(InstalledDirectory());
        Assert.Throws<NotImplementedException>(() => new ResourceService(source).Read(new ResourceKey(key)));
    }

    private static string FieldName(ResourceService resources, int fieldId)
    {
        string[] maps = Encoding.ASCII.GetString(resources.Read(new ResourceKey("field/mapdata/maplist"))).Replace("\r", "").Split('\n');
        Assert.InRange(fieldId, 0, maps.Length - 1);
        Assert.False(string.IsNullOrWhiteSpace(maps[fieldId]));
        return maps[fieldId];
    }

    private static string InstalledDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        Assert.SkipUnless(File.Exists(Path.Combine(directory!, "main.zzz")) && File.Exists(Path.Combine(directory!, "other.zzz")), "The selected installation does not contain both required ZZZ archives.");
        return directory!;
    }
}
