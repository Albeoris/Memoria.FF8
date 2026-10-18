using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTutorialBrowserTests
{
    [Fact]
    public void NativeBrowserUsesOriginalCategoriesAndUnlocksWithoutLiveActionDispatch()
    {
        using GameAssets assets = InstalledAssets();
        NativeTutorialCatalog tutorials = assets.ReadNativeTutorialCatalog();
        NativeTutorialBrowserCatalog browser = assets.ReadNativeTutorialBrowserCatalog();
        HashSet<int> unlocked = [62, 66, 70];
        List<int> started = [];
        List<MenuAction> live = [];
        MenuController menu = new(() => new MenuData(), live.Add);
        menu.ConnectNativeTutorials(tutorials, browser, () => unlocked, started.Add);
        menu.Open("tutorial");
        Assert.Equal(browser.Categories.Select(category => category.Title), menu.Entries.Select(entry => entry.Label));
        Assert.DoesNotContain("seed-test", menu.ScreenIds);
        Assert.DoesNotContain("tutorial-seed", menu.ScreenIds);
        Assert.DoesNotContain("tutorial-junction", menu.ScreenIds);
        Assert.False(menu.Entries.Single(entry => entry.Id == "category-3").Enabled);
        menu.Navigate(MenuInput.Down);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("tutorial-native-online-help", menu.ScreenId);
        Assert.Equal(new[] { "tutorial-0", "tutorial-4", "tutorial-8" }, menu.Entries.Select(entry => entry.Id));
        menu.Navigate(MenuInput.Down);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(new[] { 4 }, started);
        Assert.Empty(live);
        Assert.Throws<InvalidOperationException>(() => menu.Execute(new MenuAction("native-tutorial-start", "1")));
        Assert.Equal(new[] { 4 }, started);
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal("tutorial", menu.ScreenId);
        Assert.Equal(1, menu.SelectedIndex);
        menu.Navigate(MenuInput.Cancel);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void OriginalStaticPagesKeepEveryTextAndNativePagingReturnsToTheBrowser()
    {
        using GameAssets assets = InstalledAssets();
        NativeTutorialBrowserCatalog browser = assets.ReadNativeTutorialBrowserCatalog();
        MenuController menu = new();
        menu.ConnectNativeTutorials(assets.ReadNativeTutorialCatalog(), browser, () => new HashSet<int> { 62 }, _ => throw new InvalidOperationException("A static page cannot start a demo."));
        foreach (NativeTutorialBrowserCategory category in browser.Categories.Where(category => category.Pages.Length > 0))
        {
            menu.Open("tutorial");
            menu.SelectEntry("tutorial", $"category-{category.Id}");
            menu.Navigate(MenuInput.Confirm);
            Assert.Equal(category.Pages[0], menu.CurrentNativeTutorialPage);
            menu.Navigate(MenuInput.Left);
            Assert.Equal(category.Pages[^1], menu.CurrentNativeTutorialPage);
            menu.Navigate(MenuInput.Right);
            Assert.Equal(category.Pages[0], menu.CurrentNativeTutorialPage);
            foreach (NativeTutorialBrowserPage page in category.Pages)
            {
                Assert.Equal(page, menu.CurrentNativeTutorialPage);
                UiView view = menu.BuildView();
                UiImage image = Descendants(view.Root).OfType<UiImage>().Single(image => image.Source?.Kind == UiImageKind.NativeTutorialPage);
                Assert.Equal(page.Id, image.Source?.Id);
                Assert.Equal(384f / 256f, image.Bounds.Width / image.Bounds.Height, precision: 4);
                var labels = Descendants(view.Root).OfType<NativeTutorialTextElement>().ToArray();
                Assert.Equal(page.Placements.Length, labels.Length);
                foreach (NativeTutorialPageText placement in page.Placements)
                {
                    NativeTutorialTextElement label = labels.Single(label => label.Id == $"native-tutorial-text-{placement.TextIndex}");
                    Assert.False(string.IsNullOrWhiteSpace(label.Text));
                    Assert.Equal((24 + placement.X) / 384f, label.Anchors.Left);
                    Assert.Equal((8 + placement.Y) / 256f, label.Anchors.Top);
                    Assert.Equal(image.Bounds.X + image.Bounds.Width * label.Anchors.Left, label.Bounds.X, precision: 3);
                    Assert.Equal(image.Bounds.Y + image.Bounds.Height * label.Anchors.Top, label.Bounds.Y, precision: 3);
                    Assert.DoesNotContain("{Line}", label.Text);
                }
                Assert.Equal(page.Placements.Select(placement => placement.Value), menu.CurrentScreen.Details(menu));
                menu.Navigate(MenuInput.Confirm);
            }
            Assert.Equal("tutorial", menu.ScreenId);
            Assert.Null(menu.CurrentNativeTutorialPage);
            Assert.Equal(category.Id, menu.SelectedIndex);
        }
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static GameAssets InstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "Data", "lang-en", "menu.fs")), "Installed original tutorial resources are required.");
        return new GameAssets(directory);
    }
}
