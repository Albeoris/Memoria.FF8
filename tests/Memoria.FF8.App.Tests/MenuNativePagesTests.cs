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
        Assert.False(list.EnabledItems[0]);
        Assert.True(list.EnabledItems[1]);
        Assert.Contains("7/40 AP", list.Items[1]);
        Assert.Contains(Descendants(view.Root).OfType<UiLabel>(), label => label.Text == "7 / 40 AP");
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
