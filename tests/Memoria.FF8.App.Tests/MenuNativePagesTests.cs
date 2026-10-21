using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MenuNativePagesTests
{
    [Fact]
    public void GfRosterUsesSharedPortraitPanelAndPreservesTileActivation()
    {
        MenuController menus = new();
        menus.Open("gf", true);
        UiView view = menus.BuildView();
        UiButton[] buttons = Descendants(view.Root).OfType<UiButton>().ToArray();
        Assert.Equal(Math.Min(16, menus.Entries.Count), buttons.Length);
        Assert.All(buttons, button => Assert.False(button.DrawBackground));
        Assert.All(buttons, button => Assert.False(button.HighlightSelection));
        Assert.DoesNotContain(Descendants(view.Root).OfType<UiLabel>(), label => label.Text == menus.Entries[0].Label);
        UiButton second = buttons[1];
        view.Pointer(second.Bounds.X + second.Bounds.Width / 2, second.Bounds.Y + second.Bounds.Height / 2, true);
        Assert.Equal("gf-detail", menus.ScreenId);
        Assert.Equal(menus.Data.GuardianForces[1].Id, menus.GuardianForce?.Id);
    }

    [Fact]
    public void CardRosterHasTextCollectionAndOneSelectedArtwork()
    {
        MenuController menus = new();
        menus.Open("card", true);
        UiView view = menus.BuildView();
        UiList list = Assert.Single(Descendants(view.Root).OfType<UiList>(), element => element.Id == "commands");
        UiImage image = Assert.Single(Descendants(view.Root).OfType<UiImage>(), element => element.Source?.Kind == UiImageKind.Card);
        Assert.True(image.Bounds.X > list.Bounds.X);
        Assert.Contains(menus.Data.Cards[0].Name, list.Items[0]);
        Assert.False(list.HighlightSelection);
        Assert.Empty(Descendants(view.Root).OfType<UiButton>());
    }

    [Fact]
    public void GfLearningRetainsDisabledCompletedAbilityAndProgress()
    {
        MenuData data = new();
        MenuGuardianForce force = new() { Id = "gf", Name = "Quezacotl", Learning = "Card" };
        force.LearningChoices.Add(new MenuAbilityChoice("magic", "Magic", true, 20, 20));
        force.LearningChoices.Add(new MenuAbilityChoice("card", "Card", false, 7, 40));
        data.GuardianForces.Add(force);
        MenuController menus = new(() => data);
        menus.Open("gf-learn");
        UiView view = menus.BuildView();
        UiList list = Assert.Single(Descendants(view.Root).OfType<UiList>(), element => element.Id == "commands");
        Assert.False(menus.Entries[0].Enabled);
        Assert.True(menus.Entries[1].Enabled);
        Assert.Empty(list.EnabledItems);
        Assert.Equal("Card", list.Items[1]);
        Assert.Equal(new[] { "Complete!", "7 / 40" }, list.SecondaryItems);
        Assert.True(list.Paginated);
        Assert.Equal(10, list.PageSize);
        Assert.Contains(Descendants(view.Root).OfType<UiLabel>(), label => label.Text.Split('\n').Contains("7 / 40 AP"));
    }

    [Fact]
    public void GfPagesPreserveRowsClampTheLastPageAndKeepLearningActionsBoundToTheirAbility()
    {
        MenuData data = new();
        data.SupportedActions.Add("gf-learn");
        MenuGuardianForce force = new() { Id = "gf", Name = "Quezacotl" };
        for (int index = 0; index < 22; index++)
        {
            force.LearningChoices.Add(new MenuAbilityChoice(index.ToString(), $"Ability {index}", false, index, 100));
        }
        data.GuardianForces.Add(force);
        List<MenuAction> actions = [];
        MenuController menus = new(() => data, actions.Add);
        menus.Open("gf-learn");
        for (int index = 0; index < 5; index++)
        {
            menus.Navigate(MenuInput.Down);
        }
        menus.Navigate(MenuInput.Right);
        Assert.Equal(15, menus.SelectedIndex);
        UiList list = Assert.IsType<UiList>(menus.BuildView().FocusedElement);
        Assert.Equal(10, list.ScrollOffset);
        Assert.Equal(1, list.CurrentPage);
        Assert.Equal("15 / 100", list.SecondaryItems[15]);
        menus.Navigate(MenuInput.Right);
        Assert.Equal(21, menus.SelectedIndex);
        menus.Navigate(MenuInput.Left);
        Assert.Equal(11, menus.SelectedIndex);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("11", Assert.Single(actions).SubjectId);
    }

    [Theory]
    [InlineData("item-use")]
    [InlineData("magic-use")]
    [InlineData("card")]
    [InlineData("ability-refine")]
    public void VerticalMenuListsChangePagesWithLeftAndRight(string screen)
    {
        MenuController menus = new();
        menus.Open(screen, true);
        UiList list = Assert.IsType<UiList>(menus.BuildView().FocusedElement);
        Assert.True(list.Paginated);
        int size = list.VisibleRows;
        menus.Navigate(MenuInput.Right);
        Assert.Equal(list.PageCount > 1 ? Math.Min(size, menus.Entries.Count - 1) : 0, menus.SelectedIndex);
        menus.Navigate(MenuInput.Left);
        Assert.Equal(0, menus.SelectedIndex);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("status-stats")]
    [InlineData("status-info")]
    public void StatusUsesProjectedStatsAndSeparateEquipmentPanels(string screen)
    {
        MenuData data = new();
        MenuCharacter character = new() { Id = "squall", Name = "Squall", InParty = true, Weapon = "Revolver" };
        character.Stats["Str"] = 37;
        character.Commands.Add("Attack");
        data.Characters.Add(character);
        MenuController menus = new(() => data);
        menus.Open(screen);
        UiElement[] elements = Descendants(menus.BuildView().Root).ToArray();
        Assert.Contains(elements.OfType<UiLabel>(), label => label.Text == "37");
        Assert.Contains(elements.OfType<UiPanel>(), panel => panel.Id == "status-weapon");
        Assert.Contains(elements.OfType<UiPanel>(), panel => panel.Caption == "ABILITY");
        Assert.DoesNotContain(elements.OfType<UiLabel>(), label => label.Text.Contains("Confirm:", StringComparison.Ordinal));
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
}
