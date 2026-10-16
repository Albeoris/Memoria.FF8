using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleMagicGroupHudTests
{
    [Fact]
    public void TripleSelectsThreeTargetsBeforeSubmittingOneOriginalSpellPacket()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        NativeCharacterVital character = vitals.Records[0];
        BattleSession session = new(new BattleDefinition
        {
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, NativeStatus1 = 0x40000, Abilities = [BattleAbility.Magic], MagicStocks = [new(1, 3)], NativePlayerStatRules = assets.ReadPlayerNativeStatRules(vitals, 0), NativeGrowth = new(character.HitPointBonus, character.StatBonuses.ToImmutableArray()), NativeStatusResistances = new byte[40] },
                new() { Id = "enemy-a", Name = "First", Speed = 1 },
                new() { Id = "enemy-b", Name = "Second", Speed = 1 }
            ]
        }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { }, execute: input => session.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId, input.DrawCast, input.MagicTargets));
        hud.BuildView();
        hud.Choose(0);
        hud.Navigate(MenuInput.PageRight);
        Assert.Equal(3, hud.MagicCastCount);
        hud.Choose(0);
        hud.SelectTarget("enemy-a", true);
        Assert.Empty(session.QueuedActions);
        hud.SelectTarget("enemy-b", true);
        Assert.Empty(session.QueuedActions);
        hud.Navigate(MenuInput.Cancel);
        hud.SelectTarget("enemy-a", true);
        Assert.Empty(session.QueuedActions);
        hud.SelectTarget("enemy-b", true);
        BattleAttackCommand queued = Assert.Single(session.QueuedActions);
        Assert.Equal(new ushort[] { 8, 8, 16 }, queued.NativeMagicTargets.ToArray());
        Assert.Equal(1, queued.ActionId);
        Assert.Equal(3, Assert.Single(session.Combatants[0].MagicStocks).Count);
        Assert.False(hud.SelectingTarget);
    }
}
