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
