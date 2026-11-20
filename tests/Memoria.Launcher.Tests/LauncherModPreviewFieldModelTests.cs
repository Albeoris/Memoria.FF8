using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;

namespace Memoria.Launcher.Tests;

public sealed class LauncherModPreviewFieldModelTests
{
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
