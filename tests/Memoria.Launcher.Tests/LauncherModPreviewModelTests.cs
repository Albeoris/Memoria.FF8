using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.Launcher.Tests;

public sealed class LauncherModPreviewModelTests
{
    [Theory]
    [InlineData(ModFormat.Junction)]
    [InlineData(ModFormat.Memoria)]
    public async Task EnemyTexturePreviewRendersBothModels(ModFormat format)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Configure installed Classic game resources.");

        CancellationToken token = TestContext.Current.CancellationToken;
        string root = Path.Combine(Path.GetTempPath(), "launcher-preview-model-" + Guid.NewGuid().ToString("N"));
        try
        {
            using ModRuntime mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
            await library.StopAsync();
            string payload = format == ModFormat.Junction ? FindJunctionArchive() : CreateMemoriaTextures(root, game!, token);
            ModPackage package = new($"{format}/fixture", "Enemy textures", format, null, false, ModEffectiveStatus.Disabled, payload);
            ModPreviewDocument document = await library.OpenPreviewAsync(null, package, token);
            ModPreviewEntry entry = Assert.Single(document.Entries, item => format == ModFormat.Memoria
                ? item.Id == Target(0).CanonicalPath()
                : item.Name.Equals("c0m005.DAT-0_00", StringComparison.OrdinalIgnoreCase));

            ModPreviewComparison pair = await CatalogPreviewPresentation.CompareAsync(document, entry, CatalogPreviewPresentation.CreateProfile(game!, null, "en"), token, modelWidth: 1024);
            EditableModel before = Assert.IsType<EditableModel>(pair.Before.Model);
            EditableModel after = Assert.IsType<EditableModel>(pair.After.Model);
            Assert.NotEmpty(before.Meshes);
            Assert.Same(before.Meshes, after.Meshes);
            Assert.Same(before.Animations, after.Animations);
            Assert.True(after.Textures[0].Width > before.Textures[0].Width);
            Assert.True(after.Textures[1].Width > before.Textures[1].Width);

            PixelImage original = Assert.IsType<PixelImage>(pair.Before.Image);
            PixelImage replacement = Assert.IsType<PixelImage>(pair.After.Image);
            Assert.Equal((1024, 768), (original.Width, original.Height));
            Assert.Equal((original.Width, original.Height), (replacement.Width, replacement.Height));
            Assert.Contains(original.Pixels, pixel => pixel != original.Pixels[0]);
            Assert.Contains(replacement.Pixels, pixel => pixel != replacement.Pixels[0]);
            Assert.False(original.Pixels.SequenceEqual(replacement.Pixels));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JunctionBattleWeaponComparisonRendersBothModels(bool useUnchangedArchive)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Configure installed Classic game resources.");

        CancellationToken token = TestContext.Current.CancellationToken;
        string root = Path.Combine(Path.GetTempPath(), "launcher-preview-weapon-" + Guid.NewGuid().ToString("N"));
        try
        {
            using ModRuntime mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
            await library.StopAsync();
            string payload = useUnchangedArchive ? FindJunctionArchive("Launcher.Tests.LauncherModPreviewModelTests.2") : CreateJunctionWeaponReplacement(root, game!);
            ModPackage package = new("Junction/fixture", "Battle weapon models", ModFormat.Junction, null, false, ModEffectiveStatus.Disabled, payload);
            ModPreviewDocument document = await library.OpenPreviewAsync(null, package, token);
            ModPreviewEntry entry = Assert.Single(document.Entries, item => item.Category == "Models" && item.Name.Equals("d0w003.dat", StringComparison.OrdinalIgnoreCase));
            ModPreviewComparison pair = await CatalogPreviewPresentation.CompareAsync(document, entry, CatalogPreviewPresentation.CreateProfile(game!, null, "en"), token);

            Assert.NotEmpty(Assert.IsType<EditableModel>(pair.Before.Model).Meshes);
            Assert.NotEmpty(Assert.IsType<EditableModel>(pair.After.Model).Meshes);
            PixelImage before = Assert.IsType<PixelImage>(pair.Before.Image);
            PixelImage after = Assert.IsType<PixelImage>(pair.After.Image);
            Assert.Equal((before.Width, before.Height), (after.Width, after.Height));
            Assert.Contains(before.Pixels, pixel => pixel != before.Pixels[0]);
            Assert.Contains(after.Pixels, pixel => pixel != after.Pixels[0]);
            Assert.Equal(useUnchangedArchive, before.Pixels.AsSpan().SequenceEqual(after.Pixels));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string CreateJunctionWeaponReplacement(string root, string game)
    {
        using IResourceSource source = GameDataSourceFactory.Open(game);
        ResourceService resources = new(source);
        string package = Path.Combine(root, "package");
        string path = Path.Combine(package, "direct", "battle", "D0W003.DAT");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, resources.Read(new("battle/d0w002.dat")));
        File.WriteAllText(Path.Combine(package, "mod.xml"), "<ModInfo><Name>Battle weapon fixture</Name></ModInfo>");
        return package;
    }

    private static string FindJunctionArchive(string fixtureId = "Launcher.Tests.LauncherModPreviewModelTests.1")
    {
        string? catalog = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURE_CATALOG");
        Assert.SkipUnless(File.Exists(catalog), "Configure the optional fixture catalog.");
        Dictionary<string, string> fixtures = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalog!))!;
        Assert.SkipUnless(fixtures.TryGetValue(fixtureId, out string? configured), $"Configure the Junction fixture {fixtureId}.");
        string fixtureRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURES") ?? Path.GetDirectoryName(Path.GetFullPath(catalog!))!;
        string archive = Path.GetFullPath(configured!, fixtureRoot);
        Assert.True(File.Exists(archive), "The configured fixture archive is unavailable.");
        return archive;
    }

    private static string CreateMemoriaTextures(string root, string game, CancellationToken token)
    {
        using GameAssets assets = new(game);
        ModelCatalogEntry entry = assets.EnumerateModelCatalog(token).First(model => model.ResourcePath == Target(0).Owner);
        EditableModel model = assets.LoadCatalogModel(entry);
        string package = Path.Combine(root, "package");
        Rgba32[] colors = [new(240, 12, 110), new(12, 240, 110)];
        for (int slot = 0; slot < colors.Length; slot++)
        {
            PixelImage original = model.Textures[slot];
            PixelImage image = new(original.Width * 2, original.Height * 2);
            Array.Fill(image.Pixels, colors[slot]);
            string path = Path.Combine(package, Target(slot).CanonicalPath());
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, LocalizationArtworkExporter.EncodePng(image));
        }
        return package;
    }

    private static TextureTarget Target(int slot) => new("ff8", "battle/c0m005.dat", $"texture/{slot}");
}
