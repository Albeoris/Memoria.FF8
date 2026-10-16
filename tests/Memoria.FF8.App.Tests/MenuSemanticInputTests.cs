using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MenuSemanticInputTests
{
    [Fact]
    public void Selection_tracks_identity_after_entries_are_reordered_and_renders_before_confirmation()
    {
        MenuController menu = new();
        menu.Open("junction-options", demo: true);
        MenuScreenDefinition definition = menu.CurrentScreen;
        MenuEntry[] reordered = definition.Entries(menu).Reverse().ToArray();
        menu.Register(new MenuScreenDefinition { Id = definition.Id, Title = definition.Title, Help = definition.Help, Layout = definition.Layout, Entries = _ => reordered });
        menu.SelectEntry("junction-options", "junction-gf");
        Assert.Equal("junction-options", menu.ScreenId);
        Assert.Equal("junction-gf", menu.SelectedEntry?.Id);
        UiList commands = Descendants(menu.BuildView().Root).OfType<UiList>().First(list => list.Id == "commands");
        Assert.Equal(menu.SelectedIndex, commands.SelectedIndex);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("junction-gf", menu.ScreenId);
    }

    [Fact]
    public void Guardian_force_artwork_leaves_the_selected_pointer_visible()
    {
        MenuController menu = new();
        menu.Open("gf", demo: true);
        UiView view = menu.BuildView();
        UiButton selected = Assert.IsType<UiButton>(view.FocusedElement);
        UiImage portrait = Descendants(selected).OfType<UiImage>().Single();
        Assert.True(portrait.Bounds.X >= selected.Bounds.X + 7 + selected.PointerSize);
    }

    [Fact]
    public void Missing_or_wrong_screen_targets_fail_before_any_command_is_activated()
    {
        MenuController menu = new();
        menu.Open("junction", demo: true);
        Assert.Throws<InvalidOperationException>(() => menu.SelectEntry("main", "junction"));
        Assert.Throws<InvalidOperationException>(() => menu.SelectEntry("junction", "unknown-command"));
        Assert.Equal("junction", menu.ScreenId);
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
