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
    public void AbilityMenuPrependsLearnedPassivesByTypeAndTogglesTheirExplicitOwner(bool enabled, bool available)
    {
        MenuData data = new();
        data.SupportedActions.Add("gf-passive-toggle");
        data.RefinementAbilities.Add("Refine");
        MenuGuardianForce first = new() { Id = "gf-0", Name = "First" };
        first.LearningChoices.Add(new MenuAbilityChoice("79", "Unlearned", false) { IsPassive = true });
        first.LearningChoices.Add(new MenuAbilityChoice("80", "Not passive", true));
        MenuGuardianForce owner = new() { Id = "gf-5", Name = "Owner" };
        // The projected type determines inclusion, independently of native ability IDs.
        owner.LearningChoices.Add(new MenuAbilityChoice("12", "Typed passive", true) { IsPassive = true, PassiveEnabled = enabled, PassiveAvailable = available, Icon = NativeAbilityMenuMetadata.Icon(79), Description = "Passive description" });
        first.LearningChoices.Add(owner.LearningChoices[0]);
        data.GuardianForces.AddRange([first, owner]);
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, action =>
        {
            actions.Add(action);
            owner.LearningChoices[0] = owner.LearningChoices[0] with { PassiveEnabled = !owner.LearningChoices[0].PassiveEnabled };
        });
        menu.Open("ability");
        Assert.Equal(new[] { "gf-0-passive-12", "gf-5-passive-12", "Refine" }, menu.Entries.Select(entry => entry.Id));
        menu.SelectEntry("ability", "gf-5-passive-12");
        UiList list = Assert.Single(Descendants(menu.BuildView().Root).OfType<UiList>(), element => element.Id == "commands");
        Assert.Equal(NativeAbilityMenuMetadata.Icon(79), list.Images[1]);
        Assert.Equal(NativeAbilityMenuMetadata.Icon(92), list.Images[2]);
        Assert.Equal(enabled ? new UiColor(112, 232, 144) : new UiColor(232, 184, 96), list.ItemTextColor!(1));
        Assert.Null(list.ItemTextColor(2));
        Assert.Contains(enabled ? "On" : "Off", list.SecondaryItems[1]);
        Assert.Contains(available ? "Available" : "GF KO", list.SecondaryItems[1]);
        Assert.Contains("Owner", menu.SelectedEntry!.Help);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(new MenuAction("gf-passive-toggle", "12", "gf-5"), Assert.Single(actions));
        Assert.Equal("ability", menu.ScreenId);
        Assert.Equal("gf-5-passive-12", menu.SelectedEntry!.Id);
        list = Assert.Single(Descendants(menu.BuildView().Root).OfType<UiList>(), element => element.Id == "commands");
        Assert.Equal(enabled ? new UiColor(232, 184, 96) : new UiColor(112, 232, 144), list.ItemTextColor!(1));
        menu.Navigate(MenuInput.Alternate);
        Assert.Equal(2, actions.Count);
        Assert.Equal(actions[0], actions[1]);
        Assert.Equal(enabled, owner.LearningChoices[0].PassiveEnabled);
        Assert.Equal(enabled, first.LearningChoices.Single(choice => choice.Id == "12").PassiveEnabled);
        menu.SelectEntry("ability", "Refine");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("ability-recipes", menu.ScreenId);
        Assert.Equal(2, actions.Count);
    }

    [Theory]
    [InlineData(79, true, true)]
    [InlineData(79, false, true)]
    [InlineData(79, true, false)]
    [InlineData(80, true, true)]
    [InlineData(80, false, true)]
    [InlineData(80, true, false)]
    [InlineData(81, true, true)]
    [InlineData(81, false, true)]
    [InlineData(81, true, false)]
    public void CardBindingAndSemanticRowShareTheExplicitToggle(int ability, bool enabled, bool available)
    {
        MenuData data = new();
        data.SupportedActions.Add("gf-passive-toggle");
        MenuGuardianForce gf = new() { Id = "gf-5", Name = "GF" };
        gf.LearningChoices.Add(new MenuAbilityChoice(ability.ToString(System.Globalization.CultureInfo.InvariantCulture), "Passive", true) { IsPassive = true, PassiveEnabled = enabled, PassiveAvailable = available });
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
        Assert.Equal(new MenuAction("gf-passive-toggle", ability.ToString(System.Globalization.CultureInfo.InvariantCulture), "gf-5"), action);
        PlayerAction input = NativeJunctionMenuProjection.PlayerAction(action);
        Assert.Equal(PlayerActionKind.ToggleGuardianForcePassiveAbility, input.Kind);
        Assert.Equal(5, input.Value);
        Assert.Equal(ability.ToString(System.Globalization.CultureInfo.InvariantCulture), input.Text);
        actions.Clear();
        menu.SelectEntry("gf-learn", ability.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
    [InlineData("78", "gf-0")]
    [InlineData("79", "gf-16")]
    [InlineData("79", "character-0")]
    public void PassiveProjectionRejectsUnsupportedOrLostIdentities(string ability, string owner)
    {
        Assert.Throws<ArgumentException>(() => NativeJunctionMenuProjection.PlayerAction(new MenuAction("gf-passive-toggle", ability, owner)));
    }

    [Theory]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    public void LiveToggleSurvivesSnapshotReloadAndIsAbsentFromJunctionChoices(int ability)
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed native menu resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(5).TeachGuardianForceAbility(5, ability).ToggleGuardianForceJunction(0, 5);
        MenuController menu = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = vitals }), action => vitals = NativeJunctionMenuInput.Apply(vitals, NativeJunctionMenuProjection.PlayerAction(action)));
        menu.Open("ability");
        menu.SelectEntry("ability", $"gf-5-passive-{ability}");
        menu.Navigate(MenuInput.Confirm);
        Assert.False(vitals.GuardianForcePassiveAbilityEnabled(5, ability));
        vitals = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(vitals))!;
        vitals.Validate();
        menu.Open("gf-learn");
        MenuAbilityChoice passive = Assert.Single(menu.Data.GuardianForces).LearningChoices.Single(choice => choice.Id == ability.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(passive.Learned);
        Assert.False(passive.PassiveEnabled);
        Assert.DoesNotContain(menu.Data.Characters.Single(character => character.Id == "character-0").AvailableAbilities, choice => choice.Id == ability.ToString(System.Globalization.CultureInfo.InvariantCulture));
        menu.SelectEntry("gf-learn", ability.ToString(System.Globalization.CultureInfo.InvariantCulture));
        menu.Navigate(MenuInput.Alternate);
        Assert.True(vitals.GuardianForcePassiveAbilityEnabled(5, ability));
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
