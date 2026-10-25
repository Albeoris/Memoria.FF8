using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleMagicGroupHudTests
{
    [Theory]
    [InlineData(0x20000u, 2)]
    [InlineData(0x40000u, 3)]
    public void RepeatedMagicDefaultsToAvailableStatusAndAllowsEachTarget(uint status, int count)
    {
        BattleSession session = CreateSession(status);
        BattleHudController hud = CreateHud(session);
        hud.BuildView();
        hud.Choose(0);
        hud.Choose(0);
        Assert.Equal(count, hud.MagicCastCount);
        Assert.Contains($"×{count}", hud.TargetActionCaption);
        for (int index = 0; index < count; index++)
        {
            Assert.True(hud.SelectingTarget);
            Assert.Empty(session.QueuedActions);
            hud.SelectTarget(index % 2 == 0 ? "enemy-a" : "enemy-b", true);
        }
        BattleAttackCommand queued = Assert.Single(session.QueuedActions);
        Assert.Equal(Enumerable.Range(0, count).Select(index => (ushort)(index % 2 == 0 ? 8 : 16)), queued.NativeMagicTargets);
        Assert.False(hud.SelectingTarget);
    }

    [Fact]
    public void ExplicitSingleCastOverridesDoubleDefault()
    {
        BattleSession session = CreateSession(0x20000);
        BattleHudController hud = CreateHud(session);
        hud.BuildView();
        hud.Choose(0);
        hud.Navigate(MenuInput.PageRight);
        hud.Navigate(MenuInput.PageRight);
        Assert.Equal(1, hud.MagicCastCount);
        hud.Choose(0);
        hud.SelectTarget("enemy-a", true);
        Assert.Empty(Assert.Single(session.QueuedActions).NativeMagicTargets);
        Assert.False(hud.SelectingTarget);
    }

    [Fact]
    public void TripleSelectsThreeTargetsBeforeSubmittingOneOriginalSpellPacket()
    {
        BattleSession session = CreateSession(0x40000);
        BattleHudController hud = CreateHud(session);
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

    private static BattleHudController CreateHud(BattleSession session)
    {
        return new(session, session.QueueAttack, () => { }, () => { }, execute: input => session.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId, input.DrawCast, input.MagicTargets.IsEmpty ? null : input.MagicTargets));
    }

    private static BattleSession CreateSession(uint status)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        NativeCharacterVital character = vitals.Records[0];
        return new(new BattleDefinition
        {
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, NativeStatus1 = status, Abilities = [BattleAbility.Magic], MagicStocks = [new(1, 3)], NativePlayerStatRules = assets.ReadPlayerNativeStatRules(vitals, 0), NativeGrowth = new(character.HitPointBonus, character.StatBonuses.ToImmutableArray()), NativeStatusResistances = new byte[40] },
                new() { Id = "enemy-a", Name = "First", Speed = 1 },
                new() { Id = "enemy-b", Name = "Second", Speed = 1 }
            ]
        }, BattleAtbMode.TurnBased);
    }
}
