using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationArtworkProjectTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.Localization.Artwork.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("json", "single", "xlsx", "split")]
    [InlineData("json", "split", "xlsx", "single")]
    [InlineData("xlsx", "single", "json", "split")]
    [InlineData("xlsx", "split", "json", "single")]
    public void OfflineConversionPreservesSeparateSourceTargetPngBytesAndNativeIdentity(string format, string layout, string convertedFormat, string convertedLayout)
    {
        LocalizationProject project = Project();
        string first = Path.Combine(root, "first");
        string second = Path.Combine(root, "second");
        LocalizationProjectIO.WriteNew(project, first, format, layout, TestContext.Current.CancellationToken);
        LocalizationProjectIO.Convert(first, second, convertedFormat, convertedLayout, TestContext.Current.CancellationToken);
        LocalizationProject restored = LocalizationProjectIO.Read(second);
        Assert.Equal(project.Manifest.ProjectId, restored.Manifest.ProjectId);
        Assert.Equal(project.Entries[0].Source, restored.Entries[0].Source);
        Assert.Equal(project.Entries[0].Target, restored.Entries[0].Target);
        LocalizationArtworkFile file = Assert.Single(restored.Artwork);
        Assert.Equal(project.Artwork[0].Declaration, file.Declaration);
        Assert.Equal(project.Artwork[0].SourcePng, file.SourcePng);
        Assert.Equal(project.Artwork[0].TargetPng, file.TargetPng);
        Assert.NotEqual(file.SourcePng, file.TargetPng);
        Assert.Equal(file.SourcePng, File.ReadAllBytes(Path.Combine(second, file.Declaration.SourceImage)));
        Assert.Equal(file.TargetPng, File.ReadAllBytes(Path.Combine(second, file.Declaration.TargetImage!)));
    }

    [Fact]
    public void PatchKeepsEditableOriginalBitmapAndStartsWithoutTranslatedImages()
    {
        LocalizationProject original = Project();
        LocalizationProject patch = LocalizationProjectExport.CreatePatch(original, "Bitmap patch");
        Assert.NotEqual(original.Manifest.ProjectId, patch.Manifest.ProjectId);
        Assert.Equal(original.Manifest.ProjectId, patch.Manifest.BaseProjectId);
        Assert.Equal("", Assert.Single(patch.Entries).Target);
        LocalizationArtworkFile file = Assert.Single(patch.Artwork);
        Assert.Equal(original.Artwork[0].SourcePng, file.SourcePng);
        Assert.Equal(original.Artwork[0].Declaration.Key, file.Declaration.Key);
        Assert.Equal(original.Artwork[0].Declaration.SourceSha256, file.Declaration.SourceSha256);
        Assert.Null(file.TargetPng);
        string output = Path.Combine(root, "patch");
        LocalizationProjectIO.WriteNew(patch, output, "xlsx", "single", TestContext.Current.CancellationToken);
        Assert.Null(Assert.Single(LocalizationProjectIO.Read(output).Artwork).TargetPng);
        Assert.NotNull(original.Artwork[0].TargetPng);
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("Localization/Artwork/../escape.png")]
    [InlineData("Localization/Artwork/source.png")]
    [InlineData("Localization/Artwork/target.dds")]
    [InlineData("C:/absolute.png")]
    public void TraversalAliasedPathsAndUnsupportedImageDeclarationsAreRejectedBeforeWriting(string targetPath)
    {
        LocalizationProject project = Project();
        LocalizationArtworkFile file = project.Artwork[0];
        LocalizationArtworkDeclaration invalid = file.Declaration with { TargetImage = targetPath };
        project = project with { Manifest = project.Manifest with { Artwork = [invalid] }, Artwork = [file with { Declaration = invalid }] };
        string output = Path.Combine(root, "invalid");
        Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.WriteNew(project, output, "json", "single", TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void DuplicateNativeIdentityAndChangedSourceOrTargetCanvasAreRejected()
    {
        LocalizationProject project = Project();
        LocalizationArtworkFile file = project.Artwork[0];
        Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.Validate(project with { Manifest = project.Manifest with { Artwork = [file.Declaration, file.Declaration] }, Artwork = [file, file] }));
        Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.Validate(project with { Artwork = [file with { SourcePng = Png(3, 1, 1) }] }));
        Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.Validate(project with { Artwork = [file with { TargetPng = Png(3, 1, 1) }] }));
    }

    [Fact]
    public void RuntimeUsesOrderedImageTargetsAndAnEmptyPatchFallsThrough()
    {
        LocalizationProject basis = Project();
        LocalizationProject empty = Project() with { Artwork = [Project().Artwork[0] with { TargetPng = null }] };
        ModPackage first = Package("base", basis);
        ModPackage second = Package("patch", Project(99));
        ModPackage blank = Package("empty", empty);
        ModCatalogSnapshot snapshot = new(1, [first, second, blank], []);
        LocalizationPreparation prepared = LocalizationRuntime.Prepare(snapshot, [first.Id, second.Id, blank.Id], new("synthetic", GameDataEdition.Classic, [], "en"), TestContext.Current.CancellationToken);
        LocalizationArtworkValue selected = Assert.Single(prepared.Catalog.Artwork).Value;
        Assert.Equal((byte)99, selected.Pixels[0].R);
        Assert.Equal(second.Id, selected.PackageId);
        LocalizationPreparation reversed = LocalizationRuntime.Prepare(snapshot, [second.Id, first.Id, blank.Id], new("synthetic", GameDataEdition.Classic, [], "en"), TestContext.Current.CancellationToken);
        Assert.Equal((byte)50, Assert.Single(reversed.Catalog.Artwork).Value.Pixels[0].R);
    }

    [Fact]
    public void InvalidEditedTargetRetainsThePreviouslyPublishedTextAndImageGeneration()
    {
        LocalizationProject project = Project();
        ModPackage package = Package("package", project);
        ResourceSourceDescriptor descriptor = new("synthetic", GameDataEdition.Classic, [], "en");
        LocalizationPreparation valid = LocalizationRuntime.Prepare(new(1, [package], []), [package.Id], descriptor, TestContext.Current.CancellationToken);
        using InMemoryResourceSource source = new(descriptor);
        ResourceService resources = new(source);
        resources.Prepare(null, localization: valid.Catalog).Commit();
        string fingerprint = resources.Localization.Fingerprint;
        File.WriteAllBytes(Path.Combine(package.RootDirectory, project.Artwork[0].Declaration.TargetImage!), Png(3, 1, 1));
        Assert.Throws<InvalidDataException>(() => LocalizationRuntime.Prepare(new(2, [package], []), [package.Id], descriptor, TestContext.Current.CancellationToken));
        Assert.Equal(fingerprint, resources.Localization.Fingerprint);
        Assert.Equal("Translated", resources.Localize("$en_sample_0000", "Original"));
        Assert.Equal((byte)50, Assert.Single(resources.Localization.Artwork).Value.Pixels[0].R);
    }

    [Fact]
    public void FlatteningKeepsImageTargetsInPriorityOrderAndRetainsIndependentSourceFiles()
    {
        LocalizationProject basis = Project();
        LocalizationProject patch = Project(99);
        LocalizationProject empty = Project() with { Artwork = [Project().Artwork[0] with { TargetPng = null }] };
        LocalizationProject combined = LocalizationProjectExport.Resolve([basis, patch, empty], "composite");
        LocalizationArtworkFile file = Assert.Single(combined.Artwork);
        Assert.Equal(basis.Artwork[0].SourcePng, file.SourcePng);
        Assert.Equal(patch.Artwork[0].TargetPng, file.TargetPng);
        Assert.NotEqual(file.Declaration.SourceImage, file.Declaration.TargetImage);
        Assert.Equal(file.Declaration, Assert.Single(combined.Manifest.Artwork));
        Assert.NotSame(patch.Artwork[0].TargetPng, file.TargetPng);
        Assert.Equal("Translated", Assert.Single(combined.Entries).Target);
    }

    [Fact]
    public void SourceRefreshKeepsTranslatedBitmapAndObsoleteImageWork()
    {
        LocalizationProject existing = Project();
        LocalizationArtworkFile old = existing.Artwork[0];
        LocalizationArtworkDeclaration declaration = old.Declaration with { SourceSha256 = new string('B', 64) };
        LocalizationProject refreshed = Project() with { Manifest = existing.Manifest with { Artwork = [declaration] }, Artwork = [new(declaration, Png(2, 1, 70))] };
        LocalizationProject merged = LocalizationProjectExport.Reconcile(existing, refreshed).Project;
        LocalizationArtworkFile current = Assert.Single(merged.Artwork);
        Assert.Equal(refreshed.Artwork[0].SourcePng, current.SourcePng);
        Assert.Equal(old.TargetPng, current.TargetPng);
        Assert.Equal(declaration.SourceSha256, current.Declaration.SourceSha256);
        LocalizationProject noImages = refreshed with { Manifest = refreshed.Manifest with { Artwork = [] }, Artwork = [] };
        LocalizationArtworkFile obsolete = Assert.Single(LocalizationProjectExport.Reconcile(existing, noImages).Project.Artwork);
        Assert.Equal(old.SourcePng, obsolete.SourcePng);
        Assert.Equal(old.TargetPng, obsolete.TargetPng);
    }

    private ModPackage Package(string name, LocalizationProject project)
    {
        string path = Path.Combine(root, name);
        LocalizationProjectIO.WriteNew(project, path, "json", "single", TestContext.Current.CancellationToken);
        return new("Memoria/" + name, name, ModFormat.Memoria, true, true, ModEffectiveStatus.Active, path) { HasLocalization = true, LocalizationOnly = true };
    }

    private static LocalizationProject Project(byte translatedRed = 50)
    {
        LocalizationArtworkDeclaration declaration = new() { Owner = "menu/mag00.tex", Subresource = "atlas", SourceSha256 = new string('A', 64), Width = 2, Height = 1, SourceImage = "Localization/Artwork/source.png", TargetImage = "Localization/Artwork/target.png" };
        return new() { Manifest = new() { TargetLanguage = "translated", MappingVersion = FieldCorrespondenceCatalog.Version, CompatibleVariants = ["Classic:en"], Artwork = [declaration] }, Entries = [new() { Category = "Menus", File = "sample", Key = "$en_sample_0000", Source = "Original", Target = "Translated" }], Artwork = [new(declaration, Png(2, 1, 10), Png(4, 2, translatedRed))] };
    }

    private static byte[] Png(int width, int height, byte red) => LocalizationArtworkExporter.EncodePng(new PixelImage(width, height, Enumerable.Repeat(new Rgba32(red, 20, 30, 255), width * height).ToArray()));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
