using System.Text;

namespace Memoria.FF8.Mods.Tests;

public sealed class ExternalMusicResolverTests
{
    [Theory]
    [InlineData("Data/Music/dmusic/056s-gargarde.sgt", "gargarde")]
    [InlineData("Data\\Music\\stream\\003-combat.wav", "combat")]
    [InlineData("099-joriku2.sgt", "joriku2")]
    [InlineData("hikutei.sgt", "hikutei")]
    public void NativeSelectedFilenamesMapWithoutNumericTrackTables(string path, string expected)
    {
        Assert.Equal(expected, ExternalMusicResolver.TrackName(path));
    }

    [Fact]
    public void NumericOffsetAndNoIntroReplacementAreIndependent()
    {
        ExternalMusicResolver resolver = Create("volume = 60\n[joriku2]\noffset_seconds = 51.8\n[gargarde]\nno_intro_track = 'gar3'\n[hikutei]\nintro_seconds = 20.5", "joriku2", "gargarde", "gar3", "hikutei");
        ExternalMusicSelection landing = Assert.IsType<ExternalMusicSelection>(resolver.Resolve("099-joriku2.sgt"));
        Assert.Equal(51.8, landing.OffsetSeconds);
        Assert.Equal(60, landing.Volume);
        Assert.Equal("music/gargarde.ogg", resolver.Resolve("056s-gargarde.sgt")!.File.RelativePath);
        Assert.Equal("music/gar3.ogg", resolver.Resolve("056s-gargarde.sgt", true)!.File.RelativePath);
        Assert.Equal(0, resolver.Resolve("hikutei.sgt")!.OffsetSeconds);
        Assert.Equal(20.5, resolver.Resolve("hikutei.sgt", true)!.OffsetSeconds);
    }

    [Theory]
    [InlineData("[battle]\nshuffle = ['battle2']")]
    [InlineData("[battle]\noffset_seconds = 'sync'")]
    [InlineData("[battle]\nrelative_speed = 2")]
    [InlineData("volume = 101")]
    [InlineData("[battle]\nintro_seconds = -1")]
    public void UnsupportedActiveSettingsFailExplicitly(string config)
    {
        Assert.Throws<NotSupportedException>(() => Create(config, "battle"));
    }

    [Fact]
    public void MissingConfiguredAlternateFailsRatherThanPlayingTheIntro()
    {
        ExternalMusicResolver resolver = Create("[gargarde]\nno_intro_track = 'gar3'", "gargarde");
        Assert.Throws<InvalidDataException>(() => resolver.Resolve("gargarde.sgt", true));
    }

    [Fact]
    public void MissingTrackLeavesTheNativeResourceAvailable()
    {
        Assert.Null(Create("", "battle").Resolve("hikutei.sgt"));
    }

    [Fact]
    public void ValidationReportsThePackageAndConfigAddress()
    {
        ModPackage package = new("example", "Example", ModFormat.Junction, true, true, ModEffectiveStatus.Active, ".");
        ModFile file = new(package.Id, ModFormat.Junction, "music/config.toml", 1, () => Encoding.UTF8.GetBytes("[battle]\nshuffle = ['other']"));
        ModDiagnostic diagnostic = Assert.Single(ExternalMusicResolver.Validate(new ModCatalogSnapshot(1, [package], [file])));
        Assert.Equal(package.Id, diagnostic.PackageId);
        Assert.Equal("music/config.toml", diagnostic.RelativePath);
        Assert.Equal("external-music-config-invalid", diagnostic.Code);
    }

    [Fact]
    public void ExplicitlyDisabledExternalMusicDoesNotParseOrSelectItsFiles()
    {
        ModPackage package = new("example", "Example", ModFormat.Junction, true, true, ModEffectiveStatus.Active, ".") { JunctionMetadata = new JunctionManifestMetadata { UseExternalMusic = false } };
        ModFile file = new(package.Id, ModFormat.Junction, "music/config.toml", 1, () => Encoding.UTF8.GetBytes("unsupported = true"));
        ModFile track = new(package.Id, ModFormat.Junction, "music/battle.ogg", 1, () => []);
        ModCatalogSnapshot catalog = new(1, [package], [file, track]);
        Assert.Empty(ExternalMusicResolver.Validate(catalog));
        Assert.Null(new ExternalMusicResolver(catalog).Resolve("battle.sgt"));
    }

    private static ExternalMusicResolver Create(string configuration, params string[] names)
    {
        ModPackage package = new("example", "Example", ModFormat.Junction, true, true, ModEffectiveStatus.Active, ".");
        List<ModFile> files = [new(package.Id, ModFormat.Junction, "music/config.toml", 1, () => Encoding.UTF8.GetBytes(configuration))];
        files.AddRange(names.Select(name => new ModFile(package.Id, ModFormat.Junction, $"music/{name}.ogg", 1, () => [])));
        return new ExternalMusicResolver(new ModCatalogSnapshot(1, [package], files));
    }
}
