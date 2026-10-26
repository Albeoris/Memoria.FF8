using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class FfnxPaletteNamingTests
{
    private static readonly ResourceKey Owner = new("battle/a0stg000.x");

    [Theory]
    [InlineData("001")]
    [InlineData("00l")]
    [InlineData("1")]
    [InlineData("0100")]
    [InlineData("256")]
    public void NeverGeneratedPaletteNamesAreUnusedAndCannotBeFallbacks(string suffix)
    {
        using InMemoryResourceSource source = NativeSource();
        ModFile artifact = new("artifact", ModFormat.Junction, $"mods/textures/{Owner.Path}-0_{suffix}.png", 1, () => throw new InvalidOperationException("Never-generated names must not be decoded."));
        ModCatalogSnapshot catalog = new(1, [], [artifact]);

        DemasterInspection inspection = FfnxTextureResolver.Inspect(source.Descriptor, catalog, source);

        Assert.Equal("ffnx-unused-native-target", Assert.Single(inspection.Diagnostics).Code);
        Assert.Empty(inspection.UsablePackageIds);
        Assert.Null(new FfnxTextureResolver(catalog).Resolve(Context(0)));
    }

    [Theory]
    [InlineData("01", 1)]
    [InlineData("100", 100)]
    [InlineData("255", 255)]
    public void CanonicalBytePaletteNamesResolveAndRemainAvailableAsVariants(string suffix, int palette)
    {
        using InMemoryResourceSource source = NativeSource();
        ModFile file = Sheet("selected", suffix, new(12, 34, 56, 255));
        ModCatalogSnapshot catalog = new(1, [], [file]);
        Assert.Empty(FfnxTextureResolver.Inspect(source.Descriptor, catalog, source).Diagnostics);

        TextureReplacement direct = Assert.IsType<TextureReplacement>(new FfnxTextureResolver(catalog).Resolve(Context(palette)));
        TextureReplacement fallback = Assert.IsType<TextureReplacement>(new FfnxTextureResolver(catalog).Resolve(Context(0)));

        Assert.Equal(new Rgba32(12, 34, 56, 255), direct.Image[0, 0]);
        Assert.Equal(direct.Provenance, fallback.Provenance);
        Assert.Contains($"palette/{palette}", direct.Variants!.Keys);
    }

    [Fact]
    public void NoncanonicalArtifactDoesNotHideCanonicalPaletteFallback()
    {
        ModFile artifact = new("artifact", ModFormat.Junction, $"mods/textures/{Owner.Path}-0_001.png", 1, () => throw new InvalidOperationException("Artifact must remain unused."));
        ModFile canonical = Sheet("canonical", "01", new(50, 60, 70, 255));

        TextureReplacement result = Assert.IsType<TextureReplacement>(new FfnxTextureResolver(new(1, [], [artifact, canonical])).Resolve(Context(0)));

        Assert.Contains("canonical/", result.Provenance);
        Assert.Equal(new Rgba32(50, 60, 70, 255), result.Image[0, 0]);
        Assert.Single(result.Variants!);
    }

    [Fact]
    public void CanonicalHighPaletteStillChecksOriginalMemberCount()
    {
        using InMemoryResourceSource source = NativeSource();
        ModFile file = new("absent-member", ModFormat.Junction, $"mods/textures/{Owner.Path}-1_100.png", 1, () => throw new InvalidOperationException());

        DemasterInspection inspection = FfnxTextureResolver.Inspect(source.Descriptor, new(1, [], [file]), source);

        Assert.Equal("ffnx-unused-native-target", Assert.Single(inspection.Diagnostics).Code);
        Assert.Empty(inspection.UsablePackageIds);
    }

    [Fact]
    public void UnknownBattleGrammarIsNotClassifiedAsANeverGeneratedPalette()
    {
        using InMemoryResourceSource source = NativeSource();
        ResourceKey unknown = new("battle/unknown.bin");
        source.Set(unknown, [0], "bin");
        ModFile file = new("unknown", ModFormat.Junction, $"mods/textures/{unknown.Path}-0_00l.png", 1, () => throw new InvalidOperationException());

        DemasterInspection inspection = FfnxTextureResolver.Inspect(source.Descriptor, new(1, [], [file]), source);

        Assert.Equal("ffnx-unsupported-target", Assert.Single(inspection.Diagnostics).Code);
    }

    [Fact]
    public void CanonicalDuplicateSelectionPreservesFirstWinnerAndRejectsCorruptWinner()
    {
        ModFile winner = Sheet("first", "01", new(10, 20, 30, 255));
        ModFile shadowed = new("second", ModFormat.Junction, winner.RelativePath, 2, () => throw new InvalidOperationException("Shadowed candidate must not be decoded."));
        TextureReplacement result = Assert.IsType<TextureReplacement>(new FfnxTextureResolver(new(1, [], [winner, shadowed])).Resolve(Context(1)));
        Assert.Contains("first/", result.Provenance);

        ModFile corrupt = new("corrupt", ModFormat.Junction, winner.RelativePath, 3, () => new byte[] { 1, 2, 3 });
        Assert.Throws<ModTextureLoadException>(() => new FfnxTextureResolver(new(2, [], [corrupt, winner])).Resolve(Context(1)));
    }

    private static InMemoryResourceSource NativeSource()
    {
        InMemoryResourceSource source = new(Source());
        source.Set(Owner, [0], "x");
        return source;
    }

    private static ResourceSourceDescriptor Source() => new("test", GameDataEdition.Classic, ["classic-ffnx"], "en");

    private static TextureBuildContext Context(int palette) => new(new(Owner, "texture/0", palette), Source(), 1, "classic-battle", 64, 32, () => new(64, 32));

    private static ModFile Sheet(string package, string suffix, Rgba32 color)
    {
        using MagickImage image = new(new MagickColor(color.R, color.G, color.B, color.A), 128, 64);
        byte[] bytes = image.ToByteArray(MagickFormat.Png);
        return new(package, ModFormat.Junction, $"mods/textures/{Owner.Path}-0_{suffix}.png", 1, () => bytes);
    }
}
