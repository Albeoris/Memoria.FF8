using System.Text.Json;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class FfnxTextureTests
{
    [Fact]
    public void WideNativeSheetsKeepTheExistingTotalPixelBudget()
    {
        Assert.Equal(14336, new PixelImage(14336, 1).Width);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelImage(16384, 8192));
    }

    [Theory]
    [InlineData(5, 0, 5)]
    [InlineData(7, 0, 7)]
    [InlineData(5, 1, 17)]
    [InlineData(7, 1, 19)]
    [InlineData(4, 1, 4)]
    [InlineData(6, 1, 6)]
    [InlineData(5, 2, 5)]
    public void NativeTerrainVariantUsesOnlyTheOriginalReplacementBit(int page, ushort mask, int expected)
    {
        Assert.Equal(expected, WorldAssets.SelectTerrainTexturePage(page, mask));
    }

    [Fact]
    public void CardNativeCoordinatesCoverAllFrontsBackElementsAndBothBackgrounds()
    {
        ModFile[] files = [Sheet("cardgame/cards", 1792, 256), Sheet("cardgame/icons", 768, 192), Sheet("cardgame/intro", 384, 224), Sheet("cardgame/game", 384, 224), Sheet("data/eng/menu/mc05", 256, 256)];
        FfnxTextureResolver resolver = new(new(1, [], files));
        for (int card = 0; card < 110; card++)
        {
            TextureReplacement value = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("cards/cards", $"front/{card}", "native-artwork", 64, 64)));
            Assert.Equal(128, value.Image.Width);
            Assert.Equal(new Rgba32((byte)(card / 8 * 128 + card % 2 * 64), (byte)(card % 8 / 2 * 64), 7), value.Image[0, 0]);
        }
        Assert.Equal(new Rgba32(128, 192, 7), resolver.Resolve(Context("cards/cards", "back", "native-artwork", 64, 64))!.Image[0, 0]);
        for (int element = 0; element < 8; element++)
        {
            Assert.Equal(new Rgba32((byte)(element % 4 * 64), (byte)((element / 4 + 1) * 16), 7), resolver.Resolve(Context("cards/cards", $"element/{element}", "native-artwork", 16, 16))!.Image[0, 0]);
        }
        for (int capture = 0; capture < 3; capture++)
        {
            TextureReplacement value = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("cards/cards", $"capture/{capture}", "native-artwork", 255, 64)));
            Assert.Equal(510, value.Image.Width);
            Assert.Equal(128, value.Image.Height);
            Assert.Equal(new Rgba32(0, (byte)(capture * 64), 7), value.Image[0, 0]);
        }
        Assert.Equal(768, resolver.Resolve(Context("cards/cards", "background", "native-artwork", 384, 224))!.Image.Width);
        Assert.Equal(448, resolver.Resolve(Context("cards/cards", "board-background", "native-artwork", 384, 224))!.Image.Height);
        Assert.Equal(512, resolver.Resolve(Context("menu/mc05.tex", "atlas", "native-artwork", 256, 256))!.Image.Width);
    }

    [Fact]
    public void MainCharacterLibraryAndSceneConsumersSelectTheSameExternalMaterial()
    {
        FfnxTextureResolver resolver = new(new(1, [], [Sheet("field/model/main_chr/d000-0", 64, 64)]));
        TextureReplacement library = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("field/main_chr/d000.mch", "texture/0", "classic-field-model", 64, 64)));
        TextureReplacement scene = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("field/test/chara.one", "model/3/main/d000/texture/0", "classic-field-model", 64, 64)));
        Assert.Equal(scene.Image.Pixels, library.Image.Pixels);
        Assert.Equal(scene.Provenance, library.Provenance);
        Assert.Equal(scene.LogicalWidth, library.LogicalWidth);
        Assert.Equal(scene.LogicalHeight, library.LogicalHeight);
        Assert.Null(resolver.Resolve(Context("field/main_chr/d000.mch", "texture/1", "classic-field-model", 64, 64)));
        Assert.Null(resolver.Resolve(Context("field/main_chr/d000.mch", "texture/0", "other-layout", 64, 64)));
    }

    [Fact]
    public void NativeModelNamesAndSourceAliasesKeepLogicalUvSizeAndUseNativePaletteFallback()
    {
        FfnxTextureResolver resolver = new(new(1, [], [Sheet("field/model/second_chr/o000-0", 64, 64), Sheet("world/esk/chara_one/model1-0", 64, 64), Sheet("world/dat/wmset/section38/texture0", 256, 128)]));
        TextureReplacement field = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("field/test/chara.one", "model/3/second/o000/texture/0", "classic-field-model", 64, 64)));
        Assert.Equal(64, field.LogicalWidth);
        Assert.Equal(128, field.Image.Width);
        Assert.NotNull(resolver.Resolve(Context("world/chara.one", "model/1/0", "classic-model", 64, 64)));
        TextureBuildContext alias = Context("world/texl.obj", "texl/1", "classic-world-texl", 256, 256, new(new("world/wmsetus.obj"), "section/38/texture/0", 0, 128, 0, 128, 128, 256, 128));
        TextureReplacement region = Assert.IsType<TextureReplacement>(resolver.Resolve(alias));
        Assert.Equal(256, region.LogicalWidth);
        Assert.Equal(256, region.Image.Width);
        Assert.Equal(new Rgba32(128, 0, 7), region.Image[0, 0]);
        Assert.NotNull(resolver.Resolve(Context("world/chara.one", "model/1/0", "classic-model", 64, 64, palette: 1)));
    }

    [Fact]
    public void InspectionDoesNotDecodeAndRejectsUnknownFamilies()
    {
        ModFile file = new("test", ModFormat.Junction, "mods/textures/unknown/example_00.dds", 1, () => throw new InvalidOperationException());
        DemasterInspection result = FfnxTextureResolver.Inspect(Source(), new(1, [], [file]));
        Assert.Empty(result.UsablePackageIds);
        Assert.Equal("ffnx-unsupported-target", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void InspectionDistinguishesAbsentOriginalMembersFromUnsupportedLiveTargets()
    {
        byte[] resource = new byte[256];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(39 * 4), 196);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(196), 8);
        using InMemoryResourceSource source = new(Source());
        source.Set(new("world/wmsetus.obj"), resource, "obj");
        ModFile absent = new("test", ModFormat.Junction, "mods/textures/world/dat/wmset/section40/texture1_00.dds", 1, () => throw new InvalidOperationException());
        DemasterInspection result = FfnxTextureResolver.Inspect(source.Descriptor, new(1, [], [absent]), source);
        Assert.Empty(result.UsablePackageIds);
        Assert.Equal("ffnx-unused-native-target", Assert.Single(result.Diagnostics).Code);
        Assert.Equal("ffnx-unsupported-target", Assert.Single(FfnxTextureResolver.Inspect(source.Descriptor, new(1, [], [absent])).Diagnostics).Code);
    }

    [Fact]
    public void InstalledPackagesUseNativeCardTerrainModelAndParticleConsumers()
    {
        string? fixtures = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURE_CATALOG");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(File.Exists(fixtures) && Directory.Exists(game), "Configure optional external Junction fixture catalog and game data.");
        Dictionary<string, string> paths = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(fixtures!))!;
        Assert.SkipUnless(paths.ContainsKey("junction-card-textures") && paths.ContainsKey("junction-world-textures"), "Configure the optional card and world texture fixtures.");
        List<ModFile> files = [];
        foreach (string id in new[] { "junction-card-textures", "junction-world-textures" })
        {
            JunctionModPackage package = JunctionModImporter.Open(paths[id]);
            Assert.Empty(package.Diagnostics);
            foreach (JunctionReplacement replacement in package.Files.Where(file => file.RelativePath.StartsWith("mods/textures/", StringComparison.OrdinalIgnoreCase)))
            {
                files.Add(new(id, ModFormat.Junction, replacement.RelativePath, 1, () =>
                {
                    using MemoryStream output = new();
                    replacement.CopyTo(output);
                    return output.ToArray();
                }));
            }
        }
        Assert.True(files.Count > 50);
        foreach (ModFile file in files)
        {
            DecodedModImage decoded = ModImageDecoder.Decode(file);
            Assert.True(decoded.Image.Width > 0 && decoded.Image.Height > 0, file.RelativePath);
        }
        FfnxTextureResolver resolver = new(new(1, [], files));
        using ClassicGameDataSource source = new(game!);
        DemasterInspection inspection = FfnxTextureResolver.Inspect(source.Descriptor, new(1, [], files), source);
        Assert.Equal(4, inspection.Diagnostics.Count(diagnostic => diagnostic.Code == "ffnx-unused-native-target"));
        Assert.DoesNotContain(inspection.Diagnostics, diagnostic => diagnostic.Code == "ffnx-unsupported-target");
        ResourceService resources = new(source);
        List<Exception> failures = [];
        Dictionary<string, TextureSourceAddress> aliases = [];
        resources.ReplacementFailed += (_, exception) => failures.Add(exception);
        resources.Prepare(new ContextRecorder(resolver, context =>
        {
            if (context.NativeAlias is { } alias)
            {
                aliases[context.Key.Subresource] = alias;
            }
        })).Commit();
        NativeTripleTriadArtwork cards = NativeTripleTriadArtwork.Load(game!, resources);
        Assert.Equal(512, cards.GetFront(0).Width);
        Assert.Equal(512, cards.GetFront(109).Width);
        Assert.Equal(2688, cards.Background.Width);
        Assert.Equal(128, cards.GetElement(Memoria.FF8.Runtime.TripleTriadElement.Fire).Width);
        using WorldAssets world = new(game!, resources);
        PixelImage native = world.ReadNativeSectionTexture(38, 0);
        Assert.Equal(1024, native.Width);
        WorldTriangle triangle = new(Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, Vector2.Zero, Vector2.Zero, Vector2.Zero, 0, 0, 0, 0, 0);
        Assert.Equal(512, world.GetTexture(triangle).Width);
        Assert.Equal(64, world.GetForestTexture(4, 0).Width);
        foreach (byte type in new byte[] { 0, 12, 13, 15, 16, 19, 20, 21 })
        {
            var effect = world.ForestTables.GetEffect(type);
            Assert.True(world.GetForestTexture(type, 0).Width > effect.Width, $"Native world effect {type} did not consume its selected external source.");
        }
        FieldModel ragnarok = world.GetRagnarokModel();
        Assert.Equal(4, ResourceTextureList.PhysicalScale(ragnarok.Textures, 0).X);
        using GameAssets assets = new(game!, resources: resources);
        HashSet<string> objectNames = files.Where(file => file.RelativePath.Contains("/field/model/second_chr/", StringComparison.OrdinalIgnoreCase)).Select(file => Path.GetFileName(file.RelativePath)[..4]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ModelCatalogEntry entry = assets.EnumerateModelCatalog().First(entry => entry.Category == "Field" && entry.LoadError is null && entry.Name.Length >= 4 && objectNames.Contains(entry.Name[..4]));
        FieldAssets field = assets.LoadField(entry.FieldId!.Value);
        ResourceTextureList textures = Assert.IsType<ResourceTextureList>(field.Models[entry.ModelIndex].Textures);
        Assert.Contains("mods/textures/field/model/second_chr/" + field.Models[entry.ModelIndex].TextureResourceName + "-0_00", textures.GetTexture(0).Provenance, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(failures);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEXTURE_SOURCE_AUDIT_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            File.WriteAllText(output, JsonSerializer.Serialize(aliases));
        }
    }

    private static ResourceSourceDescriptor Source() => new("test", GameDataEdition.Classic, ["classic-ffnx"], "en");

    private sealed class ContextRecorder(ITextureReplacementResolver inner, Action<TextureBuildContext> record) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            record(context);
            return inner.Resolve(context);
        }
    }

    private static TextureBuildContext Context(string owner, string part, string layout, int width, int height, TextureSourceAddress? alias = null, int palette = 0) => new(new(new(owner), part, palette), Source(), 1, layout, width, height, () => new(width, height)) { NativeAlias = alias };

    private static ModFile Sheet(string name, int width, int height)
    {
        byte[] pixels = new byte[width * height * 4 * 4];
        for (int y = 0; y < height * 2; y++)
        {
            for (int x = 0; x < width * 2; x++)
            {
                int index = (y * width * 2 + x) * 4;
                pixels[index] = (byte)(x / 2);
                pixels[index + 1] = (byte)(y / 2);
                pixels[index + 2] = 7;
                pixels[index + 3] = 255;
            }
        }
        using MagickImage image = new();
        image.ReadPixels(pixels, new PixelReadSettings((uint)(width * 2), (uint)(height * 2), StorageType.Char, PixelMapping.RGBA));
        byte[] png = image.ToByteArray(MagickFormat.Png);
        return new("test", ModFormat.Junction, $"mods/textures/{name}_00.png", 1, () => png);
    }
}
