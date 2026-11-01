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
        menu.Open("main", demo: true);
        menu.Push("junction-options");
        menu.Execute(new("junction-ui", "manage-gfs"));
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
        bool dashboard = menu.ScreenId == "junction-dashboard";
        Assert.Contains(Descendants(vector.Root), element => element.Id == (dashboard ? "junction-dashboard" : vectorId));
        Assert.Equal(dashboard ? 1280f : 960f, vector.DesignWidth);
        Assert.Equal(dashboard ? 720f : 540f, vector.DesignHeight);
        menu.Presentation = MenuPresentation.Classic;
        UiView classic = menu.BuildView();
        Assert.DoesNotContain(Descendants(classic.Root), element => element.Id == vectorId);
        Assert.Equal(dashboard ? 1280f : 960f, classic.DesignWidth);
        Assert.Equal(dashboard ? 720f : 672f, classic.DesignHeight);
        Assert.IsType<UiTheme>(menu.CreateTheme());
    }

    public static IEnumerable<object[]> ResponsiveScreens()
    {
        foreach (string screen in new[] { "main", "junction-magic", "config-interface", "item", "status", "junction-command" })
        {
            foreach (int width in new[] { 1920, 2560 })
            {
                yield return new object[] { screen, width, 1080 };
            }
        }
    }

    [Theory]
    [MemberData(nameof(ResponsiveScreens))]
    public void Vector_content_reaches_screen_edges_with_uniform_safe_margins(string screen, int width, int height)
    {
        MenuController menu = new();
        menu.Open(screen, demo: true);
        UiView view = menu.BuildView();
        view.Resize(width, height);

        Assert.Equal(UiViewportMode.Expand, view.ViewportMode);
        UiPanel root = Assert.IsType<UiPanel>(view.Root);
        float padding = menu.ScreenId == "junction-dashboard" ? 16 : 26;
        Assert.Equal(new UiThickness(padding), root.Padding);
        float scale = Math.Min(width / view.DesignWidth, height / view.DesignHeight);
        Assert.Equal(width, root.Bounds.Width * scale, 3);
        Assert.Equal(height, root.Bounds.Height * scale, 3);
        UiAnchorPanel anchored = Assert.IsType<UiAnchorPanel>(Assert.Single(root.Children));
        UiElement content = anchored.Children[0];
        Assert.Equal(padding * scale, content.Bounds.X * scale, 3);
        Assert.Equal(padding * scale, content.Bounds.Y * scale, 3);
        Assert.Equal(width - padding * scale, content.Bounds.Right * scale, 3);
        Assert.Equal(height - padding * scale, content.Bounds.Bottom * scale, 3);

        menu.Presentation = MenuPresentation.Classic;
        UiView classic = menu.BuildView();
        classic.Resize(width, height);
        Assert.Equal(UiViewportMode.Fit, classic.ViewportMode);
        Assert.Equal(classic.DesignWidth, classic.Root.Bounds.Width);
        Assert.Equal(classic.DesignHeight, classic.Root.Bounds.Height);
        float classicScale = Math.Min(width / classic.DesignWidth, height / classic.DesignHeight);
        Assert.True(classic.Root.Bounds.Width * classicScale <= width);
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
                Assert.Equal(entries.Select(entry => entry.Id), menu.Entries.Select(entry => entry.Id));
                // Font configuration is skin-specific; gameplay commands retain their complete contracts.
                Assert.Equal(entries.Where(entry => entry.Id != "UI font"), menu.Entries.Where(entry => entry.Id != "UI font"));
                Assert.Equal(navigation, JsonSerializer.Serialize(menu.CaptureNavigation()));
                Assert.Equal(presentation == MenuPresentation.Vector ? UiViewportMode.Expand : UiViewportMode.Fit, view.ViewportMode);
                if (presentation == MenuPresentation.Vector)
                {
                    Assert.Equal(new UiThickness(menu.ScreenId == "junction-dashboard" ? 16 : screen == "magazine" ? 0 : 26), Assert.IsType<UiPanel>(view.Root).Padding);
                }
                Assert.Equal(menu.ScreenId == "junction-dashboard" ? 1280f : 960f, view.DesignWidth);
                Assert.Equal(menu.ScreenId == "junction-dashboard" ? 720f : screen == "magazine" ? 720f : presentation == MenuPresentation.Vector && (screen == "main" || screen.StartsWith("junction", StringComparison.Ordinal) && screen is not ("junction-command" or "junction-ability" or "junction-command-choice" or "junction-ability-choice")) ? 540f : 672f, view.DesignHeight);
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
        Assert.Equal(UiViewportMode.Fit, custom.ViewportMode);
        Assert.Same(provider.Theme, menu.CreateTheme());
        Assert.Equal(navigation, JsonSerializer.Serialize(menu.CaptureNavigation()));
        Assert.Same(custom, menu.BuildView());
        Assert.Equal(1, provider.ComposeCount);
        Assert.Throws<ArgumentNullException>(() => menu.PresentationProvider = null!);
    }

    [Fact]
    public void Active_character_identity_and_focus_survive_presentation_changes()
    {
        MenuController menu = new();
        menu.Open("main", demo: true);
        menu.SelectEntry("main", "junction");
        menu.Navigate(MenuInput.Confirm);
        Assert.True(menu.IsSelectingCharacter);
        string selected = menu.CharacterChoices.First(character => character.InParty).Id;
        menu.SelectCharacter(selected);
        string navigation = JsonSerializer.Serialize(menu.CaptureNavigation());
        foreach (MenuPresentation presentation in new[] { MenuPresentation.Vector, MenuPresentation.Classic, MenuPresentation.Vector })
        {
            menu.Presentation = presentation;
            UiView view = menu.BuildView();
            Assert.Equal(selected, view.FocusedElement?.Id);
            Assert.Contains(Descendants(view.Root).OfType<UiButton>(), button => button.Id == selected);
            Assert.Equal(navigation, JsonSerializer.Serialize(menu.CaptureNavigation()));
        }
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("junction-dashboard", menu.ScreenId);
        Assert.Equal(selected, menu.Character?.Id);
    }

    [Fact]
    public void StockPickerAndSemanticFocusSurvivePresentationChangesWithoutMutatingAssignments()
    {
        MenuController menu = new();
        menu.Open("junction-magic", demo: true);
        menu.Execute(new("junction-ui", "slot-Str"));
        string candidate = menu.Entries.First(entry => entry.Id.StartsWith("candidate-", StringComparison.Ordinal) && entry.Id is not ("candidate-previous" or "candidate-next")).Id;
        menu.SelectEntry("junction-dashboard", candidate);
        Dictionary<string, int> stats = new(menu.Character!.Stats);
        Dictionary<string, string> junctions = new(menu.Character.Junctions);
        string navigation = JsonSerializer.Serialize(menu.CaptureNavigation());
        foreach (MenuPresentation presentation in new[] { MenuPresentation.Classic, MenuPresentation.Vector })
        {
            menu.Presentation = presentation;
            UiView view = menu.BuildView();
            Assert.Equal("junction-dashboard", menu.ScreenId);
            Assert.Equal(candidate, view.FocusedElement?.Id);
            Assert.Contains(menu.Entries, entry => entry.Id == candidate);
            Assert.Equal(navigation, JsonSerializer.Serialize(menu.CaptureNavigation()));
            Assert.Equal(stats, menu.Character.Stats);
            Assert.Equal(junctions, menu.Character.Junctions);
            Assert.Equal(19, Descendants(view.Root).Count(element => element.Id?.StartsWith("slot-", StringComparison.Ordinal) == true));
        }
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
