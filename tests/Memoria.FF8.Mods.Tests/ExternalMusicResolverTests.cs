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

    [Fact]
    public void SelectedPackageSettingsChooseFormatConfigBiosAndLibraryTogether()
    {
        ModPackage low = new("low", "Low", ModFormat.Junction, true, true, ModEffectiveStatus.Active, ".");
        ModPackage high = new("high", "High", ModFormat.Junction, true, true, ModEffectiveStatus.Active, ".")
        {
            JunctionMetadata = new JunctionManifestMetadata
            {
                ExternalMusicPath = "music/synth",
                ExternalMusicExtension = "minipsf",
                PsfBiosPath = "music/synth/bios.bin"
            }
        };
        ModFile Entry(string package, string path, byte[] bytes)
        {
            return new ModFile(package, ModFormat.Junction, path, 1, () => bytes);
        }
        ModFile library = Entry(high.Id, "music/synth/shared/library.psflib", [3]);
        ModFile bios = Entry(high.Id, "music/synth/bios.bin", [4]);
        ModCatalogSnapshot catalog = new(1, [high, low],
        [
            Entry(low.Id, "music/battle.ogg", [1]),
            Entry(low.Id, "music/synth/shared/library.psflib", [2]),
            Entry(high.Id, "music/synth/battle.ogg", [5]),
            Entry(high.Id, "music/synth/battle.minipsf", [6]),
            Entry(high.Id, "music/synth/alternate.minipsf", [7]),
            Entry(high.Id, "music/synth/config.toml", Encoding.UTF8.GetBytes("volume = 70\n[battle]\nno_intro_track = 'alternate'\noffset_seconds = 2.5")),
            library,
            bios
        ]);
        ExternalMusicSelection selection = new ExternalMusicResolver(catalog).Resolve("003-battle.sgt", true)!;
        Assert.Equal(high.Id, selection.File.PackageId);
        Assert.Equal("music/synth/alternate.minipsf", selection.File.RelativePath);
        Assert.Equal(2.5, selection.OffsetSeconds);
        Assert.Equal(70, selection.Volume);
        Assert.Same(bios, selection.BiosFile);
        Assert.Same(library, selection.ResolveDependency(selection.File.RelativePath, "shared/library.psflib"));
        Assert.Same(library, selection.ResolveDependency("music/synth/shared/library.psflib", "../shared/library.psflib"));
    }

    [Theory]
    [InlineData("../../../outside.psflib")]
    [InlineData("/outside.psflib")]
    [InlineData("C:/outside.psflib")]
    [InlineData("\\\\server/share/library.psflib")]
    [InlineData("bad\0.psflib")]
    public void LibraryReferencesCannotEscapeTheSelectedPackage(string dependency)
    {
        ExternalMusicSelection selection = Create("", "battle").Resolve("battle.sgt")!;
        Assert.Throws<InvalidDataException>(() => selection.ResolveDependency(selection.File.RelativePath, dependency));
    }

    [Fact]
    public void MissingLibraryDoesNotUseAnotherPackageAndUnknownRequesterIsRejected()
    {
        ModPackage high = new("high", "High", ModFormat.Junction, true, true, ModEffectiveStatus.Active, ".");
        ModPackage low = new("low", "Low", ModFormat.Junction, true, true, ModEffectiveStatus.Active, ".");
        ModFile track = new(high.Id, ModFormat.Junction, "music/battle.ogg", 1, () => []);
        ModFile library = new(low.Id, ModFormat.Junction, "music/shared.psflib", 1, () => []);
        ExternalMusicSelection selection = new ExternalMusicResolver(new ModCatalogSnapshot(1, [high, low], [track, library])).Resolve("battle.sgt")!;
        InvalidDataException missing = Assert.Throws<InvalidDataException>(() => selection.ResolveDependency(track.RelativePath, "shared.psflib"));
        Assert.Contains("high", missing.Message);
        Assert.Contains("shared.psflib", missing.Message);
        Assert.Throws<InvalidDataException>(() => selection.ResolveDependency("music/absent.minipsf", "battle.ogg"));
    }

    [Fact]
    public void MissingDeclaredBiosFailsBeforePlayback()
    {
        ModPackage package = new("example", "Example", ModFormat.Junction, true, true, ModEffectiveStatus.Active, ".") { JunctionMetadata = new JunctionManifestMetadata { PsfBiosPath = "music/bios.bin" } };
        ModFile track = new(package.Id, ModFormat.Junction, "music/battle.ogg", 1, () => []);
        ExternalMusicResolver resolver = new(new ModCatalogSnapshot(1, [package], [track]));
        Assert.Contains("BIOS", Assert.Throws<InvalidDataException>(() => resolver.Resolve("battle.sgt")).Message);
    }

    private static ExternalMusicResolver Create(string configuration, params string[] names)
    {
        ModPackage package = new("example", "Example", ModFormat.Junction, true, true, ModEffectiveStatus.Active, ".");
        List<ModFile> files = [new(package.Id, ModFormat.Junction, "music/config.toml", 1, () => Encoding.UTF8.GetBytes(configuration))];
        files.AddRange(names.Select(name => new ModFile(package.Id, ModFormat.Junction, $"music/{name}.ogg", 1, () => [])));
        return new ExternalMusicResolver(new ModCatalogSnapshot(1, [package], files));
    }
}
