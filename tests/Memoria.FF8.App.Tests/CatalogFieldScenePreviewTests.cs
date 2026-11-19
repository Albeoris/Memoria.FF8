using Memoria.FF8.Assets;
using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

public sealed class CatalogFieldScenePreviewTests
{
    [Theory]
    [InlineData("bghall_6", 175, 1)]
    [InlineData("gltown1", 761, 5)]
    [InlineData("bgroom_1", 232, 0)]
    public async Task BundledDepixelationRendersOriginalTilesAndChangedActorsInContext(string fieldName, int fieldId, int actorCount)
    {
        string? gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(gameDirectory), "Configure MEMORIA_FF8_GAME_DIR to preview the original field assets.");
        CancellationToken token = TestContext.Current.CancellationToken;
        string root = Path.Combine(Path.GetTempPath(), $"catalog-field-preview-{Guid.NewGuid():N}");
        string bundle = Path.Combine(AppContext.BaseDirectory, "BundledMods", "Memoria", "Depixelation");
        string relative = $"fields/{fieldName}.json";
        ModPackage package = new("Memoria/Depixelation", "Depixelation", ModFormat.Memoria, false, false, ModEffectiveStatus.Disabled, bundle) { IsBundled = true };
        ModFile payload = new(package.Id, package.Format, relative, 0, () => File.ReadAllBytes(Path.Combine(bundle, relative)));
        using ModRuntime mods = new(root, watch: false);
        ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
        using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
        try
        {
            ModPreviewDocument document = await library.OpenPreviewAsync(null, package, token);
            ModPreviewEntry entry = Assert.Single(document.Entries, candidate => candidate.Id == relative);
            ModPreviewResourcesProfile profile = CatalogPreviewPresentation.CreateProfile(gameDirectory!, null, "en") with
            {
                CompareFieldScene = CatalogPreviewPresentation.CompareFieldScene
            };
            ModPreviewComparison comparison = await document.CompareAsync(entry, profile, token);
            Assert.NotNull(comparison.Before.Image);
            Assert.NotNull(comparison.After.Image);
            Assert.False(comparison.Before.Image.Pixels.AsSpan().SequenceEqual(comparison.After.Image.Pixels));
            Assert.DoesNotContain("Scene placement unavailable", comparison.After.Text);

            if (actorCount > 0)
            {
                Assert.Contains($"{actorCount} changed", comparison.After.Text);
                Assert.True(FieldSceneMods.TryPrepare(new(0, [package], [payload]), out var changes, out _));
                FieldScenePatch backgroundOnly = Assert.Single(changes.Patches).Patch with { Actors = [] };
                using GameAssets assets = new(gameDirectory!);
                FieldAssets field = assets.LoadField(fieldId);
                ModPreviewComparison withoutActors = CatalogPreviewPresentation.CompareFieldScene(field, field, [backgroundOnly], assets.LoadField, token);
                Assert.True(comparison.Before.Image.Pixels.AsSpan().SequenceEqual(withoutActors.Before.Image!.Pixels));
                Assert.False(comparison.After.Image.Pixels.AsSpan().SequenceEqual(withoutActors.After.Image!.Pixels));
            }
        }
        finally
        {
            await library.StopAsync();
            mods.Dispose();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
