using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudNativeCureAdmissionTests
{
    [Theory]
    [InlineData(27, false)]
    [InlineData(27, true)]
    [InlineData(51, false)]
    [InlineData(51, true)]
    public void Expanded_menu_submits_installed_cures_for_magic_and_draw_cast(int spellId, bool drawCast)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleActionDefinition spell = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Id == spellId);
        BattleSession battle = new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, CurrentHp = 40, MaximumHp = 100, Magic = 90, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Magic, BattleAbility.Draw], MagicStocks = [new(spellId, 2)] },
                new() { Id = "source", Name = "Source", DrawSpells = [new(spellId, 0)] }
            ],
            ActionDefinitions = [spell],
            NativeActions = assets.NativeActions.Where(action => action.ActionId == spellId && action.Command is 2 or 247).ToArray(),
            NativeKnownMagic = new(NativeInputConfiguration.OriginalInitHash, [0, 0])
        }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { }, execute: input =>
            battle.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId, input.DrawCast, drawCastTargetId: input.DrawCastTargetId));

        hud.SelectParty("caster");
        BattleAbility ability = drawCast ? BattleAbility.Draw : BattleAbility.Magic;
        int commandIndex = hud.Commands.ToList().IndexOf(ability);
        Assert.True(commandIndex >= 0);
        hud.Choose(commandIndex);
        if (drawCast)
        {
            Assert.True(hud.DrawCastAvailable);
            hud.ChangeDrawPage(true);
            Assert.True(hud.DrawCast);
        }
        Assert.Equal(spellId, Assert.Single(hud.ActionChoices).Id);

        hud.Choose(0);
        Assert.True(hud.SelectingTarget);
        hud.SelectTarget("caster", confirm: true);

        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal(ability, queued.Ability);
        Assert.Equal(spellId, queued.ActionId);
        Assert.Equal(drawCast, queued.DrawCast);
        Assert.Equal(drawCast ? "source" : "caster", queued.TargetId);
        Assert.Equal(drawCast ? "caster" : null, queued.DrawCastTargetId);
    }
}
