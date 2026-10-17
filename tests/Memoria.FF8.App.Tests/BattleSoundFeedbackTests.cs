using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSoundFeedbackTests
{
    [Fact]
    public void KeyboardAndPointerActionsShareFeedbackAndIdleCancelIsSilent()
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Enemy" }, new() { Id = "other", Name = "Other" }] }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { });
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

    [Fact]
    public void DummyIsDebugOnlyAndHasNoBorrowedNativeModelOrAttackSound()
    {
        BattleDefinition definition = new() { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "dummy", Name = "Dummy", ModelId = -1, InitialAtb = BattleSession.AtbMaximum }] };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        List<int> impactActors = [];
        BattleAudioPresentation audio = new(impactActors.Add);
        audio.Update(battle);
        for (int frame = 0; frame < BattleSession.AttackWindupFrames + 2; frame++)
        {
            battle.Tick();
        }
        audio.Update(battle);
        Assert.Empty(impactActors);
        Assert.Throws<ArgumentException>(() => new BattleSession(definition with { EncounterId = 1 }, BattleAtbMode.Normal));
        Assert.Throws<ArgumentException>(() => new BattleSession(definition with { Combatants = [definition.Combatants[0] with { ModelId = -1 }, definition.Combatants[1]] }, BattleAtbMode.Normal));
        Assert.Equal("Debug training dummy", BattleDebugModels.CreateDummy().Name);
    }
}
