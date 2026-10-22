using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianForceNavigationTests
{
    [Fact]
    public void SelectingGuardianForceFocusesAbilitiesAndCancelReturnsToRoster()
    {
        MenuController menu = new();
        menu.Open("gf", demo: true);
        menu.Navigate(MenuInput.Right);
        string forceId = menu.SelectedEntry!.Id;

        menu.Navigate(MenuInput.Confirm);

        Assert.Equal("gf-learn", menu.ScreenId);
        Assert.Equal(forceId, menu.GuardianForce?.Id);
        UiList abilities = Assert.IsType<UiList>(menu.BuildView().FocusedElement);
        Assert.Equal("commands", abilities.Id);
        Assert.Equal(menu.GuardianForce!.LearningChoices.Select(choice => choice.Name), abilities.Items);
        Assert.DoesNotContain(menu.Entries, entry => entry.Id is "gf-learn" or "gf-abilities");

        menu.Navigate(MenuInput.Cancel);

        Assert.Equal("gf", menu.ScreenId);
        Assert.Equal(forceId, menu.SelectedEntry?.Id);
    }

    [Theory]
    [InlineData("gf-learn")]
    [InlineData("gf-abilities")]
    public void PageButtonsCycleAvailableGuardianForcesWithoutChangingCharacter(string screen)
    {
        MenuController menu = new();
        menu.Open(screen, demo: true);
        MenuGuardianForce[] forces = menu.Data.GuardianForces.ToArray();
        MenuCharacter? character = menu.Character;
        var view = menu.BuildView();

        menu.Navigate(MenuInput.PageLeft);

        Assert.Equal(screen, menu.ScreenId);
        Assert.Same(forces[^1], menu.GuardianForce);
        Assert.Same(character, menu.Character);
        Assert.NotSame(view, menu.BuildView());

        menu.Navigate(MenuInput.PageRight);
        Assert.Same(forces[0], menu.GuardianForce);
        foreach (MenuGuardianForce force in forces.Skip(1).Append(forces[0]))
        {
            menu.Navigate(MenuInput.PageRight);
            Assert.Same(force, menu.GuardianForce);
        }
    }

    [Fact]
    public void LearningSelectionClampsAndConfirmationTargetsTheNewGuardianForce()
    {
        MenuData data = new();
        MenuGuardianForce first = new() { Id = "gf-0", Name = "First" };
        first.LearningChoices.AddRange([new("10", "First ability"), new("11", "Other ability")]);
        MenuGuardianForce second = new() { Id = "gf-4", Name = "Second" };
        second.LearningChoices.Add(new("20", "Second ability", Progress: 7, RequiredPoints: 30));
        data.GuardianForces.AddRange([first, second]);
        data.SupportedActions.Add("gf-learn");
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.Open("gf-learn");
        menu.Navigate(MenuInput.Down);
        Assert.Equal(1, menu.SelectedIndex);

        menu.Navigate(MenuInput.PageRight);

        Assert.Same(second, menu.GuardianForce);
        Assert.Equal(0, menu.SelectedIndex);
        Assert.Equal("20", menu.SelectedEntry?.Id);
        Assert.Equal("Second ability", menu.SelectedEntry?.Label);
        menu.Navigate(MenuInput.Confirm);
        MenuAction action = Assert.Single(actions);
        Assert.Equal("gf-learn", action.Kind);
        Assert.Equal("gf-4", action.TargetId);
        Assert.Equal("20", action.SubjectId);
        Assert.Equal(7, second.LearningChoices[0].Progress);
        Assert.Equal("gf-learn", menu.ScreenId);
    }

    [Fact]
    public void PageButtonsHandleEmptyAndSingleGuardianForceLists()
    {
        MenuData data = new();
        MenuController menu = new(() => data);
        menu.Open("gf-learn");
        foreach (MenuInput input in new[] { MenuInput.PageLeft, MenuInput.PageRight })
        {
            menu.Navigate(input);
            Assert.Null(menu.GuardianForce);
            Assert.Equal(0, menu.SelectedIndex);
        }
        MenuGuardianForce force = new() { Id = "gf-3", Name = "Only" };
        data.GuardianForces.Add(force);
        foreach (MenuInput input in new[] { MenuInput.PageLeft, MenuInput.PageRight })
        {
            menu.Navigate(input);
            Assert.Same(force, menu.GuardianForce);
            Assert.Equal(0, menu.SelectedIndex);
        }
    }
}
