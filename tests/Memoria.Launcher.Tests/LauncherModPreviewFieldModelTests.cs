using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;

namespace Memoria.Launcher.Tests;

public sealed class LauncherModPreviewFieldModelTests
{
    [Theory]
    [InlineData("p011", "glfurin1", 6)]
    [InlineData("p025", "gpbig1a", 9)]
    public async Task JunctionTextureWithoutPaletteSuffixRendersOnTheSourceFieldModel(string modelName, string fieldName, int modelIndex)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? catalog = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURE_CATALOG");
        Assert.SkipUnless(Directory.Exists(game) && File.Exists(catalog), "Configure installed Classic game resources and the optional fixture catalog.");
        Dictionary<string, string> fixtures = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalog!))!;
        Assert.SkipUnless(fixtures.TryGetValue("Launcher.Tests.LauncherModPreviewFieldModelTests.1", out string? configured), "Configure the Junction field texture fixture.");
        string fixtureRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURES") ?? Path.GetDirectoryName(Path.GetFullPath(catalog!))!;
        string archive = Path.GetFullPath(configured!, fixtureRoot);
        string root = Path.Combine(Path.GetTempPath(), "launcher-preview-field-texture-" + Guid.NewGuid().ToString("N"));
        CancellationToken token = TestContext.Current.CancellationToken;
        try
        {
            using ModRuntime mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
            await library.StopAsync();
            ModPackage package = new("Junction/fixture", "Field texture fixture", ModFormat.Junction, null, false, ModEffectiveStatus.Disabled, archive);
            ModPreviewDocument document = await library.OpenPreviewAsync(null, package, token);
            ModPreviewEntry entry = Assert.Single(document.Entries, item => item.Id == $"mods/textures/field/model/second_chr/{modelName}-0.png");
            Assert.Equal("Field models", entry.Group);

            ModPreviewComparison comparison = await CatalogPreviewPresentation.CompareAsync(document, entry, CatalogPreviewPresentation.CreateProfile(game!, null, "en"), token);
            FieldModel original = Assert.IsType<FieldModel>(comparison.Before.FieldModel);
            FieldModel changed = Assert.IsType<FieldModel>(comparison.After.FieldModel);
            Assert.Equal(modelName, original.TextureResourceName);
            Assert.Contains($"{fieldName}/{modelIndex}", comparison.Before.Text);
            Assert.Same(original.Faces, changed.Faces);
            Assert.Same(original.Animations, changed.Animations);
            PixelImage before = Assert.IsType<PixelImage>(comparison.Before.Image);
            PixelImage after = Assert.IsType<PixelImage>(comparison.After.Image);
            Assert.Equal((480, 360), (before.Width, before.Height));
            Assert.Equal((before.Width, before.Height), (after.Width, after.Height));
            Assert.Contains(before.Pixels, pixel => pixel != before.Pixels[0]);
            Assert.Contains(after.Pixels, pixel => pixel != after.Pixels[0]);
            Assert.False(before.Pixels.AsSpan().SequenceEqual(after.Pixels));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void FieldModelComparisonStaysUprightAndUsesNativeFaceCullingOnBothSides()
    {
        Rgba32 top = new(210, 65, 35, 255);
        Rgba32 bottom = new(45, 110, 170, 255);
        Rgba32 back = new(20, 220, 20, 255);
        // The root's native quarter turn maps these vertices onto the XZ plane.
        ModelVertex[] vertices =
        [
            new(new(-40, 0, -20), 0), new(new(-40, 0, 20), 0),
            new(new(0, 0, -20), 0), new(new(0, 0, 20), 0),
            new(new(40, 0, -20), 0), new(new(40, 0, 20), 0)
        ];
        Vector2[] uv = [Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero];
        ModelFace upper = new([0, 1, 2, 3], uv, 0, true);
        ModelFace lower = new([2, 3, 4, 5], uv, 1, true);
        ModelFace reverse = new([1, 0, 3, 2], uv, 2, true);
        FieldModel original = new("coat", [new(-1, 0)], vertices, [upper, lower], [new(1, 1, [top]), new(1, 1, [bottom]), new(1, 1, [back])], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        FieldModel replacement = original with { Faces = [reverse, upper, lower] };
        ModPreviewComparison models = new(new("Original") { FieldModel = original }, new("Replacement") { FieldModel = replacement });
        ModelPreviewCamera camera = new();

        ModPreviewComparison frame = CatalogPreviewPresentation.DrawModels(models, camera, 0);
        PixelImage before = Assert.IsType<PixelImage>(frame.Before.Image);
        PixelImage after = Assert.IsType<PixelImage>(frame.After.Image);
        Assert.Equal(top, before.Pixels[before.Width * (before.Height / 4) + before.Width / 2]);
        Assert.Equal(bottom, before.Pixels[before.Width * (before.Height * 3 / 4) + before.Width / 2]);
        Assert.True(before.Pixels.AsSpan().SequenceEqual(after.Pixels));

        camera.Orbit(MathF.PI, 0);
        frame = CatalogPreviewPresentation.DrawModels(models, camera, 0);
        Assert.DoesNotContain(back, frame.Before.Image!.Pixels);
        Assert.Contains(back, frame.After.Image!.Pixels);
    }

    [Fact]
    public async Task FieldTextureComparisonUsesTheNativeFieldPoseAndLogicalTextureSize()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Configure installed Classic game resources.");

        CancellationToken token = TestContext.Current.CancellationToken;
        string root = Path.Combine(Path.GetTempPath(), "launcher-preview-field-texture-" + Guid.NewGuid().ToString("N"));
        try
        {
            using GameAssets assets = new(game!);
            ModelCatalogEntry modelEntry = assets.GetFieldModelSource("eciway12", 9);
            FieldModel fieldModel = assets.LoadCatalogFieldModel(modelEntry);
            Assert.Equal("o196", fieldModel.TextureResourceName);
            PixelImage original = fieldModel.Textures[0];
            const int scale = 4;
            PixelImage replacement = new(original.Width * scale, original.Height * scale);
            for (int y = 0; y < replacement.Height; y++)
            {
                for (int x = 0; x < replacement.Width; x++)
                {
                    replacement[x, y] = original[x / scale, y / scale];
                }
            }
            const string name = "o196-0_00";
            string payload = Path.Combine(root, "package");
            string texturePath = Path.Combine(payload, "mods", "textures", "field", "model", "second_chr", name + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(texturePath)!);
            File.WriteAllBytes(texturePath, LocalizationArtworkExporter.EncodePng(replacement));
            File.WriteAllText(Path.Combine(payload, "mod.xml"), "<ModInfo><Name>Field texture fixture</Name></ModInfo>");

            using ModRuntime mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
            await library.StopAsync();
            ModPackage package = new("Junction/fixture", "Field texture fixture", ModFormat.Junction, null, false, ModEffectiveStatus.Disabled, payload);
            ModPreviewDocument document = await library.OpenPreviewAsync(null, package, token);
            ModPreviewEntry item = Assert.Single(document.Entries, entry => entry.Name == name);
            ModPreviewComparison pair = await CatalogPreviewPresentation.CompareAsync(document, item, CatalogPreviewPresentation.CreateProfile(game!, null, "en"), token);

            FieldModel before = Assert.IsType<FieldModel>(pair.Before.FieldModel);
            FieldModel after = Assert.IsType<FieldModel>(pair.After.FieldModel);
            Assert.Equal(fieldModel.Name, before.Name);
            Assert.Same(before.Faces, after.Faces);
            Assert.Same(before.Animations, after.Animations);
            Assert.Equal((original.Width, original.Height), (before.Textures[0].Width, before.Textures[0].Height));
            Assert.Equal((replacement.Width, replacement.Height), (after.Textures[0].Width, after.Textures[0].Height));
            Assert.Equal(new Vector2(original.Width, original.Height), ResourceTextureList.LogicalSize(after.Textures, 0));
            PixelImage beforeImage = Assert.IsType<PixelImage>(pair.Before.Image);
            PixelImage afterImage = Assert.IsType<PixelImage>(pair.After.Image);
            Rgba32 background = beforeImage.Pixels[0];
            Assert.Contains(beforeImage.Pixels, pixel => pixel != background);
            Assert.True(beforeImage.Pixels.Select(pixel => pixel == background).SequenceEqual(afterImage.Pixels.Select(pixel => pixel == background)));
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
    [InlineData("d000.mch")]
    [InlineData("d065.mch")]
    public async Task InstalledFieldComparisonRendersTheOriginalAndTheJunctionReplacement(string file)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? archive = Environment.GetEnvironmentVariable("MEMORIA_FF8_PREVIEW_FIELD_MODEL_FIXTURE");
        Assert.SkipUnless(Directory.Exists(game) && File.Exists(archive), "Configure the original game and MEMORIA_FF8_PREVIEW_FIELD_MODEL_FIXTURE for the installed comparison.");
        CancellationToken token = TestContext.Current.CancellationToken;
        string root = Path.Combine(Path.GetTempPath(), "launcher-preview-field-model-" + Guid.NewGuid().ToString("N"));
        try
        {
            using ModRuntime mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
            await library.StopAsync();
            ModPackage package = new("Junction/fixture", "Field model fixture", ModFormat.Junction, null, false, ModEffectiveStatus.Disabled, archive!);
            ModPreviewDocument document = await library.OpenPreviewAsync(null, package, token);
            ModPreviewEntry item = Assert.Single(document.Entries, entry => entry.Category == "Models" && entry.Name.Equals(file, StringComparison.OrdinalIgnoreCase));
            ModPreviewComparison comparison = await CatalogPreviewPresentation.CompareAsync(document, item, new(game!), token);
            Assert.NotNull(comparison.Before.FieldModel);
            Assert.NotNull(comparison.After.FieldModel);
            PixelImage before = Assert.IsType<PixelImage>(comparison.Before.Image);
            PixelImage after = Assert.IsType<PixelImage>(comparison.After.Image);
            Assert.Contains(before.Pixels, pixel => !pixel.Equals(before.Pixels[0]));
            Assert.Contains(after.Pixels, pixel => !pixel.Equals(after.Pixels[0]));
            Assert.False(before.Pixels.AsSpan().SequenceEqual(after.Pixels));

            ModelPreviewCamera camera = CatalogPreviewPresentation.CreateModelCamera();
            camera.Orbit(0.5f, -0.3f);
            camera.PanBy(0.08f, -0.02f);
            camera.ZoomBy(1.3f);
            ModPreviewComparison reframed = CatalogPreviewPresentation.DrawModels(comparison, camera, 0.15f);
            Assert.False(before.Pixels.AsSpan().SequenceEqual(reframed.Before.Image!.Pixels));
            Assert.False(after.Pixels.AsSpan().SequenceEqual(reframed.After.Image!.Pixels));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
