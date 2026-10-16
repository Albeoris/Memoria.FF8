using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MenuSecondaryLayoutTests
{
    [Fact]
    public void ConfigKeepsNamesAndValuesOnTheSamePageAndAcceptsValueColumnClicks()
    {
        var controller = new MenuController();
        controller.Open("config", demo: true);
        for (int index = 0; index < 16; index++)
        {
            controller.Navigate(MenuInput.Down);
        }
        UiView view = controller.BuildView();
        UiList commands = Descendants(view.Root).OfType<UiList>().Single(list => list.Id == "commands");
        Assert.True(commands.Paginated);
        Assert.True(commands.ScrollOffset > 0);
        int clicked = commands.ScrollOffset;
        string setting = controller.Entries[clicked].Id;
        string before = controller.Data.Settings[setting];
        Assert.Equal(controller.Entries.Count, commands.SecondaryItems.Count);
        view.Pointer(commands.Bounds.Right - 60, commands.Bounds.Y + 10, true);
        Assert.Equal(clicked, controller.SelectedIndex);
        Assert.NotEqual(before, controller.Data.Settings[setting]);
        controller.Navigate(MenuInput.Left);
        Assert.Equal(clicked, controller.SelectedIndex);
    }

    [Fact]
    public void Save_cards_keep_supplied_metadata_and_selected_card_activation()
    {
        var controller = new MenuController();
        controller.Open("save", demo: true);
        controller.Navigate(MenuInput.Down);
        UiView view = controller.BuildView();
        MenuSaveSlot slot = controller.Data.SaveSlots[1];
        Assert.Equal(slot.Id, view.FocusedElement?.Id);
        Assert.Contains(Descendants(view.Root).OfType<UiLabel>(), label => label.Text == slot.Summary);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("save-confirm", controller.ScreenId);
        Assert.Equal(slot.Id, controller.SaveSlot?.Id);
    }

    [Theory]
    [InlineData("config")]
    [InlineData("save")]
    [InlineData("load")]
    [InlineData("shop-buy")]
    [InlineData("ability-refine")]
    [InlineData("tutorial")]
    [InlineData("seed-test")]
    public void Secondary_pages_omit_host_footer_and_keep_controls_in_design_bounds(string screen)
    {
        var controller = new MenuController();
        controller.Open(screen, demo: true);
        UiView view = controller.BuildView();
        Assert.DoesNotContain(Descendants(view.Root).OfType<UiLabel>(), label => label.Text.Contains("Confirm:", StringComparison.Ordinal) && label.Text.Contains("Menu:", StringComparison.Ordinal));
        Assert.All(Descendants(view.Root), element =>
        {
            Assert.True(float.IsFinite(element.Bounds.Width));
            Assert.True(element.Bounds.Width >= 0);
            Assert.True(element.Bounds.Height >= 0);
        });
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
