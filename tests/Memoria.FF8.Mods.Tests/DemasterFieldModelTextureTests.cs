using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterFieldModelTextureTests
{
    private static readonly ResourceSourceDescriptor Source = new("synthetic", GameDataEdition.Classic, ["native-field-model-sheet"]);

    [Fact]
    public void LibraryMaterialsRetainTheirActualEncodedOwnerWithoutAFieldPlacement()
    {
        TextureBuildContext placed = Context("d123", 4);
        TextureBuildContext library = new(new(new("field/main_chr/d123.mch"), "texture/4"), Source, 0, placed.Layout, 128, 128, () => new(128, 128)) { NativeSheet = placed.NativeSheet };
        DemasterTextureResolver resolver = Resolver(Image(Path("d123", 4)));
        Assert.IsType<TextureReplacement>(resolver.Resolve(library));
        TextureBuildContext differentOwner = new(new(new("field/main_chr/d124.mch"), "texture/4"), Source, 0, placed.Layout, 128, 128, () => new(128, 128)) { NativeSheet = placed.NativeSheet };
        Assert.Null(resolver.Resolve(differentOwner));
    }

    [Theory]
    [InlineData("main_chr")]
    [InlineData("second_chr")]
    public void PublishedNativeSourceOrdinalAndFamilyDriveLookupIndependentlyOfInstanceIndexes(string kind)
    {
        TextureBuildContext context = Context("c021", 7, kind: kind);
        string path = Path("n021", 7);
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(path)).Resolve(context));
        Assert.Equal((384, 384, 128, 128), (replacement.Image.Width, replacement.Image.Height, replacement.LogicalWidth, replacement.LogicalHeight));
        Assert.Equal(Pixel(17, 31), replacement.Image[17, 31]);
        Assert.Equal(new[] { path.Replace(".png", ".dds"), path, path.Replace("field_hd_new", "field_hd").Replace(".png", ".dds"), path.Replace("field_hd_new", "field_hd") }, DemasterTextureResolver.CandidatePaths(context));
        Assert.Null(Resolver(Image(Path("n021", 0))).Resolve(context));
    }

    [Fact]
    public void NativeNewRootPrecedesBaseRootAndAlphaRequiresPublishedMetadata()
    {
        string path = Path("o006", 0, "_alpha");
        DemasterTextureResolver resolver = Resolver(Image(path), Image(path.Replace("field_hd_new", "field_hd"), offset: 19), Image(Path("o006", 0), offset: 31));
        TextureReplacement selected = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("o006", 0, variant: "alpha")));
        Assert.Equal(Pixel(0, 0), selected.Image[0, 0]);
        Assert.Contains(path, selected.Provenance);
        selected = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("o006", 0)));
        Assert.Equal(Pixel(31, 0), selected.Image[0, 0]);
        Assert.Null(Resolver(Image(path)).Resolve(Context("o006", 0)));
        selected = Assert.IsType<TextureReplacement>(Resolver(Image(path.Replace("field_hd_new", "field_hd"), offset: 19)).Resolve(Context("o006", 0, variant: "alpha")));
        Assert.Equal(Pixel(19, 0), selected.Image[0, 0]);
    }

    [Theory]
    [InlineData(384, false)]
    [InlineData(768, true)]
    public void CompleteSheetsDistinguishOneNormalPlaneFromASeparateClosedEyePlane(int width, bool hasClosed)
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(Path("p123", 4), width)).Resolve(Context("p123", 4)));
        Assert.Equal((384, 384), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Pixel(383, 117), replacement.Image[383, 117]);
        if (hasClosed)
        {
            PixelImage closed = Assert.IsAssignableFrom<IReadOnlyDictionary<string, PixelImage>>(replacement.Variants)["eyes-closed"];
            Assert.Equal(Pixel(767, 117), closed[383, 117]);
        }
        else
        {
            Assert.Null(replacement.Variants);
        }
        Assert.Null(replacement.Animation);
    }

    [Theory]
    [InlineData(384, false)]
    [InlineData(768, true)]
    public void PartialUploadsUseCompleteSameOwnerMaterialAnchorsAndCompleteUnuploadedRowsTransparently(int width, bool hasClosed)
    {
        DemasterTextureResolver resolver = Resolver(Image(Path("d123", 3), width, 288), Image(Path("d123", 8), 768, 384));
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("d123", 3)));
        Assert.Equal((384, 384), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Pixel(17, 287), replacement.Image[17, 287]);
        Assert.Equal(default, replacement.Image[17, 288]);
        if (hasClosed)
        {
            PixelImage closed = Assert.IsAssignableFrom<IReadOnlyDictionary<string, PixelImage>>(replacement.Variants)["eyes-closed"];
            Assert.Equal(Pixel(401, 287), closed[17, 287]);
            Assert.Equal(default, closed[17, 288]);
        }
        TextureReplacement crop = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("d123", 3, height: 96)));
        Assert.Equal((384, 288), (crop.Image.Width, crop.Image.Height));
    }

    [Fact]
    public void UnanchoredOrConflictingPartialSourcesFailWithTheirPackageAndPath()
    {
        ModFile partial = Image(Path("d123", 3), 768, 288);
        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => Resolver(partial, Image(Path("d124", 0))).Resolve(Context("d123", 3)));
        Assert.Equal(partial.PackageId, failure.PackageId);
        Assert.Equal(partial.RelativePath, failure.RelativePath);
        TextureReplacement selected = Assert.IsType<TextureReplacement>(Resolver(partial, Image(Path("d123", 0)), Image(Path("d123", 1), 512, 512)).Resolve(Context("d123", 3)));
        Assert.Equal((384, 384), (selected.Image.Width, selected.Image.Height));
        Assert.NotNull(selected.Variants);
        Assert.Throws<ModTextureLoadException>(() => Resolver(partial, Image(Path("d123", 0)), Image(Path("d123", 1), 768, 768)).Resolve(Context("d123", 3)));
    }

    [Fact]
    public void PublishedCropCoordinatesRetainTheMaterialCanvas()
    {
        TextureBuildContext context = new(new(new("field/source/chara.one"), "model/19/texture/3"), Source, 1, "classic-field-model", 64, 96, () => throw new InvalidOperationException("Native pixels remain lazy."))
        {
            NativeSheet = new(new("field/model/second_chr/o099"), "row/3", 0, 16, 16, 64, 96, 128, 128)
        };
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(Path("o099", 3))).Resolve(context));
        Assert.Equal((192, 288), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Pixel(48, 48), replacement.Image[0, 0]);
        Assert.Equal(Pixel(239, 335), replacement.Image[191, 287]);
    }

    [Fact]
    public void UnsupportedContextsDoNotOpenImages()
    {
        ModFile file = new("Demaster/Synthetic", ModFormat.Demaster, Path("d123", 0), 1, () => throw new InvalidOperationException("Unsupported source stays lazy."));
        TextureBuildContext context = Context("d123", 0, published: false);
        Assert.Null(Resolver(file).Resolve(context));
        TextureBuildContext missing = new(context.Key, Source, 1, "classic-field-model", 128, 128, () => throw new InvalidOperationException("Missing metadata stays lazy."));
        Assert.Null(Resolver(file).Resolve(missing));
        Assert.Null(Resolver(file).Resolve(Context("d123", 0, variant: "unknown")));
    }

    [Fact]
    public void CorruptPreferredDdsKeepsOriginalMaterialWhileValidMaterialsPublishAndReportsAfterNotification()
    {
        TextureBuildContext context = Context("d123", 0);
        using InMemoryResourceSource source = new(Source);
        ResourceService resources = new(source);
        AssetHandle<TextureValue> handle = resources.GetTexture(context);
        TextureBuildContext validContext = new(new(new("field/other/chara.one"), context.Key.Subresource), Source, 1, "classic-field-model", 128, 128, () => new(128, 128)) { NativeSheet = Context("d124", 0).NativeSheet };
        AssetHandle<TextureValue> valid = resources.GetTexture(validContext);
        TextureValue original = handle.Value;
        ModFile corrupt = new("Demaster/Broken", ModFormat.Demaster, Path("d123", 0).Replace(".png", ".dds"), 1, () => [0]);
        DemasterTextureResolver resolver = Resolver(corrupt, Image(Path("d123", 0)), Image(Path("d123", 0).Replace("field_hd_new", "field_hd")), Image(Path("d124", 0)));
        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(context));
        Assert.Equal(corrupt.PackageId, failure.PackageId);
        Assert.Equal(corrupt.RelativePath, failure.RelativePath);
        List<(TextureKey Key, Exception Exception)> reports = [];
        resources.ReplacementFailed += (key, exception) => reports.Add((key, exception));
        ResourceService.PreparedResources prepared = resources.Prepare(resolver);
        Assert.Same(original, handle.Value);
        Assert.Empty(reports);
        prepared.Commit(notify: false);
        Assert.False(handle.Value.IsReplacement);
        Assert.Same(original.Image, handle.Value.Image);
        Assert.True(valid.Value.IsReplacement);
        Assert.Equal(1, resources.Generation);
        Assert.Empty(reports);
        prepared.Notify();
        var report = Assert.Single(reports);
        Assert.Equal(context.Key, report.Key);
        ModTextureLoadException reported = Assert.IsType<ModTextureLoadException>(report.Exception);
        Assert.Equal(failure.PackageId, reported.PackageId);
        Assert.Equal(failure.RelativePath, reported.RelativePath);
        resources.Prepare(Resolver(Image(Path("d123", 0), 768))).Commit();
        Assert.True(handle.Value.IsReplacement);
        Assert.NotNull(handle.Value.Variants);
        resources.Prepare(null).Commit();
        Assert.False(handle.Value.IsReplacement);
        Assert.Equal((128, 128), (handle.Value.Image.Width, handle.Value.Image.Height));
        Assert.Single(reports);
    }

    [Fact]
    public void InspectionAcceptsOnlyCanonicalPublishedFieldModelTargetsWithoutReadingContent()
    {
        string[] valid = [Path("n123", 37), Path("o006", 0, "_alpha"), Path("p001", 2).Replace("field_hd_new", "field_hd")];
        string[] invalid = [Path("c123", 0), Path("d000", 0).Replace("_0.png", "_00.png"), Path("d000", 0, "_unknown")];
        ModCatalogSnapshot catalog = new(1, [], valid.Concat(invalid).Select(path => new ModFile("Demaster/Synthetic", ModFormat.Demaster, path, 1, () => throw new InvalidOperationException("Inspection stays lazy."))));
        DemasterInspection inspection = DemasterTextureResolver.Inspect(Source, catalog);
        Assert.Single(inspection.UsablePackageIds);
        Assert.Equal(invalid.Order(), inspection.Diagnostics.Select(item => item.RelativePath).Order());
        Assert.Equal(valid.Length + invalid.Length, DemasterTextureResolver.Inspect(new("unpublished", GameDataEdition.Remastered, ["demaster-direct"]), catalog).Diagnostics.Count);
    }

    private static TextureBuildContext Context(string name, int row, string kind = "main_chr", int height = 128, string? variant = null, bool published = true)
    {
        return new(new(new("field/source/chara.one"), "model/19/texture/9"), published ? Source : new("unpublished", GameDataEdition.Classic, []), 1, "classic-field-model", 128, height, () => new(128, height))
        {
            NativeSheet = new(new($"field/model/{kind}/{name}"), $"row/{row}" + (variant is null ? "" : "/" + variant), 0, 0, 0, 128, height, 128, 128)
        };
    }

    private static DemasterTextureResolver Resolver(params ModFile[] files) => new(new(1, [], files));
    private static string Path(string name, int row, string variant = "") => $"textures/field.fs/field_hd_new/{name}_{row}{variant}.png";
    private static Rgba32 Pixel(int x, int y) => new((byte)(x % 251), (byte)(y % 251), 77, 255);

    private static ModFile Image(string path, int width = 384, int height = 384, int offset = 0)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Rgba32 color = Pixel(x + offset, y);
                int index = (y * width + x) * 4;
                pixels[index] = color.R;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.B;
                pixels[index + 3] = color.A;
            }
        }
        using MagickImage image = new(pixels, new PixelReadSettings((uint)width, (uint)height, StorageType.Char, PixelMapping.RGBA));
        byte[] encoded = image.ToByteArray(MagickFormat.Png);
        return new("Demaster/Synthetic", ModFormat.Demaster, path, 1, () => encoded);
    }
}
