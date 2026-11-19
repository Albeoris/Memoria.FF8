using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Memoria.FF8.Mods;

namespace Memoria.Launcher.Tests;

[Collection("Incident dialog UI")]
public sealed class LauncherModPresentationTests(LauncherHeadlessFixture fixture)
{
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
                RemoteModEntry external = new("external", Guid.NewGuid(), "A third-party mod", "1");
                RemoteModEntry official = new(OfficialModCatalog.SourceId, OfficialModCatalog.GirlNextDoor.CatalogId, "Girl Next Door", "1") { Official = OfficialModCatalog.GirlNextDoor };
                ModLibrarySnapshot snapshot = new(1, new([external, official], null, false, false, null), [], "Classic/en/test", true);
                typeof(ModLibraryService).GetField("view", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(library, snapshot);
                LauncherWindow window = new();
                try
                {
                    Set(window, "launcherMods", mods);
                    Set(window, "launcherModLibrary", library);
                    Get<TabControl>(window, "launcherModTabs").SelectedIndex = 1;
                    Render(window);
                    ListBox rows = Get<ListBox>(window, "launcherModRows");
                    ListBoxItem[] collapsed = rows.Items.Cast<ListBoxItem>().ToArray();
                    Assert.Equal(2, collapsed.Length);
                    Assert.Equal("group:Memoria", collapsed[0].Tag);
                    Image crest = Assert.Single(((Control)collapsed[0].Content!).GetLogicalDescendants().OfType<Image>());
                    Assert.NotNull(crest.Source);
                    GridSplitter splitter = Assert.Single(((Grid)rows.Parent!).Children.OfType<GridSplitter>());
                    Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(splitter.Background).Color);
                    Button toggle = Assert.Single(((Control)collapsed[0].Content!).GetLogicalDescendants().OfType<Button>());
                    Assert.Equal("\u25B8", toggle.Content);
                    toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(3, rows.ItemCount);
                    ListBoxItem component = rows.Items.Cast<ListBoxItem>().ElementAt(1);
                    Assert.Equal(official.SourceId + ":" + official.Id, component.Tag);
                    rows.SelectedItem = component;
                    Render(window);
                    Assert.Same(component, rows.SelectedItem);
                    Assert.DoesNotContain(Get<StackPanel>(window, "launcherModDetails").GetLogicalDescendants().OfType<TextBlock>(), text => text.Text?.Contains("Restart required", StringComparison.OrdinalIgnoreCase) == true);
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
