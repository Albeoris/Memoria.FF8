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
        Assert.Contains(menu.Entries, entry => entry.Label == "[x] Automatically enable new mods");
        Assert.Contains(menu.Entries, entry => entry.Id == "Demaster/example" && entry.Label.StartsWith("[x] example", StringComparison.Ordinal) && entry.Label.Contains("Demaster", StringComparison.Ordinal) && entry.Help == "Active");
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
        Assert.Contains("Reload pending", menu.CurrentScreen.Details(menu));
        Publish(runtime);
        Assert.Contains(menu.Entries, entry => entry.Id == "Demaster/example" && entry.Label.StartsWith("[ ]", StringComparison.Ordinal) && entry.Help == "Disabled");
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
        Assert.False(package.Enabled);
        Assert.Equal("Incompatible edition", package.Help);
        menu.Navigate(MenuInput.Down);
        menu.Navigate(MenuInput.Down);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("Incompatible edition", menu.Message);
        Assert.Contains("enabled = 0", File.ReadAllText(Path.Combine(root, "Mods/Tonberry/incompatible/mod.ini")));
        Assert.False(runtime.IsReloadPending);
    }

    [Fact]
    public void RejectedReloadKeepsPublishedCheckboxAndDisplaysFailure()
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
        Assert.Contains(menu.Entries, entry => entry.Id == "Demaster/example" && entry.Label.StartsWith("[x]", StringComparison.Ordinal));
        Assert.Contains(menu.CurrentScreen.Details(menu), text => text.Contains("failed", StringComparison.OrdinalIgnoreCase));
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
        Assert.Contains(menu.Entries, entry => entry.Id == "mod-auto" && entry.Label.StartsWith("[ ]", StringComparison.Ordinal));
        Assert.True(runtime.Catalog.Packages[0].RequestedEnabled);
    }

    [Fact]
    public void PackageOwnedFailureDisplaysReloadFailedWithoutChangingPublishedCatalog()
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
        Assert.Equal("Reload failed", entry.Help);
        Assert.Contains("Reload failed", entry.Label);
        Assert.Same(catalog, runtime.Catalog);
        Assert.Equal(ModEffectiveStatus.Active, runtime.Catalog.Packages[0].Status);
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
