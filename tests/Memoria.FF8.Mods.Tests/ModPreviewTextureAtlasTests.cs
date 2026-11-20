using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tex;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods.Tests;

public sealed class ModPreviewTextureAtlasTests
{
    private const string Cards = "mods/textures/eng/cardgame/cards_00.png";
    private const string Icons = "mods/textures/eng/cardgame/icons_00.png";
    private const string OutcomePage = "mods/textures/eng/cardgame/icons_1_0_255.png";

    [Fact]
    public async Task ListsExploreBeforeTheWorldMapTextureGroups()
    {
        await using PreviewFixture fixture = new();
        fixture.Write("mods/textures/world/esk/chara_one/model1-0_00.png", []);
        fixture.Write("mods/textures/world/dat/wmset/section39/texture0_00.png", []);
        ModPreviewDocument document = await fixture.Open();

        ModPreviewTreeNode world = Assert.Single(document.Tree.Branches);
        Assert.Equal("World map (3)", world.Label);
        Assert.Equal(new[] { "Explore (1)", "Characters and vehicles (1)", "Roads (1)" }, world.Children.Select(group => group.Label));
        Assert.Equal(ModPreviewDocument.WorldSceneId, Assert.Single(world.Children[0].Children).Entry?.Id);
    }

    [Fact]
    public async Task ListsSheetAndPageMembersWithoutDecodingPackageImages()
    {
        await using PreviewFixture fixture = new();
        fixture.Write(Cards, []);
        fixture.Write(Icons, []);
        fixture.Write(OutcomePage, []);
        ModPreviewDocument document = await fixture.Open();

        ModPreviewEntry[] cards = document.Entries.Where(entry => entry.SourcePath == Cards).ToArray();
        Assert.Equal(111, cards.Length);
        Assert.Equal(cards.Length, cards.Select(entry => entry.Id).Distinct().Count());
        Assert.All(cards, entry => Assert.Equal("cards_00.png", entry.Folder));
        Assert.Contains(cards, entry => entry.Name == "Card 001");
        Assert.Contains(cards, entry => entry.Name == "Card 110");
        Assert.Contains(cards, entry => entry.Name == "Card back");

        ModPreviewEntry[] icons = document.Entries.Where(entry => entry.SourcePath == Icons).ToArray();
        Assert.Equal(14, icons.Length);
        Assert.Contains(icons, entry => entry.Name == "Fire");
        Assert.Contains(icons, entry => entry.Name == "Player wins");
        Assert.Contains(icons, entry => entry.Name == "Combo");
        Assert.Equal(new[] { "Draw", "Opponent wins", "Player wins" }, document.Entries.Where(entry => entry.SourcePath == OutcomePage).Select(entry => entry.Name));
    }

    [Fact]
    public async Task ListsEachBattleArenaOnceAndSkipsUnusedDottedStageOwners()
    {
        await using PreviewFixture fixture = new();
        fixture.Write("mods/textures/battle/A0STG030.X-0_00.dds", []);
        fixture.Write("mods/textures/eng/battle/A0STG030.X-0_01.png", []);
        fixture.Write("mods/textures/battle/.A0STG030.X-0_02.dds", []);
        fixture.Write("mods/textures/battle/.A0STG031.X-0_00.dds", []);
        fixture.Write("mods/textures/battle/A0STG030.X-0_f0_4_0_243.dds", []);
        fixture.Write("mods/textures/battle/C0M001.DAT-0_00.dds", []);

        ModPreviewDocument document = await fixture.Open();

        ModPreviewEntry arena = Assert.Single(document.Entries, entry => entry.Category == "Battle arenas");
        Assert.Equal("A0STG030", arena.Name);
        Assert.Equal(arena, document.Tree.InitialPath[^1].Entry);
        ModPreviewEntry model = Assert.Single(document.Entries, entry => entry != arena);
        Assert.Equal("C0M001.DAT-0_00", model.Name);
    }

    [Fact]
    public async Task ComparesTheInstalledBattleArenaUsingTheSelectedPackageTextures()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Configure optional installed game resources.");

        await using PreviewFixture fixture = new();
        PixelImage texture = new(1, 1);
        texture[0, 0] = new(255, 0, 255);
        fixture.Write("mods/textures/battle/A0STG030.X-0_00.png", LocalizationArtworkExporter.EncodePng(texture));
        fixture.Write("mods/textures/battle/.A0STG030.X-0_01.dds", []);
        ModPreviewDocument document = await fixture.Open();
        ModPreviewEntry arena = Assert.Single(document.Entries);
        ModPreviewResourcesProfile profile = new(game!)
        {
            RenderBattleStage = stage =>
            {
                BattleRenderer renderer = new(320, 240);
                byte[] pixels = renderer.Render(stage, [], BattleCamera.FromNative(stage.IntroductionCameras[0]));
                return new(320, 240, MemoryMarshal.Cast<byte, Rgba32>(pixels).ToArray());
            }
        };

        ModPreviewComparison result = await document.CompareAsync(arena, profile, TestContext.Current.CancellationToken);

        PixelImage before = Assert.IsType<PixelImage>(result.Before.Image);
        PixelImage after = Assert.IsType<PixelImage>(result.After.Image);
        Assert.True(before.Pixels.Distinct().Count() > 2);
        Assert.False(before.Pixels.SequenceEqual(after.Pixels));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 3)]
    public async Task ComparesTheNamedCommonBattlePageAndPaletteFromTheInstalledClassicAtlas(int page, int palette)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Configure optional installed Classic game resources.");

        string path = $"mods/textures/battle/A8DEF{page}.TIM_{palette:00}.png";
        await using PreviewFixture fixture = new();
        PixelImage texture = new(512, 512);
        Array.Fill(texture.Pixels, new Rgba32(42, 76, 150, 255));
        texture[8, 16] = new(100, 160, 220, 255);
        byte[] replacementBytes = LocalizationArtworkExporter.EncodePng(texture);
        fixture.Write(path, replacementBytes);
        ModPreviewDocument document = await fixture.Open();
        ModPreviewEntry entry = Assert.Single(document.Entries);
        Assert.Equal("Battle", entry.Group);

        ModPreviewComparison comparison = await document.CompareAsync(entry, new(game!), TestContext.Current.CancellationToken);

        using IResourceSource source = GameDataSourceFactory.Open(game!);
        Assert.Equal(GameDataEdition.Classic, source.Descriptor.Edition);
        ResourceService resources = new(source);
        byte[] atlasBytes = resources.Read(new("battle/a8def.tim"));
        TimImage tim = TimFile.Read(atlasBytes).Images.Single();
        PixelImage atlas = GameAssets.DecodeEffectTexture(tim, palette);
        PixelImage original = Assert.IsType<PixelImage>(comparison.Before.Image);
        Assert.Equal((256, 256), (original.Width, original.Height));
        for (int y = 0; y < original.Height; y++)
        {
            Assert.True(atlas.Pixels.AsSpan(y * atlas.Width + page * 256, 256).SequenceEqual(original.Pixels.AsSpan(y * 256, 256)));
        }
        PixelImage replacement = Assert.IsType<PixelImage>(comparison.After.Image);
        Assert.Equal((512, 512), (replacement.Width, replacement.Height));
        Assert.Equal(new Rgba32(42, 76, 150, 127), replacement[256, 256]);

        ModFile file = new("fixture", ModFormat.Junction, path, 1, () => replacementBytes);
        resources.Prepare(new FfnxTextureResolver(new(1, [], [file]))).Commit();
        NativeBattleEffectHeapAllocation nativeAtlas = new(NativeBattleSharedEffectTexture.File.Name, atlasBytes.ToImmutableArray());
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess().EnqueueTim505E30(nativeAtlas, identity: NativeBattleSharedEffectTexture.File).State.Drain505D20().State;
        ushort vramPage = checked((ushort)(tim.X / 64 + page + (tim.Y >= 256 ? 16 : 0)));
        ushort clut = checked((ushort)(tim.Clut!.X / 16 + (tim.Clut.Y + palette) * 64));
        using NativeBattleEffectTextureSource runtime = new(resources);
        PixelImage applied = runtime.Apply(new(2, 2), state, vramPage, clut, 4, 8, 2, 2);
        Assert.Equal((4, 4), (applied.Width, applied.Height));
        Assert.Equal(new Rgba32(100, 160, 220, 127), applied[0, 0]);
        Assert.Equal(replacement[9, 16], applied[1, 0]);
    }

    [Fact]
    public void ComparesTheNamedMemberOfAnInstalledStandaloneBattleTim()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Configure optional installed Classic game resources.");

        using IResourceSource source = GameDataSourceFactory.Open(game!);
        const string owner = "battle/ma8def_0.tim";
        TimImage tim = TimFile.Read(new ResourceService(source).Read(new(owner))).Images[0];
        PixelImage expected = GameAssets.DecodeEffectTexture(tim, 1);
        byte[] replacementBytes = LocalizationArtworkExporter.EncodePng(new(expected.Width, expected.Height));
        const string path = "mods/textures/battle/MA8DEF_0.TIM-0_01.png";
        ModFile file = new("fixture", ModFormat.Junction, path, 1, () => replacementBytes);

        ModPreviewComparison result = Assert.IsType<ModPreviewComparison>(ModPreviewResources.CompareTexture(game!, null, "en", new(1, [], [file]), path, TestContext.Current.CancellationToken));

        Assert.True(expected.Pixels.SequenceEqual(Assert.IsType<PixelImage>(result.Before.Image).Pixels));
        Assert.NotNull(result.After.Image);
    }

    [Fact]
    public void CompositeBattleEffectDoesNotRequireTheClassicFieldCatalog()
    {
        const string path = "mods/textures/battle/MAG012_E.DAT-0_f2_1_320_224.png";
        ModFile file = new("fixture", ModFormat.Junction, path, 1, () => throw new InvalidOperationException("The comparison should leave decoding to the package-image fallback."));

        Assert.Null(ModPreviewResources.CompareTexture(Path.GetTempPath(), null, "en", new(1, [], [file]), path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ComparesEachSelectedIconPageWithItsOwnInstalledPaletteAndPackageImage()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Configure optional installed game resources.");

        await using PreviewFixture fixture = new();
        using IResourceSource source = GameDataSourceFactory.Open(game!);
        ResourceService resources = new(source);
        Dictionary<string, PixelImage> replacements = new();
        foreach (int page in new[] { 1, 3 })
        {
            PixelImage image = new(1024, 1024);
            image[123, 456] = new((byte)page, 80, 120, 200);
            string name = $"iconfl{page:00}";
            replacements.Add(name, image);
            fixture.Write($"mods/textures/data/eng/menu/hires/{name}_04.png", LocalizationArtworkExporter.EncodePng(image));
        }
        ModPreviewDocument document = await fixture.Open();
        Assert.Equal(2, document.Entries.Count);

        foreach ((string name, PixelImage replacement) in replacements)
        {
            ModPreviewEntry entry = Assert.Single(document.Entries, entry => entry.Name == name + "_04");
            ModPreviewComparison comparison = await document.CompareAsync(entry, new(game!), TestContext.Current.CancellationToken);
            PixelImage before = Assert.IsType<PixelImage>(comparison.Before.Image);
            PixelImage after = Assert.IsType<PixelImage>(comparison.After.Image);
            PixelImage original = TexFile.Read(resources.Read(new($"menu/{name}.tex"))).Decode(4);
            Assert.Equal((original.Width, original.Height), (before.Width, before.Height));
            Assert.Equal(original.Pixels, before.Pixels);
            Assert.Equal((replacement.Width, replacement.Height), (after.Width, after.Height));
            Assert.Equal(replacement.Pixels, after.Pixels);
        }
    }

    [Fact]
    public async Task ComparesEachSelectedCardWithItsOwnInstalledOriginalAndPackageCrop()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Configure optional installed game resources.");

        await using PreviewFixture fixture = new();
        PixelImage sheet = new(1792, 256);
        Rgba32 firstColor = new(10, 20, 30);
        Rgba32 lastColor = new(80, 90, 100);
        sheet[0, 0] = firstColor;
        sheet[13 * 128 + 64, 2 * 64] = lastColor;
        fixture.Write(Cards, LocalizationArtworkExporter.EncodePng(sheet));
        ModPreviewDocument document = await fixture.Open();
        ModPreviewEntry first = Assert.Single(document.Entries, entry => entry.Name == "Card 001");
        ModPreviewEntry last = Assert.Single(document.Entries, entry => entry.Name == "Card 110");

        ModPreviewComparison firstComparison = await document.CompareAsync(first, new(game!), TestContext.Current.CancellationToken);
        ModPreviewComparison lastComparison = await document.CompareAsync(last, new(game!), TestContext.Current.CancellationToken);

        PixelImage firstOriginal = Assert.IsType<PixelImage>(firstComparison.Before.Image);
        PixelImage lastOriginal = Assert.IsType<PixelImage>(lastComparison.Before.Image);
        Assert.Equal(64, firstOriginal.Width);
        Assert.Equal(64, lastOriginal.Width);
        Assert.False(firstOriginal.Pixels.SequenceEqual(lastOriginal.Pixels));
        Assert.Equal(firstColor, Assert.IsType<PixelImage>(firstComparison.After.Image)[0, 0]);
        Assert.Equal(lastColor, Assert.IsType<PixelImage>(lastComparison.After.Image)[0, 0]);
    }

    [Fact]
    public async Task KeepsAnImageWithNoMatchingFieldMaterialInTheStandaloneView()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Configure optional installed game resources.");

        await using PreviewFixture fixture = new();
        PixelImage image = new(128, 128);
        image[25, 40] = new(190, 35, 70, 255);
        fixture.Write("mods/textures/field/model/second_chr/p011-99.png", LocalizationArtworkExporter.EncodePng(image));
        ModPreviewDocument document = await fixture.Open();
        ModPreviewEntry entry = Assert.Single(document.Entries);

        ModPreviewComparison comparison = await document.CompareAsync(entry, new(game!), TestContext.Current.CancellationToken);
        Assert.False(comparison.Before.HasModel);
        Assert.False(comparison.After.HasModel);
        Assert.Null(comparison.Before.Image);
        Assert.Equal(image.Pixels, Assert.IsType<PixelImage>(comparison.After.Image).Pixels);
    }

    [Theory]
    [InlineData(ModFormat.Junction)]
    [InlineData(ModFormat.Memoria)]
    public void ComparesRagnarokWithAllOfTheSelectedPackagesModelTextures(ModFormat format)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Configure optional installed game resources.");
        using IResourceSource source = GameDataSourceFactory.Open(game!);
        using GameAssets assets = new(game!, resources: new(source));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ModelCatalogEntry entry = assets.EnumerateModelCatalog(cancellationToken).First(model => model.Id == "world/1");
        EditableModel installed = assets.LoadCatalogModel(entry);
        Rgba32[] colors = [new(255, 0, 255), new(0, 255, 255)];
        ModFile[] files = [CreateReplacement(0), CreateReplacement(1)];

        ModPreviewComparison? comparison = ModPreviewResources.CompareTexture(game!, null, "en", new(1, [], files), files[1].RelativePath, cancellationToken);

        Assert.NotNull(comparison);
        EditableModel before = Assert.IsType<EditableModel>(comparison.Before.Model);
        EditableModel after = Assert.IsType<EditableModel>(comparison.After.Model);
        Assert.Null(comparison.Before.Image);
        Assert.Null(comparison.After.Image);
        Assert.Equal(installed.Name, before.Name);
        Assert.Same(before.Meshes, after.Meshes);
        Assert.Contains(after.Meshes, mesh => mesh.Texture == 1);
        Assert.Equal(installed.Textures[1].Pixels, before.Textures[1].Pixels);
        Assert.Equal(colors[0], after.Textures[0][0, 0]);
        Assert.Equal(colors[1], after.Textures[1][0, 0]);
        Assert.Equal(before.Textures[1].Width * 2, after.Textures[1].Width);
        Assert.Equal(before.Textures[2].Pixels, after.Textures[2].Pixels);

        ModFile CreateReplacement(int slot)
        {
            string path = format == ModFormat.Junction
                ? $"mods/textures/world/esk/chara_one/model1-{slot}_00.png"
                : new TextureTarget("ff8", "world/chara.one", $"model/1/texture/{slot}").CanonicalPath();
            PixelImage original = installed.Textures[slot];
            PixelImage image = new(original.Width * 2, original.Height * 2);
            Array.Fill(image.Pixels, colors[slot]);
            byte[] bytes = LocalizationArtworkExporter.EncodePng(image);
            return new("fixture", format, path, 1, () => bytes);
        }
    }

    private sealed class PreviewFixture : IAsyncDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), $"mod-preview-atlas-{Guid.NewGuid():N}");
        private readonly ModRuntime mods;
        private readonly ModLibraryService library;

        public PreviewFixture()
        {
            Directory.CreateDirectory(PackagePath);
            File.WriteAllText(Path.Combine(PackagePath, "mod.xml"), "<ModInfo><Name>Card artwork</Name></ModInfo>");
            mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
        }

        private string PackagePath => Path.Combine(root, "package");

        public void Write(string relativePath, byte[] content)
        {
            string path = Path.Combine(PackagePath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content);
        }

        public Task<ModPreviewDocument> Open()
        {
            ModPackage package = new("Junction/fixture", "Card artwork", ModFormat.Junction, false, false, ModEffectiveStatus.Disabled, PackagePath);
            return library.OpenPreviewAsync(null, package, TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await library.StopAsync();
            library.Dispose();
            mods.Dispose();
            Directory.Delete(root, true);
        }
    }
}
