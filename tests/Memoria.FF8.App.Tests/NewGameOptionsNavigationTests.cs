using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NewGameOptionsNavigationTests
{
    [Theory]
    [InlineData(100)]
    [InlineData(150)]
    public void Down_reaches_begin_and_up_returns_across_every_page(int scale)
    {
        MenuController menus = new();
        menus.Open("new-game-options", demo: true);
        menus.Data.Settings["Text scale"] = scale + "%";
        string[] ids = menus.Entries.Select(entry => entry.Id).ToArray();
        foreach (string id in ids)
        {
            Assert.Equal(id, menus.SelectedEntry!.Id);
            Assert.Equal(id, menus.BuildView().FocusedElement!.Id);
            if (id != "begin")
            {
                menus.Navigate(MenuInput.Down);
            }
        }
        Assert.Equal("begin", menus.SelectedEntry!.Id);
        for (int index = ids.Length - 1; index > 0; index--)
        {
            menus.Navigate(MenuInput.Up);
            Assert.Equal(ids[index - 1], menus.SelectedEntry!.Id);
            Assert.Equal(ids[index - 1], menus.BuildView().FocusedElement!.Id);
        }
    }

    [Theory]
    [InlineData(MenuInput.PageUp, MenuInput.PageDown)]
    [InlineData(MenuInput.PageLeft, MenuInput.PageRight)]
    public void Page_buttons_reach_begin_and_return(MenuInput previous, MenuInput next)
    {
        MenuController menus = new();
        menus.Open("new-game-options", demo: true);
        for (int index = 0; index < menus.Entries.Count; index++)
        {
            menus.Navigate(next);
        }
        Assert.Equal("begin", menus.SelectedEntry!.Id);
        for (int index = 0; index < menus.Entries.Count; index++)
        {
            menus.Navigate(previous);
        }
        Assert.Equal("Difficulty", menus.SelectedEntry!.Id);
    }

    [Fact]
    public void Overflow_indicators_follow_hidden_rows_and_hardcore_decorations_keep_values_enabled()
    {
        MenuData data = new();
        data.SupportedActions.Add("config");
        data.Settings["Difficulty"] = "Hardcore";
        MenuController menus = new(() => data, _ => { });
        menus.Open("new-game-options");
        UiElement[] first = Descendants(menus.BuildView().Root).ToArray();
        UiOverflowArrows top = Assert.Single(first.OfType<UiOverflowArrows>());
        Assert.False(top.ShowUp);
        Assert.True(top.ShowDown);
        UiChoiceRow difficulty = first.OfType<UiChoiceRow>().Single(row => row.Id == "Difficulty");
        Assert.False(difficulty.ReadOnly);
        Assert.Contains(2, difficulty.LockedItems);
        UiChoiceRow locked = first.OfType<UiChoiceRow>().Single(row => row.Id == "Limit once per turn");
        Assert.True(locked.LabelLocked);
        Assert.True(locked.ReadOnly);
        Assert.True(locked.Enabled);
        for (int index = 0; index < menus.Entries.Count; index++)
        {
            menus.Navigate(MenuInput.PageDown);
        }
        UiOverflowArrows bottom = Assert.Single(Descendants(menus.BuildView().Root).OfType<UiOverflowArrows>());
        Assert.True(bottom.ShowUp);
        Assert.False(bottom.ShowDown);
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

    [Fact]
    public void Hardcore_difficulty_is_editable_before_begin_but_locked_in_config()
    {
        MenuData data = new();
        data.SupportedActions.Add("config");
        data.Settings["Difficulty"] = "Hardcore";
        List<MenuAction> applied = [];
        MenuController menus = new(() => data, applied.Add);
        menus.Open("new-game-options");
        Assert.True(menus.SelectedEntry!.Enabled);
        menus.Navigate(MenuInput.Left);
        Assert.Equal("Hard", Assert.Single(applied).Value);
        menus.Open("config-combat");
        Assert.False(menus.SelectedEntry!.Enabled);
        menus.Navigate(MenuInput.Left);
        Assert.Single(applied);
    }
}
