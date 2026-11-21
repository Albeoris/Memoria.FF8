using System.IO.Compression;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using Memoria.FF8.Mods;

namespace Memoria.Launcher.Tests;

[Collection("Incident dialog UI")]
public sealed class LauncherModPresentationTests(LauncherHeadlessFixture fixture)
{
    [Fact]
    public async Task InstalledLocalJunctionArchiveOffersDeleteWithoutAnInstallerReceipt()
    {
        await fixture.Session.Dispatch(async () =>
        {
            string root = DistributionPaths.GetFullPath(Path.Combine(Path.GetTempPath(), "launcher-local-mod-" + Guid.NewGuid().ToString("N")));
            string modsRoot = Path.Combine(root, "Mods");
            string wrapper = Path.Combine(modsRoot, "Junction", "Download");
            Directory.CreateDirectory(wrapper);
            try
            {
                string archive = Path.Combine(wrapper, "Local.iroj");
                RemoteModEntry entry = new("external", Guid.NewGuid(), "Local archive", "1");
                using (BinaryWriter writer = new(File.Create(archive)))
                {
                    byte[] name = Encoding.Unicode.GetBytes("mod.xml");
                    byte[] manifest = Encoding.UTF8.GetBytes($"<ModInfo><ID>{entry.Id}</ID><Name>{entry.Name}</Name><Version>1</Version></ModInfo>");
                    writer.Write(0x534f5249);
                    writer.Write(0x10002);
                    writer.Write(0);
                    writer.Write(16);
                    writer.Write(1);
                    writer.Write((ushort)(20 + name.Length));
                    writer.Write((ushort)name.Length);
                    writer.Write(name);
                    writer.Write(0);
                    writer.Write(40L + name.Length);
                    writer.Write(manifest.Length);
                    writer.Write(manifest);
                }
                string metadata = Path.Combine(wrapper, "download.json");
                File.WriteAllText(metadata, "{}");
                string settings = archive + ".ini";
                File.WriteAllText(settings, "enabled = 0\n");
                using ModRuntime mods = new(root, watch: false, modsDirectory: modsRoot);
                ModPackageInstaller installer = new(ModPackageInstaller.StateDirectoryFor(modsRoot), modsRoot, "Classic/en/test");
                using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", true);
                await library.StopAsync();
                mods.PreparePending(true);
                mods.CommitPending();
                ModPackage package = Assert.Single(mods.InstalledPackages);
                Assert.Equal(entry.Id, package.JunctionMetadata?.Id);
                ModLibrarySnapshot snapshot = new(1, new([entry], null, false, false, null), [], "Classic/en/test", true);
                typeof(ModLibraryService).GetField("view", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(library, snapshot);
                LauncherWindow window = new();
                try
                {
                    Set(window, "launcherMods", mods);
                    Set(window, "launcherModLibrary", library);
                    Get<TabControl>(window, "launcherModTabs").SelectedIndex = 0;
                    Render(window);
                    StackPanel details = Get<StackPanel>(window, "launcherModDetails");
                    string[] buttons = details.GetLogicalDescendants().OfType<Button>().Select(button => (string)button.Content!).ToArray();
                    Assert.Contains("Delete", buttons);
                    Assert.DoesNotContain("Uninstall", buttons);

                    mods.DeleteLocalPackage(package);
                    mods.Refresh();
                    mods.PreparePending(true);
                    mods.CommitPending();
                    Render(window);

                    Assert.False(File.Exists(archive));
                    Assert.True(File.Exists(metadata));
                    Assert.True(File.Exists(settings));
                    Assert.Empty(Get<ListBox>(window, "launcherModRows").Items);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InstalledDemasterPackageGetsThePreviewAction()
    {
        await fixture.Session.Dispatch(async () =>
        {
            string root = Path.Combine(Path.GetTempPath(), "launcher-preview-action-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                using ModRuntime mods = new(root, watch: false);
                ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
                using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
                await library.StopAsync();
                LauncherWindow window = new();
                try
                {
                    ComboBox installations = Get<ComboBox>(window, "installations");
                    installations.ItemsSource = new[] { new GameInstallation(root, "Preview fixture") };
                    installations.SelectedIndex = 0;
                    Set(window, "launcherModLibrary", library);
                    ModPackage package = new("Demaster/cards", "Cards", ModFormat.Demaster, true, true, ModEffectiveStatus.Active, root);
                    ModPackageView installed = new(package, ModEffectiveStatus.Active, "Active", null, false, true);
                    var method = typeof(LauncherWindow).GetMethod("CreateLauncherModPreview", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    Button button = Assert.IsType<Button>(method.Invoke(window, [installed, null]));
                    Assert.Equal("Preview changes", button.Content);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CheckedVersionMismatchKeepsItsPreviewAndExplicitInstallAfterCatalogRefresh()
    {
        await fixture.Session.Dispatch(async () =>
        {
            string root = DistributionPaths.GetFullPath(Path.Combine(Path.GetTempPath(), "launcher-version-warning-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            try
            {
                RemoteModEntry pinned = new("external", Guid.NewGuid(), "Fixture", "1") { Links = ["https://example.invalid/mod.zip"] };
                RemoteModEntry current = pinned with { Version = "3" };
                string archive = Path.Combine(root, "mod.zip");
                using (ZipArchive zip = ZipFile.Open(archive, ZipArchiveMode.Create))
                {
                    using (StreamWriter manifest = new(zip.CreateEntry("mod.xml").Open()))
                    {
                        manifest.Write($"<ModInfo><ID>{pinned.Id}</ID><Name>Fixture</Name><Version>2</Version></ModInfo>");
                    }
                    using StreamWriter payload = new(zip.CreateEntry("a.bin").Open());
                    payload.Write("Preview fixture");
                }
                using ModRuntime mods = new(root, watch: false);
                ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
                using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", true);
                await library.StopAsync();
                ModPackageInstallPlan plan = await installer.InspectAsync(archive, pinned, TestContext.Current.CancellationToken);
                string warning = Assert.IsType<string>(plan.VersionWarning);
                ModLibraryJob job = new(Guid.NewGuid(), pinned, "Ready", 0, null, 0, null, null, false, false, false, true, false) { VersionWarning = warning };
                typeof(ModLibraryJob).GetProperty("PreviewPlan", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(job, plan);
                ModLibrarySnapshot snapshot = new(1, new([current], null, false, false, null), [job], "Classic/en/test", true);
                typeof(ModLibraryService).GetField("view", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(library, snapshot);
                LauncherWindow window = new();
                try
                {
                    ComboBox installations = Get<ComboBox>(window, "installations");
                    installations.ItemsSource = new[] { new GameInstallation(root, "Preview fixture") };
                    installations.SelectedIndex = 0;
                    Set(window, "launcherMods", mods);
                    Set(window, "launcherModLibrary", library);
                    TabControl tabs = Get<TabControl>(window, "launcherModTabs");
                    StackPanel details = Get<StackPanel>(window, "launcherModDetails");
                    tabs.SelectedIndex = 2;
                    Render(window);

                    TextBlock notice = Assert.Single(details.GetLogicalDescendants().OfType<TextBlock>(), block => block.Text == warning);
                    Color color = Assert.IsAssignableFrom<ISolidColorBrush>(notice.Foreground).Color;
                    Assert.Contains(color, new[] { Color.Parse("#F3A654"), Color.Parse("#9A4600") });
                    Assert.Contains(warning, Get<string>(window, "launcherModDetailText"));
                    string[] actions = details.GetLogicalDescendants().OfType<Button>().Select(button => (string)button.Content!).ToArray();
                    Assert.Contains("Install anyway", actions);
                    Assert.DoesNotContain("Install", actions);
                    Assert.Contains("Preview changes", actions);
                    Get<ComboBox>(window, "launcherModFilter").SelectedIndex = 4;
                    Assert.Single(Get<ListBox>(window, "launcherModRows").Items);

                    Get<ComboBox>(window, "launcherModFilter").SelectedIndex = 0;
                    tabs.SelectedIndex = 1;
                    Render(window);
                    Assert.Contains(details.GetLogicalDescendants().OfType<Button>(), button => Equals(button.Content, "Download & check"));
                    Assert.DoesNotContain(details.GetLogicalDescendants().OfType<Button>(), button => Equals(button.Content, "Install anyway"));
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CatalogPackageExpandsWithCrestAndKeepsComponentSelectionOnRefresh()
    {
        await fixture.Session.Dispatch(async () =>
        {
            string root = Path.Combine(Path.GetTempPath(), "launcher-mod-presentation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                using ModRuntime mods = new(root, watch: false);
                ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
                using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
                await library.StopAsync();
                Application.Current!.Styles.Add(new FluentTheme());
                RemoteModEntry[] external = Enumerable.Range(1, 30).Select(index => new RemoteModEntry("external", Guid.NewGuid(), $"Third-party mod {index}", "1")).ToArray();
                RemoteModEntry official = new(OfficialModCatalog.SourceId, OfficialModCatalog.GirlNextDoor.CatalogId, "Girl Next Door", "1") { Official = OfficialModCatalog.GirlNextDoor };
                ModLibrarySnapshot snapshot = new(1, new([.. external, official], null, false, false, null), [], "Classic/en/test", true);
                typeof(ModLibraryService).GetField("view", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(library, snapshot);
                LauncherWindow window = new();
                Window host = new() { Width = 900, Height = 480 };
                try
                {
                    Set(window, "launcherMods", mods);
                    Set(window, "launcherModLibrary", library);
                    Get<TabControl>(window, "launcherModTabs").SelectedIndex = 1;
                    Render(window);
                    TabItem modsPage = Assert.IsType<TabItem>(Get<TabControl>(window, "launcherPages").Items[1]);
                    host.Content = modsPage.Content;
                    modsPage.Content = null;
                    host.Show();
                    host.UpdateLayout();
                    ListBox rows = Get<ListBox>(window, "launcherModRows");
                    ListBoxItem[] collapsed = rows.Items.Cast<ListBoxItem>().ToArray();
                    Assert.Equal(31, collapsed.Length);
                    Assert.Equal("group:Memoria", collapsed[0].Tag);
                    Image crest = Assert.Single(((Control)collapsed[0].Content!).GetLogicalDescendants().OfType<Image>());
                    Assert.NotNull(crest.Source);
                    GridSplitter splitter = Assert.Single(((Grid)rows.Parent!).Children.OfType<GridSplitter>());
                    Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(splitter.Background).Color);
                    Button toggle = Assert.Single(((Control)collapsed[0].Content!).GetLogicalDescendants().OfType<Button>());
                    Assert.Equal("\u25B8", toggle.Content);
                    StackPanel details = Get<StackPanel>(window, "launcherModDetails");
                    Button open = Assert.Single(details.Children.OfType<Button>());
                    open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    host.UpdateLayout();
                    Assert.Equal(32, rows.ItemCount);
                    ListBoxItem component = rows.Items.Cast<ListBoxItem>().ElementAt(1);
                    Assert.Equal(official.SourceId + ":" + official.Id, component.Tag);
                    Assert.Same(component, rows.SelectedItem);
                    Assert.Contains(rows, component.GetVisualAncestors());
                    Render(window);
                    Assert.Same(component, rows.SelectedItem);
                    Assert.DoesNotContain(details.GetLogicalDescendants().OfType<TextBlock>(), text => text.Text?.Contains("Restart required", StringComparison.OrdinalIgnoreCase) == true);

                    ListBoxItem header = Assert.IsType<ListBoxItem>(rows.Items[0]);
                    Button collapse = Assert.Single(((Control)header.Content!).GetLogicalDescendants().OfType<Button>());
                    collapse.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    host.UpdateLayout();
                    Assert.Equal(31, rows.ItemCount);
                    ListBoxItem selectedHeader = Assert.IsType<ListBoxItem>(rows.SelectedItem);
                    Assert.Equal("group:Memoria", selectedHeader.Tag);
                    Assert.Contains(rows, selectedHeader.GetVisualAncestors());
                    Button expand = Assert.Single(((Control)selectedHeader.Content!).GetLogicalDescendants().OfType<Button>());
                    expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    host.UpdateLayout();
                    Assert.Equal(32, rows.ItemCount);
                }
                finally
                {
                    host.Close();
                    window.Close();
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DownloadsShowExactJobAndExposeFullDiagnosticsAfterClearingFilters()
    {
        await fixture.Session.Dispatch(async () =>
        {
            string root = Path.Combine(Path.GetTempPath(), "launcher-mod-downloads-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                using ModRuntime mods = new(root, watch: false);
                ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
                using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", true);
                await library.StopAsync();
                RemoteModEntry entry = new("external", Guid.NewGuid(), "Test mod", "1") { Author = "Test author", Category = "Graphics" };
                string error = string.Join("\n", Enumerable.Repeat("The transfer failed. Retry or open the download page.", 80)) + "\nFinal diagnostic line.";
                ModLibraryJob failed = new(Guid.NewGuid(), entry, "Failed", 0, null, 0, error, "Transfer diagnostic", false, false, false, false, true);
                ModLibraryJob recent = new(Guid.NewGuid(), entry with { Version = "2" }, "Cancelled", 0, null, 0, null, null, false, false, false, false, false);
                ModLibrarySnapshot snapshot = new(1, new([entry], null, false, false, "Catalog refresh failed."), [recent, failed], "Classic/en/test", true, "Library update failed.");
                typeof(ModLibraryService).GetField("view", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(library, snapshot);
                LauncherWindow window = new();
                try
                {
                    Set(window, "launcherMods", mods);
                    Set(window, "launcherModLibrary", library);
                    Get<TabControl>(window, "launcherModTabs").SelectedIndex = 2;
                    Render(window);

                    ListBox rows = Get<ListBox>(window, "launcherModRows");
                    Assert.Equal(2, rows.ItemCount);
                    Assert.Equal("job:" + failed.Id, Assert.IsType<ListBoxItem>(rows.SelectedItem).Tag);
                    Assert.Contains(error, Get<string>(window, "launcherModDetailText"));
                    Assert.Contains("Transfer diagnostic", Get<string>(window, "launcherModDetailText"));
                    Assert.Contains("Catalog refresh failed.", Get<string>(window, "launcherModIssueText"));
                    Assert.Contains("Library update failed.", Get<string>(window, "launcherModIssueText"));
                    Control panel = Assert.IsAssignableFrom<Control>(Assert.IsType<TabItem>(Get<TabControl>(window, "launcherPages").Items[1]).Content);
                    // Leave room for the launcher header and its vertical navigation at 1280 × 720.
                    panel.Measure(new Size(1100, 610));
                    panel.Arrange(new Rect(0, 0, 1100, 610));
                    ScrollViewer details = Get<ScrollViewer>(window, "launcherModDetailScroll");
                    Assert.True(rows.Bounds.Width > 200 && details.Bounds.Width > 300);
                    Assert.True(rows.Bounds.Height > 320 && details.Bounds.Height > 250);
                    Assert.True(rows.Bounds.Right <= ((Grid)rows.Parent!).Bounds.Width);
                    Control detailContent = Assert.IsAssignableFrom<Control>(details.Content);
                    detailContent.Measure(new Size(details.Bounds.Width, double.PositiveInfinity));
                    Assert.True(detailContent.DesiredSize.Height > details.Bounds.Height);
                    Assert.True(Get<Button>(window, "launcherModExpand").IsEnabled);

                    Button next = Get<Button>(window, "launcherModNext");
                    next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal("job:" + recent.Id, Assert.IsType<ListBoxItem>(rows.SelectedItem).Tag);
                    Assert.DoesNotContain("Final diagnostic line.", Get<string>(window, "launcherModDetailText"));

                    TextBox search = Get<TextBox>(window, "launcherModSearch");
                    search.Text = "Test author";
                    Render(window);
                    Assert.Equal(2, rows.ItemCount);
                    ComboBox filter = Get<ComboBox>(window, "launcherModFilter");
                    filter.SelectedIndex = 4;
                    Assert.Equal(1, rows.ItemCount);
                    search.Text = "No such mod";
                    Render(window);
                    Assert.Empty(rows.Items);
                    Assert.False(Get<Button>(window, "launcherModExpand").IsEnabled);
                    Get<Button>(window, "launcherModClearFilters").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Empty(search.Text!);
                    Assert.Equal(0, filter.SelectedIndex);
                    Assert.Equal(2, rows.ItemCount);
                    Assert.Equal("job:" + failed.Id, Assert.IsType<ListBoxItem>(rows.SelectedItem).Tag);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    private static T Get<T>(LauncherWindow window, string field) => (T)typeof(LauncherWindow).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;

    private static void Set(LauncherWindow window, string field, object value)
    {
        typeof(LauncherWindow).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);
    }

    private static void Render(LauncherWindow window)
    {
        typeof(LauncherWindow).GetMethod("RenderLauncherMods", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
    }
}
