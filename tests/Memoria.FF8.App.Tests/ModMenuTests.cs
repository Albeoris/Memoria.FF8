using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

public sealed class ModMenuTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-mod-menu-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ModsEntryIsAvailableInMainMenuAndShowsPackageState()
    {
        Write("Demaster/example/test.png", "metadata only");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        MenuController menu = Menu(runtime);
        menu.Open("main");
        MenuEntry entry = Assert.Single(menu.Entries, entry => entry.Id == "mods");
        Assert.Equal("Mods", entry.Label);
        Assert.Equal("mods", entry.Destination);
        menu.Open("mods");
        Assert.Contains("game and application", menu.Entries.Single(entry => entry.Id == "mod-refresh").Help);
        Assert.Contains(menu.Entries, entry => entry.Id == "mod-auto" && entry.Label == "Automatically enable new mods");
        Assert.Contains(menu.Entries, entry => entry.Id == "Demaster/example" && entry.Label == "example" && entry.Help == "Active");
        Assert.NotNull(menu.BuildView());
    }

    [Fact]
    public void ManualTogglePersistsBeforeCatalogChangesAndShowsPendingThenDisabled()
    {
        Write("Demaster/example/test.png", "metadata only");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        MenuController menu = Menu(runtime);
        menu.Open("mods");
        menu.Execute(new("mod-toggle", "Demaster/example"));
        Assert.Contains("enabled = 0", File.ReadAllText(Path.Combine(root, "Mods/Demaster/example/mod.ini")));
        Assert.True(runtime.Catalog.Packages[0].RequestedEnabled);
        Assert.Contains("queued", menu.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("In use: Active \u00b7 disabling\u2026", menu.CurrentScreen.Details(menu));
        Publish(runtime);
        Assert.Contains(menu.Entries, entry => entry.Id == "Demaster/example" && entry.Label == "example" && entry.Help == "Disabled");
    }

    [Fact]
    public void IncompatiblePackageCheckboxIsDisabledAndPreferenceSurvivesConfirmation()
    {
        Write("Tonberry/incompatible/mod.ini", "enabled = 0\n");
        Write("Tonberry/incompatible/hashmap/test.csv", "not parsed");
        using ModRuntime runtime = Runtime(compatible: format => format != ModFormat.Tonberry);
        Publish(runtime);
        MenuController menu = Menu(runtime);
        menu.Open("mods");
        MenuEntry package = menu.Entries.Single(entry => entry.Id == "Tonberry/incompatible");
        Assert.True(package.Enabled);
        Assert.Equal("Incompatible edition", package.Help);
        menu.SelectEntry("mods", "Tonberry/incompatible", allowDisabled: true);
        menu.Navigate(MenuInput.Confirm);
        Assert.Contains("incompatible", menu.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("enabled = 0", File.ReadAllText(Path.Combine(root, "Mods/Tonberry/incompatible/mod.ini")));
        Assert.False(runtime.IsReloadPending);
    }

    [Fact]
    public void RejectedReloadShowsInstalledPreferenceAndKeepsPublishedCatalog()
    {
        Write("Demaster/example/test.png", "metadata only");
        bool reject = false;
        using ModRuntime runtime = Runtime(prepare: _ => new(!reject));
        Publish(runtime);
        MenuController menu = Menu(runtime);
        menu.Open("mods");
        reject = true;
        menu.Execute(new("mod-toggle", "Demaster/example"));
        Assert.False(runtime.PreparePending(force: true));
        menu.RefreshModView();
        Assert.Contains(menu.Entries, entry => entry.Id == "Demaster/example" && entry.Label == "example" && entry.Help == "Active \u00b7 disable failed");
        Assert.True(Assert.Single(runtime.Catalog.Packages).RequestedEnabled);
        Assert.Contains(menu.CurrentScreen.Details(menu), text => text.Contains("failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NewPackageAwaitingGuardianReviewRemainsVisibleWithItsReasonAndCanBeDisabled()
    {
        using ModRuntime runtime = Runtime(prepare: catalog => catalog.Files.Count == 0 ? ModPreparationResult.Accepted : new(false, Diagnostics: [new("Memoria.GuardianForceReview", "GF changes await review.")]));
        Publish(runtime);
        MenuController menu = Menu(runtime);
        menu.Open("mods");
        Write("Memoria/Example/gameplay/gf-abilities.json", "{}");
        runtime.Refresh();
        Assert.False(runtime.PreparePending(force: true));
        menu.RefreshModView();
        Assert.Empty(runtime.Catalog.Packages);
        Assert.Contains(menu.Entries, entry => entry.Id == "Memoria/Example" && entry.Help == "Not active \u00b7 review required");
        Assert.Contains(menu.Entries, entry => entry.Id == "gf-review" && entry.Label.Contains("Pending", StringComparison.Ordinal));
        menu.SelectEntry("mods", "Memoria/Example");
        Assert.Contains(menu.CurrentScreen.Details(menu), detail => detail.Contains("require GF review", StringComparison.OrdinalIgnoreCase));
        menu.Execute(new("mod-toggle", "Memoria/Example"));
        Publish(runtime);
        Assert.Contains(menu.Entries, entry => entry.Id == "Memoria/Example" && entry.Help == "Disabled");
    }

    [Fact]
    public void RefreshActionEnqueuesRuntimeAndGlobalOffPreservesExistingEnabledPackage()
    {
        Write("Demaster/example/test.png", "metadata only");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        MenuController menu = Menu(runtime);
        menu.Open("mods");
        menu.Execute(new("mod-refresh"));
        Assert.True(runtime.IsReloadPending);
        Publish(runtime);
        menu.Execute(new("mod-auto"));
        Assert.False(runtime.AutoEnableNewMods);
        Assert.Contains("enabled = 1", File.ReadAllText(Path.Combine(root, "Mods/Demaster/example/mod.ini")));
        Publish(runtime);
        Assert.Contains(menu.Entries, entry => entry.Id == "mod-auto" && entry.Label == "Automatically enable new mods");
        Assert.True(runtime.Catalog.Packages[0].RequestedEnabled);
    }

    [Fact]
    public void PackageOwnedFailureKeepsCommittedStatusAndDisplaysProblemWithoutChangingPublishedCatalog()
    {
        Write("Demaster/example/test.png", "metadata only");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        ModCatalogSnapshot catalog = runtime.Catalog;
        MenuController menu = Menu(runtime);
        menu.Open("mods");
        runtime.ReportFailure(new("replacement-failed", "The image is incomplete.", "Demaster/example", "test.png"));
        menu.RefreshModView();
        MenuEntry entry = menu.Entries.Single(entry => entry.Id == "Demaster/example");
        Assert.Equal(Assert.Single(runtime.View.Packages).StatusText, entry.Help);
        Assert.StartsWith("Active", entry.Help);
        Assert.Equal("example", entry.Label);
        menu.SelectEntry("mods", "Demaster/example");
        Assert.Contains("The image is incomplete.", menu.CurrentScreen.Details(menu));
        Assert.Same(catalog, runtime.Catalog);
        Assert.Equal(ModEffectiveStatus.Active, runtime.Catalog.Packages[0].Status);
    }

    [Fact]
    public void SearchAndRequestedStateFilterKeepPackageIdentityAndServicePolicy()
    {
        Write("Demaster/alpha/test.png", "metadata only");
        Write("Demaster/beta/test.png", "metadata only");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        MenuController menu = Menu(runtime);
        menu.Open("mods");
        menu.SelectEntry("mods", "Demaster/beta");
        _ = menu.CurrentScreen.Details(menu);
        menu.SelectEntry("mods", "mod-search");
        menu.Navigate(MenuInput.Confirm);
        foreach (char letter in "beta")
        {
            Assert.True(menu.EnterModSearchCharacter(letter));
        }
        menu.Navigate(MenuInput.Cancel);
        Assert.Contains(menu.Entries, entry => entry.Id == "Demaster/beta");
        Assert.DoesNotContain(menu.Entries, entry => entry.Id == "Demaster/alpha");
        Assert.Equal("Demaster/beta", menu.SelectedEntry!.Id);
        menu.Execute(new MenuAction("mod-filter"));
        Assert.Contains(menu.Entries, entry => entry.Id == "Demaster/beta");
        menu.Execute(new MenuAction("mod-filter"));
        Assert.DoesNotContain(menu.Entries, entry => entry.Action?.Kind == "mod-toggle");
        Assert.True(runtime.AutoEnableNewMods);
        Assert.False(runtime.IsReloadPending);
        Assert.Contains(menu.Entries, entry => entry.Id == "mod-refresh");
        Assert.NotNull(menu.BuildView());
    }

    [Fact]
    public void DiagnosticsNavigateByPackageAndRefreshClearsTheOpenGroup()
    {
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        runtime.ReportFailure(new("duplicate", "First problem.", "Tonberry/alpha", "hashmap/alpha.csv"));
        runtime.ReportFailure(new("duplicate", "Second problem.", "tonberry/ALPHA", "hashmap/beta.csv"));
        runtime.ReportFailure(new("missing", "Other mod problem.", "Demaster/beta"));
        runtime.ReportFailure(new("transaction", "Global problem."));
        MenuController menu = Menu(runtime);
        menu.Open("mods-diagnostics");
        Assert.Equal(4, menu.Entries.Count);
        Assert.Contains(menu.Entries, entry => entry.Label == "Tonberry/alpha · 2");
        Assert.DoesNotContain(menu.Entries, entry => entry.Label.Contains("duplicate", StringComparison.Ordinal));
        menu.SelectEntry("mods-diagnostics", "diagnostic-mod-Tonberry/alpha");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("mods-package-diagnostics", menu.ScreenId);
        Assert.Equal(3, menu.Entries.Count);
        menu.SelectEntry(menu.ScreenId, "diagnostic-1");
        Assert.Equal(new[] { "Tonberry/alpha", "duplicate", "Second problem.", "hashmap/beta.csv" }, menu.CurrentScreen.Details(menu));
        Assert.NotNull(menu.BuildView());
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal("diagnostic-mod-Tonberry/alpha", menu.SelectedEntry!.Id);
        menu.SelectEntry(menu.ScreenId, "diagnostic-mod-Demaster/beta");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("retry", menu.SelectedEntry!.Id);
        menu.SelectEntry(menu.ScreenId, "diagnostic-0");
        Assert.Contains("Other mod problem.", menu.CurrentScreen.Details(menu));
        Assert.DoesNotContain("First problem.", menu.CurrentScreen.Details(menu));
        menu.Navigate(MenuInput.Cancel);
        menu.SelectEntry(menu.ScreenId, "diagnostic-mod-");
        menu.Navigate(MenuInput.Confirm);
        menu.SelectEntry(menu.ScreenId, "diagnostic-0");
        Assert.Equal(new[] { "Global transaction", "transaction", "Global problem." }, menu.CurrentScreen.Details(menu));
        menu.SelectEntry(menu.ScreenId, "retry");
        menu.Navigate(MenuInput.Confirm);
        Assert.True(runtime.IsReloadPending);
        Assert.False(runtime.PreparePending(force: true));
        Assert.Empty(runtime.View.Diagnostics);
        Assert.Contains(menu.Entries, entry => entry.Id == "none");
        Assert.DoesNotContain(menu.Entries, entry => entry.Id.StartsWith("diagnostic-", StringComparison.Ordinal));
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal(new[] { "retry", "none" }, menu.Entries.Select(entry => entry.Id));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private ModRuntime Runtime(Func<ModFormat, bool>? compatible = null, Func<ModCatalogSnapshot, ModPreparationResult>? prepare = null)
    {
        return new(root, compatible, prepare, watch: false, debounce: TimeSpan.Zero);
    }

    private static MenuController Menu(IModService service)
    {
        MenuController menu = new();
        menu.ConnectMods(service);
        return menu;
    }

    private void Write(string relative, string contents)
    {
        string path = Path.Combine(root, "Mods", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
    }
}
