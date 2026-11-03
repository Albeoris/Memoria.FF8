using System.Collections.Immutable;
using System.Globalization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudDrawTests
{
    [Theory]
    [InlineData(0, false, false)]
    [InlineData(99, false, false)]
    [InlineData(100, false, false)]
    [InlineData(0, true, false)]
    [InlineData(99, true, false)]
    [InlineData(100, true, false)]
    [InlineData(0, false, true)]
    [InlineData(99, false, true)]
    [InlineData(100, false, true)]
    [InlineData(0, true, true)]
    [InlineData(99, true, true)]
    [InlineData(100, true, true)]
    public void StockPageHidesFullStockAndFallsBackToCastRegardlessOfMagicMode(int stock, bool infinite, bool shared)
    {
        BattleSession battle = CreateWithMagicPool(stock, infinite, shared);
        List<BattleInput> inputs = [];
        BattleHudController hud = CreateHud(battle, inputs);
        hud.Choose(0);

        Assert.True(hud.ShowingDrawList);
        Assert.False(hud.SelectingTarget);
        Assert.Equal(stock == 100, hud.DrawCast);
        Assert.Equal(stock < 100, hud.DrawStockAvailable);
        Assert.True(hud.DrawCastAvailable);
        Assert.Equal(stock == 100 ? "" : stock.ToString(CultureInfo.InvariantCulture), hud.ChoiceValue(hud.SelectedAction!));
        hud.Navigate(MenuInput.Confirm);
        if (stock < 100)
        {
            BattleInput input = Assert.Single(inputs);
            Assert.Equal(2, input.ActionId);
            Assert.Equal("source", input.TargetId);
            Assert.False(input.DrawCast);
            Assert.False(hud.SelectingAction);
        }
        else
        {
            Assert.True(hud.SelectingDrawCastRecipient);
            Assert.Empty(inputs);
            hud.SelectTarget("ally", true);
            Assert.True(Assert.Single(inputs).DrawCast);
        }
    }

    [Theory]
    [InlineData(false, "ally")]
    [InlineData(true, "fallen")]
    public void CastPageChoosesSeparateRecipient(bool revive, string recipient)
    {
        List<BattleInput> inputs = [];
        BattleHudController hud = CreateHud(Create(revive), inputs);
        hud.Choose(0);
        Assert.Equal("source", hud.SelectedTarget!.Id);
        hud.Navigate(MenuInput.Right);
        Assert.True(hud.DrawCast);
        Assert.Equal(revive ? "Life" : "Cure", Assert.Single(hud.ChoiceLabels));
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingDrawCastRecipient);
        Assert.Empty(inputs);
        hud.SelectTarget(recipient, true);
        BattleInput input = Assert.Single(inputs);
        Assert.Equal("source", input.TargetId);
        Assert.Equal(recipient, input.DrawCastTargetId);
        Assert.True(input.DrawCast);
        input.Validate();
    }

    [Fact]
    public void CancelReturnsFromRecipientToCastPageThenCommand()
    {
        List<BattleInput> inputs = [];
        BattleHudController hud = CreateHud(Create(false), inputs);
        hud.Choose(0);
        hud.ChangeDrawPage(true);
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Cancel);
        Assert.True(hud.ShowingDrawList);
        Assert.True(hud.DrawCast);
        Assert.Equal("source", hud.SelectedTarget!.Id);
        hud.Navigate(MenuInput.Cancel);
        Assert.False(hud.SelectingAction);
        Assert.False(hud.SelectingTarget);
        Assert.Empty(inputs);
    }

    [Fact]
    public void GuardianForceUsesUnknownLabelAndOnlyStock()
    {
        List<BattleInput> inputs = [];
        BattleHudController hud = CreateHud(Create(false, true), inputs);
        hud.Choose(0);
        Assert.Equal("????", Assert.Single(hud.ChoiceLabels));
        Assert.True(hud.IsDrawGuardianForce(hud.SelectedAction!));
        Assert.False(hud.DrawCastAvailable);
        hud.Navigate(MenuInput.Right);
        Assert.False(hud.DrawCast);
        hud.Navigate(MenuInput.Confirm);
        BattleInput input = Assert.Single(inputs);
        Assert.Equal(65, input.ActionId);
        Assert.False(input.DrawCast);
        Assert.Null(input.DrawCastTargetId);
    }

    [Fact]
    public void UnseenMagicUsesSavedKnowledgeBank()
    {
        BattleDefinition definition = Create(false).Capture().Definition;
        NativeKnownMagic empty = new(NativeInputConfiguration.OriginalInitHash, ImmutableArray.Create<uint>(0, 0));
        BattleHudController hud = CreateHud(new(definition with { NativeKnownMagic = empty }, BattleAtbMode.TurnBased));
        hud.Choose(0);
        Assert.Equal("????", Assert.Single(hud.ChoiceLabels));
        Assert.Equal(2, hud.SelectedAction!.Id);
        hud.ChangeDrawPage(true);
        Assert.Equal("????", Assert.Single(hud.ChoiceLabels));
    }

    [Fact]
    public void DrawListUnifiesUniqueSpellsAndSelectsBestSourceWithoutRandomConsumption()
    {
        BattleDefinition definition = Create(false).Capture().Definition;
        definition.Combatants[3] = definition.Combatants[3] with { NativeDrawSlots = [new(2, 5), new(3, 0), new(0, 0), new(0, 0)] };
        definition.Combatants[4] = definition.Combatants[4] with { NativeDrawSlots = [new(2, 0), new(0, 0), new(0, 0), new(0, 0)] };
        definition = definition with { ActionDefinitions = [.. definition.ActionDefinitions, new(BattleAbility.Magic, 3, "Fire", BattleActionEffect.Damage, 10, false)] };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        var random = battle.Capture().Random;
        List<BattleInput> inputs = [];
        BattleHudController hud = CreateHud(battle, inputs);
        hud.Choose(0);
        Assert.Equal(new[] { 2, 3 }, hud.ActionChoices.Select(action => action.Id));
        Assert.Equal("other", hud.SelectedTarget!.Id);
        hud.Navigate(MenuInput.Down);
        Assert.Equal("source", hud.SelectedTarget!.Id);
        hud.HoverDrawChoice(0);
        Assert.Equal("other", hud.SelectedTarget!.Id);
        hud.ChangeDrawPage(true);
        Assert.Equal("other", hud.SelectedTarget!.Id);
        hud.ChangeDrawPage(false);
        hud.BuildView();
        Assert.Equal(random, battle.Capture().Random);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal("other", Assert.Single(inputs).TargetId);
    }

    [Theory]
    [InlineData(0, "other")]
    [InlineData(99, "source")]
    public void StockRanksAcceptedQuantityAndKeepsStableTiesWhenCapacityClampsBothSources(int stock, string expectedSource)
    {
        BattleDefinition definition = Create(false).Capture().Definition;
        definition.Combatants[0] = definition.Combatants[0] with { MagicStocks = stock == 0 ? [] : [new(2, stock)] };
        definition.Combatants[3] = definition.Combatants[3] with { NativeDrawSlots = [new(2, 2), new(0, 0), new(0, 0), new(0, 0)] };
        definition.Combatants[4] = definition.Combatants[4] with { NativeDrawSlots = [new(2, 0), new(0, 0), new(0, 0), new(0, 0)] };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleCombatant caster = battle.Combatants[0];
        BattleCombatant weaker = battle.Combatants[3];
        BattleCombatant stronger = battle.Combatants[4];
        int weakerQuantity = Enumerable.Range(0, 32).Sum(random => battle.PreviewDrawQuantity(caster, weaker, 2, random));
        int strongerQuantity = Enumerable.Range(0, 32).Sum(random => battle.PreviewDrawQuantity(caster, stronger, 2, random));
        Assert.True(weakerQuantity < strongerQuantity);
        Assert.All(Enumerable.Range(0, 32), random => Assert.True(battle.PreviewDrawQuantity(caster, weaker, 2, random) > 0));
        List<BattleInput> inputs = [];
        BattleHudController hud = CreateHud(battle, inputs);
        hud.Choose(0);
        Assert.Equal(expectedSource, hud.SelectedTarget!.Id);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(expectedSource, Assert.Single(inputs).TargetId);
    }

    [Fact]
    public void CastRanksSuccessProbabilityAndKeepsStableTiesRegardlessOfQuantity()
    {
        BattleDefinition definition = Create(false).Capture().Definition;
        definition.Combatants[3] = definition.Combatants[3] with { NativeDrawSlots = [new(2, 2), new(0, 0), new(0, 0), new(0, 0)] };
        definition.Combatants[4] = definition.Combatants[4] with { NativeDrawSlots = [new(2, 0), new(0, 0), new(0, 0), new(0, 0)] };
        List<BattleInput> inputs = [];
        BattleHudController hud = CreateHud(new(definition, BattleAtbMode.TurnBased), inputs);
        hud.Choose(0);
        Assert.Equal("other", hud.SelectedTarget!.Id);
        hud.ChangeDrawPage(true);
        Assert.Equal("source", hud.SelectedTarget!.Id);
        hud.Navigate(MenuInput.Confirm);
        hud.SelectTarget("ally", true);
        BattleInput input = Assert.Single(inputs);
        Assert.True(input.DrawCast);
        Assert.Equal("source", input.TargetId);
    }

    [Fact]
    public void DeadSourcesUnavailableSpellsAcquiredGuardiansAndImpossibleDrawsAreHidden()
    {
        BattleDefinition definition = Create(false, true).Capture().Definition;
        definition.Combatants[3] = definition.Combatants[3] with { NativeDrawSlots = [new(2, 255), new(3, 0), new(65, 0), new(0, 0)] };
        definition.Combatants[4] = definition.Combatants[4] with { CurrentHp = 0, NativeStatus0 = 1, NativeDrawSlots = [new(2, 0), new(0, 0), new(0, 0), new(0, 0)] };
        definition = definition with
        {
            GuardianForces = [new(1, 100, 1000, true)],
            ActionDefinitions = [.. definition.ActionDefinitions, new(BattleAbility.Magic, 3, "Fire", BattleActionEffect.Damage, 10, false, Available: false)]
        };
        BattleHudController hud = CreateHud(new(definition, BattleAtbMode.TurnBased));
        Assert.False(hud.DrawAvailable);
        hud.Choose(0);
        Assert.False(hud.ShowingDrawList);
        Assert.Empty(hud.ActionChoices);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DepletedSpellLeavesMenuOrRefreshesToRemainingSpellWithoutSubmitting(bool anotherSpell)
    {
        BattleSession battle = CreateConcurrentDraw(false, anotherSpell);
        List<BattleInput> inputs = [];
        BattleHudController hud = CreateHud(battle, inputs);
        hud.Choose(0);
        hud.ChangeDrawPage(true);
        hud.Navigate(MenuInput.Confirm);
        ConsumeSourceMagic(battle);
        hud.Navigate(MenuInput.Confirm);
        Assert.Empty(inputs);
        Assert.False(hud.SelectingTarget);
        Assert.Equal(anotherSpell, hud.ShowingDrawList);
        if (anotherSpell)
        {
            Assert.Equal(3, hud.SelectedAction!.Id);
        }
    }

    [Fact]
    public void DepletedSourceAutomaticallyRebindsSameSpellToAnotherEnemy()
    {
        BattleSession battle = CreateConcurrentDraw(true, false);
        List<BattleInput> inputs = [];
        BattleHudController hud = CreateHud(battle, inputs);
        hud.Choose(0);
        Assert.Equal("source", hud.SelectedTarget!.Id);
        ConsumeSourceMagic(battle);
        hud.BuildView();
        Assert.True(hud.ShowingDrawList);
        Assert.Equal("other", hud.SelectedTarget!.Id);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal("other", Assert.Single(inputs).TargetId);
    }

    [Fact]
    public void FullIndividualInventoryHidesNewStockButAllowsCast()
    {
        BattleDefinition definition = Create(false).Capture().Definition;
        definition.Combatants[0] = definition.Combatants[0] with { NativeMagicInventory = Enumerable.Range(3, 32).Select(id => new NativeMagicStock((byte)id, 1)).ToArray() };
        BattleHudController hud = CreateHud(new(definition, BattleAtbMode.TurnBased));
        hud.Choose(0);
        Assert.False(hud.DrawStockAvailable);
        Assert.True(hud.DrawCast);
    }

    [Fact]
    public void ColumnNavigationMovesWithinBalancedLayoutBeforeChangingPage()
    {
        BattleDefinition definition = Create(false).Capture().Definition;
        definition.Combatants[3] = definition.Combatants[3] with { NativeDrawSlots = [new(2, 0), new(3, 0), new(4, 0), new(5, 0)] };
        definition.Combatants[4] = definition.Combatants[4] with { NativeDrawSlots = [new(6, 0), new(7, 0), new(8, 0), new(9, 0)] };
        definition = definition with { ActionDefinitions = [.. definition.ActionDefinitions, .. Enumerable.Range(3, 7).Select(id => new BattleActionDefinition(BattleAbility.Magic, id, $"Spell{id}", BattleActionEffect.Damage, 10, false))] };
        BattleHudController hud = CreateHud(new(definition, BattleAtbMode.TurnBased));
        hud.Choose(0);
        hud.Navigate(MenuInput.Down);
        Assert.Equal(3, hud.SelectedAction!.Id);
        hud.Navigate(MenuInput.Right);
        Assert.Equal(7, hud.SelectedAction!.Id);
        Assert.False(hud.DrawCast);
        hud.Navigate(MenuInput.Right);
        Assert.True(hud.DrawCast);
        Assert.Equal(7, hud.SelectedAction!.Id);
        hud.Navigate(MenuInput.Left);
        Assert.Equal(3, hud.SelectedAction!.Id);
        hud.Navigate(MenuInput.Left);
        Assert.False(hud.DrawCast);
    }

    private static BattleHudController CreateHud(BattleSession battle, List<BattleInput>? inputs = null)
    {
        return new(battle, (_, _) => { }, () => { }, () => { }, execute: input => inputs?.Add(input));
    }

    private static void ConsumeSourceMagic(BattleSession battle)
    {
        battle.QueueAction("ally", "source", BattleAbility.Draw, 2);
        battle.Tick();
        Assert.DoesNotContain(battle.Combatants.Single(actor => actor.Id == "source").NativeDrawSlots, slot => slot.Id == 2);
    }

    private static BattleSession CreateConcurrentDraw(bool anotherSource, bool anotherSpell)
    {
        BattleDefinition definition = Create(false).Capture().Definition;
        definition.Combatants[1] = definition.Combatants[1] with { InitialAtb = BattleSession.AtbMaximum, Magic = 100, Abilities = [BattleAbility.Draw] };
        if (anotherSource)
        {
            definition.Combatants[4] = definition.Combatants[4] with { NativeDrawSlots = [new(2, 0), new(0, 0), new(0, 0), new(0, 0)] };
        }
        if (anotherSpell)
        {
            definition.Combatants[3] = definition.Combatants[3] with { NativeDrawSlots = [new(2, 0), new(3, 0), new(0, 0), new(0, 0)] };
            definition = definition with { ActionDefinitions = [.. definition.ActionDefinitions, new(BattleAbility.Magic, 3, "Fire", BattleActionEffect.Damage, 10, false)] };
        }
        return new(definition with { DrawOncePerSpell = true }, BattleAtbMode.TurnBased);
    }

    private static BattleSession CreateWithMagicPool(int stock, bool infinite, bool shared)
    {
        NativeStatCurve curve = new(1, 0, 1, 10, 10);
        NativeStatModifier modifier = new(0, 0, 0);
        NativeCharacterVitals vitals = new()
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = new string('0', 64),
            KernelSha256 = new string('0', 64),
            Records = Enumerable.Range(0, 8).Select(id => new NativeCharacterVital((byte)id, new NativeHpWord(10, 10), 0, new NativeCharacterJunction((ushort)(1 << id), 0, 0, new byte[20]), 0, 0, 0, new byte[6], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), 1, 0)).ToArray(),
            CombatParty = [0, 1, 2],
            DreamEnabled = false,
            GuardianForces = Enumerable.Range(0, 16).Select(_ => new NativeGuardianForceVital(0, 1, 10, new NativeGuardianForceLearnedBank(0x3FFFF, 0), 10, new byte[21].ToImmutableArray(), 0, 0)).ToArray(),
            Definitions = new NativeStatDefinitions(Enumerable.Repeat(curve, 11).ToImmutableArray(), Enumerable.Repeat((byte)1, 57).ToImmutableArray(), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray()),
            MenuDefinitions = new NativeGuardianForceMenuDefinitions(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), new byte[116].ToImmutableArray(), new byte[19].ToImmutableArray(), new byte[5].ToImmutableArray()),
            DreamWeapons = new byte[3],
            JunctionPartyBackup = new byte[6]
        };
        vitals = vitals.ChangeMagicStock(0, 2, stock, MagicOperationReason.ScriptReward, out _);
        vitals = MagicModeCoordinator.Preview(vitals, new GameplayPreferences { InfiniteMagic = infinite, SharedMagicStock = shared }).Vitals;
        BattleDefinition definition = Create(false).Capture().Definition;
        definition.Combatants[0] = definition.Combatants[0] with { MagicRecordId = shared ? 1 : 0 };
        return new(definition with { MagicVitals = vitals }, BattleAtbMode.TurnBased);
    }

    private static BattleSession Create(bool revive, bool gf = false)
    {
        return new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "party", Name = "Caster", IsPlayer = true, MaximumHp = 100, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, Speed = 1, Abilities = [BattleAbility.Draw] },
                new() { Id = "ally", Name = "Ally", IsPlayer = true, MaximumHp = 100, CurrentHp = 10, Speed = 1 },
                new() { Id = "fallen", Name = "Fallen", IsPlayer = true, MaximumHp = 100, CurrentHp = 0, NativeStatus0 = 1, Speed = 1 },
                new() { Id = "source", Name = "Source", MaximumHp = 100, CurrentHp = 100, Speed = 1, NativeDrawSlots = [new(gf ? (byte)65 : (byte)2, 0), new(0, 0), new(0, 0), new(0, 0)] },
                new() { Id = "other", Name = "Other", MaximumHp = 100, CurrentHp = 100, Speed = 1 }
            ],
            GuardianForces = [new(1, 1000, 100, false)],
            ActionDefinitions = [new(BattleAbility.Magic, 2, revive ? "Life" : "Cure", revive ? BattleActionEffect.Revive : BattleActionEffect.Heal, 0, true)]
        }, BattleAtbMode.TurnBased);
    }
}
