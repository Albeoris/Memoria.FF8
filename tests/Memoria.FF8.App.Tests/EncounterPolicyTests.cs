using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class EncounterPolicyTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Opening_and_highlighting_policy_never_applies_it_and_cancel_preserves_current_encounter(bool active, bool pending)
    {
        UiPreferences preferences = new UiPreferences().WithSetting("Battle policy", "Full combat");
        List<MenuAction> writes = [];
        int outcomes = 0;
        MenuController menus = new();
        menus.ConnectPreferences(() => preferences, writes.Add);
        menus.ConnectBattleSettings(() => active, _ => outcomes++);
        menus.ConnectPendingEncounter(() => pending);
        menus.Open("debug-battle", true);
        menus.SelectEntry("debug-battle", "Battle policy");
        menus.BuildView();
        Assert.Empty(writes);
        Assert.Equal(0, outcomes);
        menus.Execute(menus.SelectedEntry!.Action!);
        Assert.Equal("debug-policy-confirm", menus.ScreenId);
        Assert.Empty(writes);
        Assert.Equal(0, outcomes);
        Assert.Contains("active or pending", menus.CurrentScreen.Help);
        menus.SelectEntry("debug-policy-confirm", "cancel");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("debug-battle", menus.ScreenId);
        Assert.Equal("Battle policy", menus.SelectedEntry?.Id);
        Assert.Empty(writes);
        menus.Execute(new("encounter-policy-confirm"));
        Assert.Empty(writes);
        Assert.Equal("Full combat", preferences.BattlePolicyLabel);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Explicit_confirmation_dispatches_automatic_victory_once_to_its_existing_owner(bool active, bool pending)
    {
        UiPreferences preferences = new UiPreferences().WithSetting("Battle policy", "Full combat");
        List<MenuAction> writes = [];
        MenuController menus = new();
        menus.ConnectPreferences(() => preferences, action =>
        {
            writes.Add(action);
            preferences = preferences.WithSetting(action.SubjectId, action.Value);
        });
        menus.ConnectBattleSettings(() => active, _ => { });
        menus.ConnectPendingEncounter(() => pending);
        menus.Open("debug-battle", true);
        menus.SelectEntry("debug-battle", "Battle policy");
        menus.Execute(menus.SelectedEntry!.Action!);
        menus.SelectEntry("debug-policy-confirm", "apply");
        menus.Navigate(MenuInput.Confirm);
        MenuAction applied = Assert.Single(writes);
        Assert.Equal("config", applied.Kind);
        Assert.Equal("Battle policy", applied.SubjectId);
        Assert.Equal("Instant victory", applied.Value);
        Assert.Equal("Instant victory", preferences.BattlePolicyLabel);
        Assert.Equal("debug-battle", menus.ScreenId);
        menus.Execute(new("encounter-policy-confirm"));
        Assert.Single(writes);
    }

    [Fact]
    public void No_active_or_pending_encounter_needs_no_consequential_action_review()
    {
        UiPreferences preferences = new UiPreferences().WithSetting("Battle policy", "Full combat");
        List<MenuAction> writes = [];
        MenuController menus = new();
        menus.ConnectPreferences(() => preferences, writes.Add);
        menus.ConnectBattleSettings(() => false, _ => { });
        menus.ConnectPendingEncounter(() => false);
        menus.Open("debug-battle", true);
        menus.Execute(new("host-config", "Battle policy", Value: "Instant victory"));
        Assert.Equal("debug-battle", menus.ScreenId);
        Assert.Equal("Instant victory", Assert.Single(writes).Value);
    }
}
