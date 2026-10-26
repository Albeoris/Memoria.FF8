using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationArtworkReadCacheTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.Localization.Artwork.Cache.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Repeated222SourcesReuseValidationAndSameMetadataTargetEditDecodesOnlyOneImage()
    {
        ModPackage package = Write(222);
        LocalizationProjectReadCache cache = new();
        LocalizationPreparation first = Prepare(package, cache);
        Assert.Equal(223, cache.DecodedArtworkImages);
        LocalizationPreparation second = Prepare(package, cache);
        Assert.Equal(223, cache.DecodedArtworkImages);
        Assert.True(cache.ArtworkCacheHits >= 224);
        TextureKey key = Assert.Single(first.Catalog.Artwork).Key;
        Assert.True(first.Catalog.Artwork[key].Pixels == second.Catalog.Artwork[key].Pixels);
        string target = Path.Combine(package.RootDirectory, "Localization/Artwork/target.png");
        byte[] original = File.ReadAllBytes(target);
        DateTime modified = File.GetLastWriteTimeUtc(target);
        byte[] changed = Enumerable.Range(222, 28).Select(value => Png(2, 1, (byte)value)).First(bytes => bytes.Length == original.Length && !bytes.SequenceEqual(original));
        File.WriteAllBytes(target, changed);
        File.SetLastWriteTimeUtc(target, modified);
        Assert.Equal(original.Length, new FileInfo(target).Length);
        Assert.Equal(modified, File.GetLastWriteTimeUtc(target));
        LocalizationPreparation edited = Prepare(package, cache);
        Assert.Equal(224, cache.DecodedArtworkImages);
        Assert.NotEqual(first.Catalog.Fingerprint, edited.Catalog.Fingerprint);
        Assert.Equal((byte)250, first.Catalog.Artwork[key].Pixels[0].R);
        Assert.NotEqual((byte)250, edited.Catalog.Artwork[key].Pixels[0].R);
        Assert.InRange(cache.CachedBytes, 1, cache.MaximumBytes);
    }

    [Fact]
    public void ChangedSourceWithValidHeaderCannotReusePriorSuccessfulValidation()
    {
        ModPackage package = Write(1);
        LocalizationProjectReadCache cache = new();
        Prepare(package, cache);
        string source = Path.Combine(package.RootDirectory, "Localization/Artwork/source000.png");
        byte[] corrupted = File.ReadAllBytes(source);
        DateTime modified = File.GetLastWriteTimeUtc(source);
        Array.Fill(corrupted, (byte)0, 24, corrupted.Length - 24);
        File.WriteAllBytes(source, corrupted);
        File.SetLastWriteTimeUtc(source, modified);
        Assert.Throws<InvalidDataException>(() => Prepare(package, cache));
    }

    [Fact]
    public void CacheBudgetIncludesRgbaAndOversizedImagesAreNotRetained()
    {
        ModPackage package = Write(1, targetWidth: 32, targetHeight: 16);
        LocalizationProjectReadCache cache = new(2048);
        LocalizationPreparation first = Prepare(package, cache);
        long decodes = cache.DecodedArtworkImages;
        LocalizationPreparation second = Prepare(package, cache);
        Assert.True(cache.DecodedArtworkImages > decodes);
        TextureKey key = Assert.Single(first.Catalog.Artwork).Key;
        Assert.False(first.Catalog.Artwork[key].Pixels == second.Catalog.Artwork[key].Pixels);
        Assert.InRange(cache.CachedBytes, 0, cache.MaximumBytes);
        Assert.Equal(2048, first.Catalog.Artwork[key].Pixels.Length * 4);
    }

    private LocalizationPreparation Prepare(ModPackage package, LocalizationProjectReadCache cache) => LocalizationRuntime.Prepare(new(1, [package], []), [package.Id], new("synthetic", GameDataEdition.Classic, [], "en"), TestContext.Current.CancellationToken, readCache: cache);

    private ModPackage Write(int count, int targetWidth = 2, int targetHeight = 1)
    {
        List<LocalizationArtworkFile> files = [];
        for (int index = 0; index < count; index++)
        {
            LocalizationArtworkDeclaration declaration = new() { Owner = "menu/mngrp.bin", Subresource = $"slot{index:000}", SourceSha256 = new string('A', 64), Width = 2, Height = 1, SourceImage = $"Localization/Artwork/source{index:000}.png", TargetImage = index == 0 ? "Localization/Artwork/target.png" : null };
            files.Add(new(declaration, Png(2, 1, (byte)index), index == 0 ? Png(targetWidth, targetHeight, 250) : null));
        }
        LocalizationProject project = new() { Manifest = new() { TargetLanguage = "fixture", MappingVersion = FieldCorrespondenceCatalog.Version, CompatibleVariants = ["Classic:en"], Artwork = files.Select(file => file.Declaration).ToArray() }, Artwork = files, Entries = [new() { Category = "Menus", File = "sample", Key = "$en_sample_0000", Source = "Original", Target = "Translated" }] };
        LocalizationProjectIO.WriteNew(project, root, "json", "single", TestContext.Current.CancellationToken);
        return new("Memoria/fixture", "fixture", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, root) { HasLocalization = true, LocalizationOnly = true };
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
