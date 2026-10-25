using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianForcePassiveMenuTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void CardBindingAndSemanticRowShareTheExplicitToggle(bool enabled, bool available)
    {
        MenuData data = new();
        data.SupportedActions.Add("gf-passive-toggle");
        MenuGuardianForce gf = new() { Id = "gf-5", Name = "GF" };
        gf.LearningChoices.Add(new MenuAbilityChoice("79", "Move-Find", true) { IsPassive = true, PassiveEnabled = enabled, PassiveAvailable = available });
        data.GuardianForces.Add(gf);
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.Open("gf-learn");
        UiView view = menu.BuildView();
        UiList list = Assert.Single(Descendants(view.Root).OfType<UiList>(), element => element.Id == "commands");
        Assert.Equal(enabled ? new UiColor(112, 232, 144) : new UiColor(232, 184, 96), list.ItemTextColor!(0));
        Assert.Contains(enabled ? "On" : "Off", list.SecondaryItems[0]);
        Assert.Contains(available ? "Available" : "GF KO", list.SecondaryItems[0]);
        Assert.Contains(Descendants(view.Root).OfType<UiLabel>(), label => label.Text.Contains("(Card): Toggle passive On/Off."));
        menu.Navigate(MenuInput.Alternate);
        MenuAction action = Assert.Single(actions);
        Assert.Equal(new MenuAction("gf-passive-toggle", "79", "gf-5"), action);
        PlayerAction input = NativeJunctionMenuProjection.PlayerAction(action);
        Assert.Equal(PlayerActionKind.ToggleGuardianForcePassiveAbility, input.Kind);
        Assert.Equal(5, input.Value);
        Assert.Equal("79", input.Text);
        actions.Clear();
        menu.SelectEntry("gf-learn", "79");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(action, Assert.Single(actions));
    }

    [Fact]
    public void CardBindingLeavesUnlearnedPassiveAsAnApTarget()
    {
        MenuData data = new();
        MenuGuardianForce gf = new() { Id = "gf-5", Name = "GF" };
        gf.LearningChoices.Add(new MenuAbilityChoice("79", "Move-Find", false, 1, 40) { IsPassive = true });
        data.GuardianForces.Add(gf);
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.Open("gf-learn");
        Assert.True(Assert.Single(menu.Entries).Enabled);
        Assert.Equal("gf-learn", menu.Entries[0].Action?.Kind);
        menu.Navigate(MenuInput.Alternate);
        Assert.Empty(actions);
        UiList list = Assert.Single(Descendants(menu.BuildView().Root).OfType<UiList>(), element => element.Id == "commands");
        Assert.Null(list.ItemTextColor!(0));
        Assert.Equal("1 / 40", list.SecondaryItems[0]);
    }

    [Theory]
    [InlineData("80", "gf-0")]
    [InlineData("79", "gf-16")]
    [InlineData("79", "character-0")]
    public void PassiveProjectionRejectsUnsupportedOrLostIdentities(string ability, string owner)
    {
        Assert.Throws<ArgumentException>(() => NativeJunctionMenuProjection.PlayerAction(new MenuAction("gf-passive-toggle", ability, owner)));
    }

    [Fact]
    public void LiveToggleSurvivesSnapshotReloadAndIsAbsentFromJunctionChoices()
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed native menu resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(5).TeachGuardianForceAbility(5, 79).ToggleGuardianForceJunction(0, 5);
        MenuController menu = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = vitals }), action => vitals = NativeJunctionMenuInput.Apply(vitals, NativeJunctionMenuProjection.PlayerAction(action)));
        menu.Open("gf-learn");
        menu.SelectEntry("gf-learn", "79");
        menu.Navigate(MenuInput.Alternate);
        Assert.False(vitals.GuardianForcePassiveAbilityEnabled(5, 79));
        vitals = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(vitals))!;
        vitals.Validate();
        menu.Open("gf-learn");
        MenuAbilityChoice passive = Assert.Single(menu.Data.GuardianForces).LearningChoices.Single(choice => choice.Id == "79");
        Assert.True(passive.Learned);
        Assert.False(passive.PassiveEnabled);
        Assert.DoesNotContain(menu.Data.Characters.Single(character => character.Id == "character-0").AvailableAbilities, choice => choice.Id == "79");
        menu.SelectEntry("gf-learn", "79");
        menu.Navigate(MenuInput.Alternate);
        Assert.True(vitals.GuardianForcePassiveAbilityEnabled(5, 79));
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
