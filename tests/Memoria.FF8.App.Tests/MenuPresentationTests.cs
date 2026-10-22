using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MenuPresentationTests
{
    [Fact]
    public void Default_is_vector_and_switching_preserves_navigation_and_selected_command()
    {
        MenuController menu = new();
        Assert.Equal(MenuPresentation.Vector, menu.Presentation);
        Assert.IsType<VectorUiTheme>(menu.CreateTheme());
        menu.Open("junction-options", demo: true);
        menu.SelectEntry("junction-options", "junction-gf");
        menu.Navigate(MenuInput.Confirm);
        menu.Navigate(MenuInput.Down);
        MenuNavigationState navigation = menu.CaptureNavigation();
        Assert.NotEmpty(navigation.History);
        string saved = JsonSerializer.Serialize(navigation);
        string? selected = menu.SelectedEntry?.Id;
        UiView vector = menu.BuildView();
        foreach (MenuPresentation presentation in new[] { MenuPresentation.Classic, MenuPresentation.Vector })
        {
            menu.Presentation = presentation;
            UiView rebuilt = menu.BuildView();
            Assert.NotSame(vector, rebuilt);
            Assert.Equal(saved, JsonSerializer.Serialize(menu.CaptureNavigation()));
            Assert.Equal(selected, menu.SelectedEntry?.Id);
            Assert.True(menu.IsOpen);
            Assert.True(menu.IsDemo);
        }
    }

    [Theory]
    [InlineData("main", "vector-main")]
    [InlineData("junction", "vector-junction")]
    [InlineData("junction-options", "vector-junction")]
    [InlineData("junction-magic", "vector-junction")]
    public void Main_and_junction_compose_in_the_selected_design_space(string screen, string vectorId)
    {
        MenuController menu = new();
        menu.Open(screen, demo: true);
        UiView vector = menu.BuildView();
        Assert.Contains(Descendants(vector.Root), element => element.Id == vectorId);
        Assert.Equal(960f, vector.DesignWidth);
        Assert.Equal(540f, vector.DesignHeight);
        menu.Presentation = MenuPresentation.Classic;
        UiView classic = menu.BuildView();
        Assert.DoesNotContain(Descendants(classic.Root), element => element.Id == vectorId);
        Assert.Equal(960f, classic.DesignWidth);
        Assert.Equal(672f, classic.DesignHeight);
        Assert.IsType<UiTheme>(menu.CreateTheme());
    }

    [Fact]
    public void Both_presentations_build_registered_screens_with_the_same_command_contracts()
    {
        foreach (string screen in new MenuController().ScreenIds)
        {
            MenuController menu = new();
            menu.Open(screen, demo: true);
            if (screen == "magazine")
            {
                MagazineMenuFixture.AddOwnedPage(menu);
            }
            MenuEntry[] entries = menu.Entries.ToArray();
            string navigation = JsonSerializer.Serialize(menu.CaptureNavigation());
            foreach (MenuPresentation presentation in new[] { MenuPresentation.Vector, MenuPresentation.Classic })
            {
                menu.Presentation = presentation;
                UiView view = menu.BuildView();
                view.Resize(1280, 720);
                Assert.Equal(entries, menu.Entries.ToArray());
                Assert.Equal(navigation, JsonSerializer.Serialize(menu.CaptureNavigation()));
                Assert.Equal(960f, view.DesignWidth);
                Assert.Equal(screen == "magazine" ? 720f : presentation == MenuPresentation.Vector && (screen == "main" || screen.StartsWith("junction", StringComparison.Ordinal)) ? 540f : 672f, view.DesignHeight);
            }
        }
    }
    [Fact]
    public void Replacing_custom_provider_recomposes_the_view_and_theme_without_navigation_changes()
    {
        MenuController menu = new();
        menu.Open("main", demo: true);
        string navigation = JsonSerializer.Serialize(menu.CaptureNavigation());
        UiView original = menu.BuildView();
        TestPresentation provider = new();
        menu.PresentationProvider = provider;
        UiView custom = menu.BuildView();
        Assert.NotSame(original, custom);
        Assert.Contains(Descendants(custom.Root), element => ReferenceEquals(element, provider.Content));
        Assert.Equal(600f, custom.DesignHeight);
        Assert.Same(provider.Theme, menu.CreateTheme());
        Assert.Equal(navigation, JsonSerializer.Serialize(menu.CaptureNavigation()));
        Assert.Same(custom, menu.BuildView());
        Assert.Equal(1, provider.ComposeCount);
        Assert.Throws<ArgumentNullException>(() => menu.PresentationProvider = null!);
    }

    private sealed class TestPresentation : IMenuPresentation
    {
        public UiElement Content { get; } = new UiPanel { Id = "custom-presentation" };
        public UiTheme Theme { get; } = new();
        public int ComposeCount { get; private set; }
        public float DesignHeight(MenuController controller) => 600;
        public UiTheme CreateTheme(float textScale) => Theme;
        public UiElement Compose(MenuController controller)
        {
            ComposeCount++;
            return Content;
        }
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
