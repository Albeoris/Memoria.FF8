using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterBattleModelSheetTests
{
    private static readonly ResourceSourceDescriptor Source = new("synthetic", GameDataEdition.Classic, ["native-battle-model-sheet", "native-battle-body-sheet"]);

    [Theory]
    [InlineData(GameDataEdition.Classic, false)]
    [InlineData(GameDataEdition.Remastered, true)]
    public void BodySheetsRequireTheirAdditionalSourceCapabilityWhileWeaponsAndMonstersRemainCompatible(GameDataEdition edition, bool bodyCompatible)
    {
        ResourceSourceDescriptor source = new("profile", edition, bodyCompatible ? ["native-battle-model-sheet", "native-battle-body-sheet"] : ["native-battle-model-sheet"]);
        foreach (string name in new[] { "d0c000", "d0w000", "c0m001" })
        {
            bool supported = name != "d0c000" || bodyCompatible;
            int height = name == "d0w000" ? 32 : 128;
            TextureBuildContext context = Context(name, 0, height, height, source: source);
            ModFile file = supported ? Image(Path(name, 0), 384, height * 3) : new("Demaster/Synthetic", ModFormat.Demaster, Path(name, 0), 1, () => throw new InvalidOperationException("Unsupported body sheets stay lazy."));
            DemasterTextureResolver resolver = Resolver(file);
            Assert.Equal(supported, resolver.Resolve(context) is not null);
            Assert.Equal(supported, DemasterTextureResolver.CandidatePaths(context).Count != 0);
            DemasterInspection inspection = DemasterTextureResolver.Inspect(source, new(1, [], [file]));
            Assert.Equal(supported, inspection.UsablePackageIds.Count != 0);
            if (!supported)
            {
                Assert.Equal("demaster-unsupported-target", Assert.Single(inspection.Diagnostics).Code);
            }
            else
            {
                Assert.Empty(inspection.Diagnostics);
            }
        }
    }

    [Theory]
    [InlineData("d0c000", 1, 64, 128)]
    [InlineData("d0c000", 0, 128, 128)]
    [InlineData("d0w000", 0, 32, 32)]
    [InlineData("c0m001", 5, 128, 128)]
    public void SourceSheetRowsRetainNativeMaterialCanvasAndCropPartialPlanes(string name, int row, int height, int canvasHeight)
    {
        TextureBuildContext context = Context(name, row, height, canvasHeight);
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(Path(name, row), 384, canvasHeight * 3)).Resolve(context));
        Assert.Equal((128, height), (replacement.LogicalWidth, replacement.LogicalHeight));
        Assert.Equal((384, height * 3), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Pixel(0, 0), replacement.Image[0, 0]);
        Assert.Equal(Pixel(383, height * 3 - 1), replacement.Image[383, height * 3 - 1]);
        Assert.Null(replacement.Animation);
        Assert.Equal(new[] { Path(name, row).Replace(".png", ".dds"), Path(name, row) }, DemasterTextureResolver.CandidatePaths(context));
        Assert.Null(Resolver(Image(Path(name, row == 0 ? 1 : 0))).Resolve(context));
    }

    [Fact]
    public void PublishedSheetCropUsesItsOwnCoordinatesInsteadOfTheFfnxAlias()
    {
        TextureBuildContext context = new(new(new("battle/c0m001.dat"), "texture/7"), Source, 1, "classic-battle", 32, 64, () => throw new InvalidOperationException("Native image stays lazy."))
        {
            NativeSheet = new(new("battle/c0m001.dat"), "row/4", 0, 16, 32, 32, 64, 128, 128),
            NativeAlias = new(new("battle/c0m001.dat"), "texture/2", 0, 0, 0, 32, 64, 32, 64)
        };
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(Path("c0m001", 4))).Resolve(context));
        Assert.Equal((96, 192), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Pixel(48, 96), replacement.Image[0, 0]);
        Assert.Equal(Pixel(143, 287), replacement.Image[95, 191]);
    }

    [Theory]
    [InlineData(0, 128, 96)]
    [InlineData(1, 128, 96)]
    [InlineData(0, 96, 96)]
    [InlineData(1, 128, 128)]
    public void PairedBodyRowsDeterminePlaneScaleAndKeepClosedPixelsSeparate(int wideRow, int nativeHeight, int uploadedHeight)
    {
        const string name = "d0c000";
        TextureBuildContext context = Context(name, wideRow, nativeHeight);
        DemasterTextureResolver resolver = Resolver(Image(Path(name, wideRow), 768, uploadedHeight * 3), Image(Path(name, 1 - wideRow)));
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(context));
        PixelImage closed = Assert.IsAssignableFrom<IReadOnlyDictionary<string, PixelImage>>(replacement.Variants)["eyes-closed"];
        Assert.Equal((384, nativeHeight * 3), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal((replacement.Image.Width, replacement.Image.Height), (closed.Width, closed.Height));
        for (int y = 0; y < replacement.Image.Height; y++)
        {
            Assert.Equal(y < uploadedHeight * 3 ? Pixel(17, y) : default, replacement.Image[17, y]);
            Assert.Equal(y < uploadedHeight * 3 ? Pixel(401, y) : default, closed[17, y]);
        }
        Assert.Null(replacement.Animation);
        TextureReplacement opposite = Assert.IsType<TextureReplacement>(resolver.Resolve(Context(name, 1 - wideRow)));
        Assert.Null(opposite.Variants);
        Assert.Equal((384, 384), (opposite.Image.Width, opposite.Image.Height));
    }

    [Theory]
    [InlineData(700, 288)]
    [InlineData(768, 385)]
    [InlineData(1152, 288)]
    public void BodyUploadsMustFitOneOrTwoPlanesAtTheirCompleteCompanionScale(int width, int height)
    {
        ModTextureLoadException error = Assert.Throws<ModTextureLoadException>(() => Resolver(Image(Path("d0c000", 0)), Image(Path("d0c000", 1), width, height)).Resolve(Context("d0c000", 1)));
        Assert.Equal(Path("d0c000", 1), error.RelativePath);
    }

    [Theory]
    [InlineData("d0w000", 1, true)]
    [InlineData("d0c000", 2, true)]
    [InlineData("c0m001", 6, true)]
    [InlineData("a0stg001", 0, true)]
    [InlineData("d0w000", 0, false)]
    public void UnknownOwnersRowsAndUnpublishedLayoutsNeverOpenImages(string name, int row, bool published)
    {
        ModFile file = new("Demaster/Synthetic", ModFormat.Demaster, Path(name, row), 1, () => throw new InvalidOperationException("Unsupported addresses stay lazy."));
        Assert.Null(Resolver(file).Resolve(Context(name, row, published: published)));
    }

    [Theory]
    [InlineData(128, 31)]
    [InlineData(256, 32)]
    [InlineData(192, 48)]
    public void IncompatibleImageScaleReportsPackageAndFile(int width, int height)
    {
        ModTextureLoadException error = Assert.Throws<ModTextureLoadException>(() => Resolver(Image(Path("d0w000", 0), width, height)).Resolve(Context("d0w000", 0, 32, 32)));
        Assert.Equal("Demaster/Synthetic", error.PackageId);
        Assert.Equal(Path("d0w000", 0), error.RelativePath);
    }

    [Fact]
    public void CorruptPreferredDdsKeepsOriginalWithoutPngFallbackWhileValidMaterialsPublish()
    {
        TextureBuildContext context = Context("d0w000", 0, 32, 32);
        using InMemoryResourceSource source = new(Source);
        ResourceService resources = new(source);
        AssetHandle<TextureValue> texture = resources.GetTexture(context);
        AssetHandle<TextureValue> valid = resources.GetTexture(Context("d1w000", 0, 32, 32));
        TextureValue original = texture.Value;
        ModFile broken = new("Demaster/Broken", ModFormat.Demaster, Path("d0w000", 0).Replace(".png", ".dds"), 1, () => [0]);
        DemasterTextureResolver resolver = Resolver(broken, Image(Path("d0w000", 0), 768, 192), Image(Path("d1w000", 0), 768, 192));
        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(context));
        Assert.Equal("Demaster/Broken", failure.PackageId);
        Assert.Equal(broken.RelativePath, failure.RelativePath);
        List<(TextureKey Key, Exception Exception)> reports = [];
        resources.ReplacementFailed += (key, exception) => reports.Add((key, exception));
        ResourceService.PreparedResources prepared = resources.Prepare(resolver);
        Assert.Same(original, texture.Value);
        Assert.Empty(reports);
        prepared.Commit(notify: false);
        Assert.False(texture.Value.IsReplacement);
        Assert.Same(original.Image, texture.Value.Image);
        Assert.True(valid.Value.IsReplacement);
        Assert.Equal(1, resources.Generation);
        Assert.Empty(reports);
        prepared.Notify();
        var report = Assert.Single(reports);
        Assert.Equal(context.Key, report.Key);
        ModTextureLoadException reported = Assert.IsType<ModTextureLoadException>(report.Exception);
        Assert.Equal(failure.PackageId, reported.PackageId);
        Assert.Equal(failure.RelativePath, reported.RelativePath);
        resources.Prepare(Resolver(Image(Path("d0w000", 0), 768, 192))).Commit();
        Assert.Equal((768, 192, 128, 32), (texture.Value.Image.Width, texture.Value.Image.Height, texture.Value.LogicalWidth, texture.Value.LogicalHeight));
        resources.Prepare(null).Commit();
        Assert.False(texture.Value.IsReplacement);
        Assert.Equal((128, 32), (texture.Value.Image.Width, texture.Value.Image.Height));
        Assert.Single(reports);
    }

    [Fact]
    public void InspectionRecognizesOnlyCanonicalRowsOfPublishedModelFamilies()
    {
        string[] valid = [Path("d0w000", 0), Path("d9c001", 1), Path("c0m099", 5)];
        string[] invalid = [Path("d0w000", 1), Path("d0c000", 2), Path("c0m001", 6), Path("d0w000", 0).Replace("_0.png", "_00.png")];
        ModCatalogSnapshot catalog = new(1, [], valid.Concat(invalid).Select(path => new ModFile("Demaster/Synthetic", ModFormat.Demaster, path, 1, () => throw new InvalidOperationException("Inspection stays lazy."))));
        DemasterInspection inspection = DemasterTextureResolver.Inspect(Source, catalog);
        Assert.Single(inspection.UsablePackageIds);
        Assert.Equal(invalid.Order(), inspection.Diagnostics.Select(item => item.RelativePath).Order());
        Assert.Equal(valid.Length + invalid.Length, DemasterTextureResolver.Inspect(new("unpublished", GameDataEdition.Classic, []), catalog).Diagnostics.Count);
    }

    private static TextureBuildContext Context(string name, int row, int height = 128, int canvasHeight = 128, bool published = true, ResourceSourceDescriptor? source = null)
    {
        ResourceKey owner = new($"battle/{name}.dat");
        return new(new(owner, "texture/7"), published ? source ?? Source : new("unpublished", GameDataEdition.Classic, []), 1, "classic-battle", 128, height, () => new(128, height))
        {
            NativeSheet = new(owner, $"row/{row}", 0, 0, 0, 128, height, 128, canvasHeight)
        };
    }

    private static DemasterTextureResolver Resolver(params ModFile[] files) => new(new(1, [], files));
    private static string Path(string name, int row) => $"textures/battle.fs/hd_new/{name}_{row}.png";
    private static Rgba32 Pixel(int x, int y) => new((byte)(x % 251), (byte)(y % 251), 80, 255);

    private static ModFile Image(string path, int width = 384, int height = 384)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Rgba32 color = Pixel(x, y);
                int offset = (y * width + x) * 4;
                pixels[offset] = color.R;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.B;
                pixels[offset + 3] = color.A;
            }
        }
        using MagickImage image = new(pixels, new PixelReadSettings((uint)width, (uint)height, StorageType.Char, PixelMapping.RGBA));
        byte[] encoded = image.ToByteArray(MagickFormat.Png);
        return new("Demaster/Synthetic", ModFormat.Demaster, path, 1, () => encoded);
    }
}
