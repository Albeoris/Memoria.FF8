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
    public void SpellListShowsPoolCountAndRejectsFullStockRegardlessOfMagicMode(int stock, bool infinite, bool shared)
    {
        BattleSession battle = CreateWithMagicPool(stock, infinite, shared);
        List<BattleInput> inputs = [];
        List<MenuSound> sounds = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: inputs.Add);
        hud.SoundRequested += sounds.Add;
        hud.Choose(0);
        hud.Navigate(MenuInput.Confirm);

        Assert.Equal(infinite, battle.InfiniteMagic);
        Assert.Equal(shared, battle.MagicVitals!.MagicSystem.SharedMagicStock);
        if (shared)
        {
            Assert.Equal(1, hud.SelectedActor!.MagicRecordId);
            Assert.All(battle.MagicVitals.Records[1].MagicInventory, entry => Assert.Equal(0, entry.Count));
        }
        BattleActionDefinition action = Assert.Single(hud.ActionChoices);
        Assert.Equal(stock.ToString(CultureInfo.InvariantCulture), hud.ChoiceValue(action));
        Assert.Equal(stock < 100, action.Available);
        sounds.Clear();
        hud.Navigate(MenuInput.Confirm);

        Assert.True(hud.SelectingAction);
        Assert.False(hud.SelectingTarget);
        Assert.Equal(stock < 100, hud.SelectingDrawMode);
        Assert.Empty(inputs);
        Assert.Equal(stock < 100 ? MenuSound.Confirm : MenuSound.Error, Assert.Single(sounds));
        if (stock < 100)
        {
            hud.Navigate(MenuInput.Confirm);
            BattleInput input = Assert.Single(inputs);
            Assert.Equal(2, input.ActionId);
            Assert.Equal("source", input.TargetId);
            Assert.False(input.DrawCast);
        }
        else
        {
            Assert.Equal("Cure", Assert.Single(hud.ChoiceLabels));
            Assert.Equal(2, hud.SelectedAction!.Id);
        }
    }

    [Theory]
    [InlineData(false, "ally")]
    [InlineData(true, "fallen")]
    public void CastSelectsSourceThenSeparateRecipient(bool revive, string recipient)
    {
        BattleSession battle = Create(revive);
        List<BattleInput> inputs = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: inputs.Add);
        hud.Choose(0);
        Assert.True(hud.SelectingDrawSource);
        Assert.Equal("source", Assert.Single(hud.Targets).Id);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingAction);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingDrawMode);
        hud.Navigate(MenuInput.Down);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingDrawCastRecipient);
        Assert.Empty(inputs);
        hud.SelectTarget(recipient, true);
        BattleInput input = Assert.Single(inputs);
        Assert.Equal("source", input.TargetId);
        Assert.Equal(recipient, input.DrawCastTargetId);
        Assert.True(input.DrawCast);
        Assert.False(hud.SelectingTarget);
        input.Validate();
    }

    [Fact]
    public void CancelReturnsThroughModeSpellSourceAndCommandWithoutSubmitting()
    {
        BattleSession battle = Create(false);
        List<BattleInput> inputs = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: inputs.Add);
        hud.Choose(0);
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Down);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingDrawCastRecipient);
        hud.Navigate(MenuInput.Cancel);
        Assert.False(hud.SelectingDrawCastRecipient);
        Assert.True(hud.SelectingDrawMode);
        hud.Navigate(MenuInput.Cancel);
        Assert.True(hud.SelectingAction);
        Assert.False(hud.SelectingDrawMode);
        hud.Navigate(MenuInput.Cancel);
        Assert.True(hud.SelectingDrawSource);
        Assert.True(hud.SelectingTarget);
        Assert.Equal("source", hud.SelectedTarget!.Id);
        hud.Navigate(MenuInput.Cancel);
        Assert.False(hud.SelectingAction);
        Assert.False(hud.SelectingTarget);
        Assert.Empty(inputs);
    }

    [Fact]
    public void GuardianForceDrawUsesSavedNameAndStockWithoutCastRecipient()
    {
        BattleSession battle = Create(false, true);
        List<BattleInput> inputs = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: inputs.Add, guardianForceName: id => id == 1 ? "Custom Shiva" : $"GF{id}");
        hud.Choose(0);
        hud.Navigate(MenuInput.Confirm);
        Assert.Contains("Custom Shiva", hud.ChoiceLabels);
        Assert.True(hud.SelectedAction!.Available);
        Assert.Equal("", hud.ChoiceValue(hud.SelectedAction));
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal("Stock", Assert.Single(hud.ChoiceLabels));
        hud.Navigate(MenuInput.Right);
        hud.Navigate(MenuInput.Confirm);
        BattleInput input = Assert.Single(inputs);
        Assert.Equal(65, input.ActionId);
        Assert.Equal("source", input.TargetId);
        Assert.False(input.DrawCast);
        Assert.Null(input.DrawCastTargetId);
    }

    [Fact]
    public void SourceSelectionShowsOnlyItsOwnSpellsAndStockSubmitsWithoutARecipient()
    {
        BattleDefinition definition = Create(false).Capture().Definition;
        definition.Combatants[4] = definition.Combatants[4] with { NativeDrawSlots = [new(3, 0), new(0, 0), new(0, 0), new(0, 0)] };
        definition = definition with { ActionDefinitions = [.. definition.ActionDefinitions, new(BattleAbility.Magic, 3, "Fire", BattleActionEffect.Damage, 10, false)] };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        List<BattleInput> inputs = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: inputs.Add);
        hud.Choose(0);
        hud.SelectTarget("other", true);
        Assert.Equal("Fire", Assert.Single(hud.ChoiceLabels));
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(new[] { "Stock", "Cast" }, hud.ChoiceLabels);
        hud.Navigate(MenuInput.Confirm);
        BattleInput input = Assert.Single(inputs);
        Assert.Equal("other", input.TargetId);
        Assert.Equal(3, input.ActionId);
        Assert.False(input.DrawCast);
        Assert.Null(input.DrawCastTargetId);
        Assert.False(hud.SelectingAction);
        Assert.False(hud.SelectingTarget);
        Assert.False(hud.SelectingDrawMode);
    }

    [Fact]
    public void UnseenSpellUsesTheSavedKnowledgeBankWithoutHidingItsDrawSelection()
    {
        BattleDefinition definition = Create(false).Capture().Definition;
        NativeKnownMagic empty = new(NativeInputConfiguration.OriginalInitHash, System.Collections.Immutable.ImmutableArray.Create<uint>(0, 0));
        BattleSession battle = new(definition with { NativeKnownMagic = empty }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });
        hud.Choose(0);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal("????", Assert.Single(hud.ChoiceLabels));
        Assert.Equal(2, hud.SelectedAction!.Id);
    }

    [Fact]
    public void EmptyEnemiesCannotBeSelectedAsDrawSources()
    {
        BattleSession battle = Create(false);
        List<BattleInput> inputs = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: inputs.Add);
        hud.Choose(0);
        hud.SelectTarget("other", true);

        Assert.True(hud.SelectingDrawSource);
        Assert.Equal("source", Assert.Single(hud.Targets).Id);
        Assert.Empty(inputs);
    }

    [Fact]
    public void DrawIsUnavailableWhenNoEnemyHasEntries()
    {
        BattleDefinition definition = Create(false).Capture().Definition;
        definition.Combatants[3] = definition.Combatants[3] with { NativeDrawSlots = [new(0, 0), new(0, 0), new(0, 0), new(0, 0)] };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        List<MenuSound> sounds = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });
        hud.SoundRequested += sounds.Add;

        Assert.False(hud.DrawAvailable);
        Assert.Contains(BattleAbility.Draw, hud.Commands);
        hud.Choose(0);

        Assert.False(hud.SelectingAction);
        Assert.False(hud.SelectingTarget);
        Assert.Equal(MenuSound.Error, Assert.Single(sounds));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void DepletedSourceReturnsStaleMenusToSourceOrCommand(int stage, bool anotherSource)
    {
        BattleSession battle = CreateConcurrentDraw(anotherSource, false);
        List<BattleInput> inputs = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: inputs.Add);
        OpenDrawStage(hud, stage);
        ConsumeSourceMagic(battle);

        hud.BuildView();

        Assert.False(hud.SelectingAction);
        Assert.False(hud.SelectingDrawMode);
        Assert.False(hud.SelectingDrawCastRecipient);
        Assert.Equal(anotherSource, hud.SelectingDrawSource);
        Assert.Equal(anotherSource, hud.DrawAvailable);
        if (anotherSource)
        {
            Assert.Equal("other", Assert.Single(hud.Targets).Id);
        }
        Assert.Empty(inputs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DepletedSelectedSpellCannotSubmitAnotherSpellFromStaleMode(bool castRecipient)
    {
        BattleSession battle = CreateConcurrentDraw(false, true);
        List<BattleInput> inputs = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: inputs.Add);
        OpenDrawStage(hud, castRecipient ? 2 : 1);
        ConsumeSourceMagic(battle);

        hud.Navigate(MenuInput.Confirm);

        Assert.True(hud.SelectingAction);
        Assert.False(hud.SelectingDrawMode);
        Assert.False(hud.SelectingTarget);
        Assert.Equal(3, Assert.Single(hud.ActionChoices).Id);
        Assert.Equal("Fire", Assert.Single(hud.ChoiceLabels));
        Assert.Empty(inputs);
    }

    [Fact]
    public void ConsumingAnotherSpellPreservesTheSelectedDrawModeSpell()
    {
        BattleDefinition definition = CreateConcurrentDraw(false, true).Capture().Definition;
        definition.Combatants[3] = definition.Combatants[3] with { NativeDrawSlots = [new(2, 0), new(3, 0), new(4, 0), new(0, 0)] };
        definition = definition with { ActionDefinitions = [.. definition.ActionDefinitions, new(BattleAbility.Magic, 4, "Thunder", BattleActionEffect.Damage, 10, false)] };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        List<BattleInput> inputs = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: inputs.Add);
        OpenDrawStage(hud, 0);
        hud.Choose(1);
        Assert.True(hud.SelectingDrawMode);
        ConsumeSourceMagic(battle);

        hud.Navigate(MenuInput.Confirm);

        Assert.Equal(3, Assert.Single(inputs).ActionId);
    }

    private static void OpenDrawStage(BattleHudController hud, int stage)
    {
        hud.Choose(0);
        hud.SelectTarget("source", true);
        if (stage >= 1)
        {
            hud.Navigate(MenuInput.Confirm);
        }
        if (stage == 2)
        {
            hud.Navigate(MenuInput.Down);
            hud.Navigate(MenuInput.Confirm);
        }
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
        return new(definition, BattleAtbMode.TurnBased);
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
