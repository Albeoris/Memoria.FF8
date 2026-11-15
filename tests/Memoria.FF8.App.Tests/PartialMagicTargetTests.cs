using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class PartialMagicTargetTests
{
    [Fact]
    public void TripleCuragaCanHealTwoSelectedAlliesWithoutHealingTheHoveredThirdAlly()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        int curaga = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Name == "Curaga").Id;
        BattleDefinition definition = CreateSession(0x40000).Capture().Definition;
        BattleSession session = new(definition with
        {
            Combatants =
            [
                definition.Combatants[0] with { MaximumHp = 1000, CurrentHp = 1, MagicStocks = [new(curaga, 5)] },
                definition.Combatants[1] with { MaximumHp = 1000, CurrentHp = 1 },
                new() { Id = "third-ally", Name = "Third ally", IsPlayer = true, MaximumHp = 1000, CurrentHp = 1 },
                definition.Combatants[2],
                definition.Combatants[3]
            ]
        }, BattleAtbMode.TurnBased);
        List<BattleInput> inputs = [];
        BattleHudController hud = CreateHud(session, inputs);
        BeginTargetSelection(hud);
        Assert.Equal(3, hud.MagicCastCount);
        hud.SelectTarget("caster", true);
        hud.SelectTarget("ally", true);
        hud.SelectTarget("third-ally", false);

        hud.Navigate(MenuInput.Alternate);

        Assert.Equal(new[] { "caster", "ally" }, Assert.Single(inputs).MagicTargets);
        Assert.Equal(new ushort[] { 1, 2 }, Assert.Single(session.QueuedActions).MagicCasts.Select(cast => NativeBattleTargetRequestAdapter.ProjectOriginalWord(cast.Targets, target => session.Combatants.Single(actor => actor.Id == target.ActorId).Slot)!.Value));
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames + 2; frame++)
        {
            session.Tick();
        }
        Assert.Equal(3, Assert.Single(session.Combatants[0].MagicStocks).Count);
        Assert.True(session.Combatants.Single(actor => actor.Id == "caster").CurrentHp > 1);
        Assert.True(session.Combatants.Single(actor => actor.Id == "ally").CurrentHp > 1);
        Assert.Equal(1, session.Combatants.Single(actor => actor.Id == "third-ally").CurrentHp);
        Assert.Equal(new[] { "caster", "ally" }, session.Events.Where(item => item.Kind == "Healing").Select(item => item.TargetId));
    }

    [Theory]
    [InlineData(0x20000u, 1)]
    [InlineData(0x40000u, 1)]
    [InlineData(0x40000u, 2)]
    public void AlternateConfirmsOnlyCollectedTargetsAndSpendsTheirActualStock(uint status, int count)
    {
        BattleSession session = CreateSession(status);
        List<BattleInput> inputs = [];
        BattleHudController hud = CreateHud(session, inputs);
        BeginTargetSelection(hud);
        for (int index = 0; index < count; index++)
        {
            hud.SelectTarget("enemy-a", true);
        }
        hud.SelectTarget("enemy-b", false);
        Assert.True(hud.CanConfirmMagicTargets);
        Assert.Empty(inputs);

        hud.Navigate(MenuInput.Alternate);

        BattleInput input = Assert.Single(inputs);
        Assert.Equal("enemy-a", input.TargetId);
        Assert.Equal(Enumerable.Repeat("enemy-a", count), input.MagicTargets);
        BattleAttackCommand queued = Assert.Single(session.QueuedActions);
        Assert.Equal(Enumerable.Repeat((ushort)8, count), queued.MagicCasts.Select(cast => NativeBattleTargetRequestAdapter.ProjectOriginalWord(cast.Targets, target => session.Combatants.Single(actor => actor.Id == target.ActorId).Slot)!.Value));
        Assert.Equal(count == 2 && status == 0x40000 ? 3 : 0, queued.NativeMagicCastMode);
        Assert.False(hud.SelectingTarget);
        Assert.False(hud.CanConfirmMagicTargets);
        Assert.Empty(hud.MagicConfirmCaption);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames + 2; frame++)
        {
            session.Tick();
        }
        Assert.Equal(5 - count, Assert.Single(session.Combatants[0].MagicStocks).Count);
        Assert.Equal(count, session.Events.Count(item => item.Kind == "Damage" && item.TargetId == "enemy-a"));
        Assert.DoesNotContain(session.Events, item => item.Kind == "Damage" && item.TargetId == "enemy-b");
    }

    [Theory]
    [InlineData(0x20000u)]
    [InlineData(0x40000u)]
    public void AlternateWithNoCollectedTargetsLeavesSelectionAndStockUnchanged(uint status)
    {
        BattleSession session = CreateSession(status);
        List<BattleInput> inputs = [];
        BattleHudController hud = CreateHud(session, inputs);
        BeginTargetSelection(hud);
        hud.SelectTarget("enemy-b", false);
        int casts = hud.MagicCastCount;

        hud.Navigate(MenuInput.Alternate);

        Assert.True(hud.SelectingTarget);
        Assert.Equal("enemy-b", hud.SelectedTarget!.Id);
        Assert.Equal(casts, hud.MagicCastCount);
        Assert.False(hud.CanConfirmMagicTargets);
        Assert.Empty(hud.MagicConfirmCaption);
        Assert.Empty(inputs);
        Assert.Empty(session.QueuedActions);
        Assert.Equal(5, Assert.Single(session.Combatants[0].MagicStocks).Count);
    }

    [Fact]
    public void CancelRemovesTheLastCollectedTargetBeforeAlternateConfirmation()
    {
        BattleSession session = CreateSession(0x40000);
        List<BattleInput> inputs = [];
        BattleHudController hud = CreateHud(session, inputs);
        BeginTargetSelection(hud);
        hud.SelectTarget("enemy-a", true);
        hud.SelectTarget("enemy-b", true);

        hud.Navigate(MenuInput.Cancel);

        Assert.True(hud.SelectingTarget);
        Assert.True(hud.CanConfirmMagicTargets);
        hud.SelectTarget("enemy-b", false);
        hud.Navigate(MenuInput.Alternate);
        BattleInput input = Assert.Single(inputs);
        Assert.Equal(new[] { "enemy-a" }, input.MagicTargets);
        Assert.Equal(new ushort[] { 8 }, Assert.Single(session.QueuedActions).MagicCasts.Select(cast => NativeBattleTargetRequestAdapter.ProjectOriginalWord(cast.Targets, target => session.Combatants.Single(actor => actor.Id == target.ActorId).Slot)!.Value));
    }

    [Fact]
    public void ConfirmationCaptionUsesConfiguredBindingOnlyWhileTargetsRemainCollected()
    {
        BattleSession session = CreateSession(0x40000);
        BattleHudController hud = CreateHud(session, []);
        hud.MagicConfirmBindingHint = "Custom card binding";
        BeginTargetSelection(hud);
        Assert.Empty(hud.MagicConfirmCaption);

        hud.SelectTarget("enemy-a", true);

        Assert.Contains("Custom card binding", hud.MagicConfirmCaption);
        Assert.Contains("×1", hud.MagicConfirmCaption);
        BattleUiTests.TestCanvas canvas = new();
        hud.BuildView().Draw(canvas, new UiTheme());
        Assert.Contains(canvas.TextDraws, draw => draw.Text.Contains("Custom card binding", StringComparison.Ordinal));
        hud.SelectTarget("enemy-b", true);
        Assert.Contains("×2", hud.MagicConfirmCaption);
        hud.Navigate(MenuInput.Cancel);
        Assert.Contains("×1", hud.MagicConfirmCaption);
        hud.Navigate(MenuInput.Cancel);
        Assert.True(hud.SelectingTarget);
        Assert.False(hud.CanConfirmMagicTargets);
        Assert.Empty(hud.MagicConfirmCaption);
        hud.Navigate(MenuInput.Alternate);
        Assert.Empty(session.QueuedActions);
    }

    private static void BeginTargetSelection(BattleHudController hud)
    {
        hud.BuildView();
        hud.Choose(0);
        hud.Choose(0);
        Assert.True(hud.SelectingTarget);
    }

    private static BattleHudController CreateHud(BattleSession session, List<BattleInput> inputs)
    {
        return new(session, session.QueueAttack, () => { }, () => { }, execute: input =>
        {
            inputs.Add(input);
            session.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId, input.DrawCast, input.MagicTargets.IsEmpty ? null : input.MagicTargets);
        });
    }

    private static BattleSession CreateSession(uint status)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        NativeCharacterVital character = vitals.Records[0];
        return new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, NativeStatus1 = status, Abilities = [BattleAbility.Magic], MagicStocks = [new(1, 5)], NativePlayerStatRules = assets.ReadPlayerNativeStatRules(vitals, 0), NativeGrowth = new(character.HitPointBonus, character.StatBonuses.ToImmutableArray()), NativeStatusResistances = new byte[40] },
                new() { Id = "ally", Name = "Ally", IsPlayer = true },
                new() { Id = "enemy-a", Name = "First", MaximumHp = 60000, CurrentHp = 60000, Speed = 1, ElementDefenses = [800, 800, 800, 800, 800, 800, 800, 800] },
                new() { Id = "enemy-b", Name = "Second", MaximumHp = 60000, CurrentHp = 60000, Speed = 1, ElementDefenses = [800, 800, 800, 800, 800, 800, 800, 800] }
            ]
        }, BattleAtbMode.TurnBased);
    }
}
