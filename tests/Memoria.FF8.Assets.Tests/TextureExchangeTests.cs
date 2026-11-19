using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Albeoris.Games.FF8.Imaging.Tim;

namespace Memoria.FF8.Assets.Tests;

public sealed class TextureExchangeTests
{
    [Fact]
    public void OriginalExportKeepsInstalledProviderHdPixelsAndOrdinaryAlpha()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-installed-original-" + Guid.NewGuid().ToString("N"));
        try
        {
            PixelImage installed = new(3, 3, Enumerable.Repeat(new Rgba32(70, 80, 90, 128), 9).ToArray());
            using InstalledImageSource source = new(installed);
            ResourceService resources = new(source);
            TextureKey key = new(new("field/example/chara.one"), "model/0/texture/0");
            resources.GetTexture(new(key, source.Descriptor, 1, "classic-field-model", 1, 1, () => new(1, 1, [new(1, 2, 3, 128)]), new(BitsPerPixel: 4)));
            TextureSourceSnapshot snapshot = Assert.Single(resources.CaptureTextureSources([key]));
            Assert.Same(installed, snapshot.Original);
            string path = Assert.Single(TextureExchange.Export([snapshot], root, cancellationToken: TestContext.Current.CancellationToken).Files);
            PixelImage png = TextureImageDecoder.Decode(File.ReadAllBytes(path), path).Image;
            Assert.Equal(3, png.Width);
            Assert.All(png.Pixels, pixel => Assert.Equal((byte)128, pixel.A));
            Assert.Null(TextureExchange.ReadMetadata(path)!.NativeFlagsBase64);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void EffectSourceExportUsesNativePcPaletteColorsWithEditorCoverage()
    {
        ushort[] colors = new ushort[16];
        colors[1] = 0x8000;
        colors[2] = 0x001F;
        colors[3] = 0x0401;
        TimImage tim = new(TimPixelMode.Indexed4, 0, 0, 1, 1, [0, 1, 2, 3], new(0, 0, 16, 1, colors));
        PixelImage image = GameAssets.DecodeEffectTexture(tim);
        Assert.Equal(new Rgba32(0, 0, 1, 0), image[0, 0]);
        Assert.Equal(new Rgba32(0, 0, 8, 255), image[1, 0]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), image[2, 0]);
        Assert.Equal(new Rgba32(8, 0, 8, 255), image[3, 0]);
    }

    [Fact]
    public void ExportPreservesIdentityAndNativeCoverageThroughMetadataStripping()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-texture-exchange-" + Guid.NewGuid().ToString("N"));
        try
        {
            PixelImage original = new(2, 1, [new(255, 0, 0, 128), new(0, 255, 0, 0)]);
            TextureBuildContext context = new(new(new("battle/example.dat"), "texture/0"), new("synthetic", GameDataEdition.Classic, []), 1, "classic-battle", 2, 1, () => original);
            TextureSourceSnapshot snapshot = new(context, new(original, 2, 1, "synthetic"), 0);
            TextureExportResult exported = TextureExchange.Export([snapshot, snapshot], root, cancellationToken: TestContext.Current.CancellationToken);
            string path = Assert.Single(exported.Files);
            Assert.Equal(1, exported.Written);
            byte[] png = File.ReadAllBytes(path);
            TextureExchangeMetadata embedded = Assert.IsType<TextureExchangeMetadata>(TextureExchange.TryReadEmbeddedMetadata(png));
            Assert.Equal(TextureTarget.FromContext(context), embedded.Target);
            PixelImage editable = TextureImageDecoder.Decode(png, path).Image;
            Assert.Equal((byte)255, editable[0, 0].A);
            Assert.Equal((byte)0, editable[1, 0].A);
            PixelImage restored = TexturePixelExchange.RestoreNativeFlags(editable, 2, 1, embedded.NativeFlagsBase64!);
            Assert.Equal(original.Pixels, restored.Pixels);
            File.WriteAllBytes(path, Localization.LocalizationArtworkExporter.EncodePng(editable));
            Assert.Null(TextureExchange.TryReadEmbeddedMetadata(File.ReadAllBytes(path)));
            Assert.Equal(embedded.Target, TextureExchange.ReadMetadata(path)!.Target);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void MetadataConflictDoesNotSilentlyChooseATarget()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-texture-conflict-" + Guid.NewGuid().ToString("N"));
        try
        {
            PixelImage original = new(1, 1, [new(10, 20, 30, 255)]);
            TextureBuildContext context = new(new(new("battle/example.dat"), "texture/0"), new("synthetic", GameDataEdition.Classic, []), 1, "classic-battle", 1, 1, () => original);
            string path = Assert.Single(TextureExchange.Export([new TextureSourceSnapshot(context, new(original, 1, 1, "synthetic"), 0)], root, cancellationToken: TestContext.Current.CancellationToken).Files);
            TextureExchangeMetadata metadata = TextureExchange.ReadMetadata(path)!;
            File.WriteAllBytes(path + ".texture.json", (metadata with { Target = metadata.Target with { Subresource = "texture/1" } }).ToBytes());
            Assert.Throws<InvalidDataException>(() => TextureExchange.ReadMetadata(path));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void CatalogSearchUsesFriendlyNamesAndTechnicalOwners()
    {
        TextureCatalogEntry entry = new(new("ff8", "battle/d0w001.dat", "texture/0"), "Squall Revolver", "Battle weapon", "classic-battle", 128, 128);
        Assert.True(entry.Matches("squall texture/0"));
        Assert.True(entry.Matches("D0W001"));
        Assert.False(entry.Matches("rinoa"));
    }

    [Fact]
    public void InstalledCatalogDiscoveryDoesNotGrowResidency()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An optional installed game directory is required.");
        using GameAssets assets = new(directory);
        int residency = assets.Resources.ResidentTextureCount;
        TextureCatalogEntry[] entries = assets.EnumerateTextureCatalog(TestContext.Current.CancellationToken).Take(8).ToArray();
        Assert.NotEmpty(entries);
        Assert.Equal(residency, assets.Resources.ResidentTextureCount);
        TextureBuildContext source = assets.LoadCatalogTexture(entries[0]);
        Assert.Equal(entries[0].Target, TextureTarget.FromContext(source));
        Assert.Equal(residency, assets.Resources.ResidentTextureCount);
    }

    private sealed class InstalledImageSource(PixelImage image) : IResourceSource, ITextureReplacementResolver
    {
        public ResourceSourceDescriptor Descriptor { get; } = new("synthetic-hd", GameDataEdition.Remastered, []);
        public ResourceContent? Find(ResourceKey key) => null;
        public TextureReplacement? Resolve(TextureBuildContext context) => new(image, "synthetic-installed-image", context.LogicalWidth, context.LogicalHeight);
        public void Dispose()
        {
        }
    }
}
