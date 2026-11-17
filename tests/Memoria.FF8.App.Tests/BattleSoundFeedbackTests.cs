using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSoundFeedbackTests
{
    [Fact]
    public void KeyboardAndPointerActionsShareFeedbackAndIdleCancelIsSilent()
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Enemy" }, new() { Id = "other", Name = "Other" }] }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { }, readPresentationUi: BattleHudTestPresentation.Visible);
        List<MenuSound> sounds = [];
        hud.SoundRequested += sounds.Add;
        hud.Navigate(MenuInput.Cancel);
        hud.Attack();
        hud.SelectTarget("other", false);
        hud.SelectTarget("other", false);
        hud.Navigate(MenuInput.Cancel);
        hud.Attack();
        hud.SelectTarget("other", true);
        Assert.Equal(new[] { MenuSound.Confirm, MenuSound.Move, MenuSound.Cancel, MenuSound.Confirm, MenuSound.Confirm }, sounds);
        Assert.Single(battle.QueuedActions);
    }
}
