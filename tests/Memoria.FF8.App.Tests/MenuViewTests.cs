using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MenuViewTests
{
    [Fact]
    public void Every_registered_view_draws_with_balanced_clipping_at_small_and_large_viewports()
    {
        var menus = new MenuController();
        foreach (string screen in menus.ScreenIds)
        {
            menus.Open(screen, true);
            foreach (var (width, height) in new[] { (640, 448), (1920, 1080) })
            {
                UiView view = menus.BuildView();
                view.Resize(width, height);
                var canvas = new ValidationCanvas();
                view.Draw(canvas, new UiTheme());
                Assert.Equal(0, canvas.ClipDepth);
                Assert.True(canvas.TextCount > 0, screen);
            }
        }
    }

    [Fact]
    public void Focused_portrait_tiles_remain_clickable_and_confirm_uses_the_selected_tile()
    {
        var menus = new MenuController();
        menus.Open("gf", true);
        UiView first = menus.BuildView();
        first.Resize(960, 672);
        var tile = first.Root.Children.SelectMany(Descendants).OfType<UiButton>().First();
        first.Pointer(tile.Bounds.X + tile.Bounds.Width / 2, tile.Bounds.Y + tile.Bounds.Height / 2, true);
        Assert.Equal("gf-detail", menus.ScreenId);
        menus.Open("gf", true);
        menus.Navigate(MenuInput.Right);
        UiView second = menus.BuildView();
        second.Resize(960, 672);
        Assert.Equal(menus.Entries[1].Id, second.FocusedElement?.Id);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("Shiva", menus.GuardianForce?.Name);
    }

    [Fact]
    public void Mods_can_supply_controls_and_are_discovered_in_the_gallery()
    {
        var menus = new MenuController();
        bool invoked = false;
        menus.Register(new MenuScreenDefinition
        {
            Id = "mod-settings",
            Title = "Mod settings",
            Help = "Custom controls",
            MainCommand = true,
            BuildContent = (_, root) => root.Children.Add(new UiButton { Id = "mod-action", Width = 300, Height = 60, HorizontalAlignment = UiAlignment.Center, VerticalAlignment = UiAlignment.Center, Text = "Apply", Click = () => invoked = true })
        });
        menus.Open("debug-room", true);
        Assert.Contains(menus.Entries, entry => entry.Destination == "mod-settings");
        menus.Open("main", true);
        Assert.Contains(menus.Entries, entry => entry.Destination == "mod-settings");
        menus.Open("mod-settings", true);
        menus.BuildView().Resize(960, 672);
        menus.Navigate(MenuInput.Confirm);
        Assert.True(invoked);
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

    private sealed class ValidationCanvas : IUiCanvas
    {
        public int ClipDepth { get; private set; }
        public int TextCount { get; private set; }

        public void Fill(UiRect bounds, UiColor color)
        {
            Validate(bounds);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Validate(bounds);
            Assert.True(float.IsFinite(fontSize) && fontSize > 0);
            TextCount++;
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
            Assert.True(float.IsFinite(x1) && float.IsFinite(y1) && float.IsFinite(x2) && float.IsFinite(y2));
            Assert.True(float.IsFinite(thickness) && thickness > 0);
        }

        public void PushClip(UiRect bounds)
        {
            Validate(bounds);
            ClipDepth++;
        }

        public void PopClip()
        {
            ClipDepth--;
            Assert.True(ClipDepth >= 0);
        }

        private static void Validate(UiRect bounds)
        {
            Assert.True(float.IsFinite(bounds.X) && float.IsFinite(bounds.Y) && float.IsFinite(bounds.Width) && float.IsFinite(bounds.Height));
            Assert.True(bounds.Width >= 0 && bounds.Height >= 0);
        }
    }
}
