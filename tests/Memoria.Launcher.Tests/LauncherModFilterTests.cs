using Memoria.FF8.Mods;

namespace Memoria.Launcher.Tests;

public sealed class LauncherModFilterTests
{
    [Fact]
    public void MemoriaPackageIsFirstAndExpandsWithoutLosingOtherMods()
    {
        RemoteModEntry entry = new(OfficialModCatalog.SourceId, OfficialModCatalog.GirlNextDoor.CatalogId, "Girl Next Door", "1") { Official = OfficialModCatalog.GirlNextDoor };
        LauncherWindow.LauncherModRow external = new("external", "A mod", "", "Gameplay", null, null);
        LauncherWindow.LauncherModRow official = new("official", entry.Name, "", "Memoria", null, entry);
        LauncherWindow.LauncherModRow[] rows = [external, official];
        Assert.Equal(["Memoria", "A mod"], LauncherWindow.GroupLauncherModRows(rows, false).Select(row => row.Name));
        Assert.Equal(["Memoria", "Girl Next Door", "A mod"], LauncherWindow.GroupLauncherModRows(rows, true).Select(row => row.Name));
        Assert.Same(official, LauncherWindow.GroupLauncherModRows(rows, true)[1]);
        Assert.Equal([external], LauncherWindow.GroupLauncherModRows([external], false));
        Assert.Empty(LauncherWindow.GroupLauncherModRows([], false));
    }

    [Fact]
    public void InstalledBundledComponentsUseTheRuntimeGroupIdentity()
    {
        ModPackage package = new(ModRuntime.Ff7EnemyIntegrationPackageId, "FF7 Enemy Integration", ModFormat.Memoria, null, true, ModEffectiveStatus.Active, "") { IsBundled = true };
        ModPackageView installed = new(package, ModEffectiveStatus.Active, "Active", null, false, true);
        LauncherWindow.LauncherModRow row = new("installed", package.Name, "", "Memoria", installed, null);
        Assert.Equal(ModRuntime.MemoriaPackageId, ModRuntime.SettingsGroupId(package));
        Assert.Equal(["Memoria"], LauncherWindow.GroupLauncherModRows([row], false).Select(item => item.Name));
        Assert.False((row with { Installed = installed with { Package = package with { Id = "Memoria/unrelated", IsBundled = false } } }).IsMemoria);
    }

    [Theory]
    [InlineData("audio", "All categories", true)]
    [InlineData("voices", "Audio", true)]
    [InlineData("voices", "Gameplay", false)]
    [InlineData("missing", "All categories", false)]
    [InlineData("", "Audio", true)]
    public void SearchMatchesNamesAndDescriptionsWithinSelectedCategory(string search, string category, bool expected)
    {
        Assert.Equal(expected, LauncherWindow.MatchesLauncherModFilter("Audio pack", "New character voices", "Audio", search, category));
    }
    [Fact]
    public void CategoryCountsReflectOnlyTheProvidedTabAndSearchResults()
    {
        var categories = LauncherWindow.CreateLauncherModCategories(["Audio", "audio", "Field", ""]);
        Assert.Equal(["All categories (4)", "Audio (2)", "Field (1)"], categories.Select(category => category.ToString()));
        Assert.Equal(["All categories (0)"], LauncherWindow.CreateLauncherModCategories([]).Select(category => category.ToString()));
    }

    [Fact]
    public void SelectionSurvivesPollingAndFallsBackWhenFilteredOut()
    {
        Assert.Equal("second", LauncherWindow.SelectLauncherModKey(["first", "second"], "second"));
        Assert.Equal("first", LauncherWindow.SelectLauncherModKey(["first"], "second"));
        Assert.Null(LauncherWindow.SelectLauncherModKey([], "second"));
    }

    [Fact]
    public void OfflineOfficialSeedExplainsWhyMissingDownloadCannotBeQueued()
    {
        OfficialModComponent component = OfficialModCatalog.GirlNextDoor with { Latest = null };
        RemoteModEntry entry = new(OfficialModCatalog.SourceId, component.CatalogId, component.Name, "") { Official = component };
        ModLibrarySnapshot snapshot = new(0, new([entry], null, false, false, null), [], "Classic/en/test", true) { OfficialSupported = true };
        Assert.True(LauncherWindow.IsLauncherDownloadComponent(entry));
        Assert.Contains("No download is published", LauncherWindow.LauncherModDownloadBlocker(entry, snapshot));
        Assert.Equal("Requires a newer engine", LauncherWindow.LauncherModDownloadBlocker(entry with { CompatibilityError = "Requires a newer engine" }, snapshot));
        Assert.False(LauncherWindow.IsLauncherDownloadComponent(entry with { Official = component with { Delivery = "engine" } }));
    }

    [Fact]
    public void PublishedSupportedOfficialDownloadPassesTheSameGateAsTheBulkAction()
    {
        OfficialModArtifact artifact = new("https://example.invalid/mod.zip", 10, 20, new string('a', 64));
        OfficialModComponent component = OfficialModCatalog.GirlNextDoor with { Latest = new("1.0.0", "[1.0.0,)", "v1.0.0", "", artifact) };
        RemoteModEntry entry = new(OfficialModCatalog.SourceId, component.CatalogId, component.Name, component.Latest.Version) { Official = component, Links = [artifact.Url] };
        ModLibrarySnapshot snapshot = new(0, new([entry], null, false, false, null), [], "Classic/en/test", false) { OfficialSupported = true };
        Assert.Null(LauncherWindow.LauncherModDownloadBlocker(entry, snapshot));
        Assert.NotNull(LauncherWindow.LauncherModDownloadBlocker(entry, snapshot with { OfficialSupported = false }));
        Assert.NotNull(LauncherWindow.LauncherModDownloadBlocker(entry with { IsStale = true }, snapshot));
    }
    [Fact]
    public void DownloadedAndManagedEntriesRemainReachableWithoutTheOnlineCatalog()
    {
        RemoteModEntry retained = new("source", Guid.NewGuid(), "Installed mod", "1");
        RemoteModEntry archive = new("source", Guid.NewGuid(), "Cached mod", "1");
        RemoteModEntry cancelled = new("source", Guid.NewGuid(), "Discarded mod", "1");
        ModLibraryJob Job(RemoteModEntry entry) => new(Guid.NewGuid(), entry, "Cancelled", 0, null, 0, null, null, false, false, false, false, false);
        ModLibrarySnapshot snapshot = new(0, new([], null, false, false, null), [Job(retained), Job(archive) with { CanDeleteDownload = true }, Job(cancelled)], "Classic/en/test", true)
        {
            Installations = [new(retained.SourceId, retained.Id, "1", "owned", false) { CanUninstall = true }]
        };
        RemoteModEntry[] entries = LauncherWindow.LauncherKnownModEntries(snapshot).ToArray();
        Assert.Equal([retained.Id, archive.Id], entries.Select(entry => entry.Id));
        Assert.All(entries, entry => Assert.True(entry.IsStale));
        RemoteModEntry current = retained with { Version = "2" };
        Assert.Equal("2", LauncherWindow.LauncherKnownModEntries(snapshot with { Catalog = new([current], null, false, false, null) }).First().Version);
    }
}
