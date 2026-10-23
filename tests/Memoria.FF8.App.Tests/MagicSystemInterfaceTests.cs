using System.Collections.Immutable;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MagicSystemInterfaceTests
{
    [Fact]
    public void NewGameExposesFourIndependentMagicRules()
    {
        MenuController menu = new();
        menu.Open("new-game-options", demo: true);
        Assert.Contains(menu.Entries, entry => entry.Id == "Draw once per spell");
        Assert.Contains(menu.Entries, entry => entry.Id == "Shared magic stock");
        Assert.Contains(menu.Entries, entry => entry.Id == "Infinite magic");
        Assert.Contains(menu.Entries, entry => entry.Id == "Smart Attack");
        Assert.All(new[] { "Draw once per spell", "Shared magic stock", "Infinite magic", "Smart Attack" }, key => Assert.Equal("Off", menu.Data.Settings[key]));
    }

    [Fact]
    public void MigrationPreviewDoesNotMutateUntilAppliedAndCancelLeavesItUnchanged()
    {
        MenuData data = new();
        MenuController menu = new(() => data);
        menu.Open("config-magic");
        int applications = 0;
        menu.ShowMagicRulesReview(["Fire ×50 moved; ×50 overflow retained", "HP 500 → 300"], () => applications++);
        Assert.Equal(0, applications);
        Assert.Contains("HP 500 → 300", menu.CurrentScreen.Details(menu));
        menu.Execute(new MenuAction("magic-rules-cancel"));
        Assert.Equal("config-magic", menu.ScreenId);
        Assert.Equal(0, applications);
        menu.ShowMagicRulesReview(["Fire ×50 moved"], () => applications++);
        menu.Execute(new MenuAction("magic-rules-apply"));
        Assert.Equal(1, applications);
        Assert.Equal("config-magic", menu.ScreenId);
    }

    [Fact]
    public void EscapeCancelsDeferredMigrationAndInvalidatesThePendingPresentation()
    {
        MenuData data = new();
        int reads = 0;
        MenuController menu = new(() =>
        {
            reads++;
            return data;
        });
        menu.Open("config-magic");
        _ = menu.Data;
        int cancellations = 0;
        menu.ShowMagicRulesReview(["Fire ×50 moved"], () => Assert.Fail("Unexpected migration."), () => cancellations++);
        menu.Back();
        Assert.Equal(1, cancellations);
        Assert.Equal("config-magic", menu.ScreenId);
        _ = menu.Data;
        Assert.True(reads >= 2);
    }

    [Fact]
    public void FirstAttackSlotHasSavedPersonalChoiceAndThreeNativeSlotsRetainTheirIds()
    {
        MenuData data = new() { SmartAttack = true };
        data.Characters.Add(new MenuCharacter { Id = "character-0", Name = "Caster", InParty = true, AttackMode = "SmartMagic" });
        data.SupportedActions.Add("attack-mode");
        MenuAction? requested = null;
        MenuController menu = new(() => data, action => requested = action);
        menu.Open("junction-command");
        Assert.Equal(new[] { "attack", "1", "2", "3" }, menu.Entries.Select(entry => entry.Id));
        Assert.True(menu.Entries[0].Enabled);
        menu.Execute(menu.Entries[0].Action!);
        Assert.Equal(new MenuAction("attack-mode", TargetId: "character-0", Value: "PhysicalOnly"), requested);
        data.SmartAttack = false;
        Assert.Equal(new[] { "1", "2", "3" }, menu.Entries.Select(entry => entry.Id));
        Assert.Equal("SmartMagic", data.Characters[0].AttackMode);
    }

    [Fact]
    public void SharedInventoryDisablesExchangeAndDisplaysRemainingUsesAndStock()
    {
        MenuData data = new() { SharedMagicStock = true };
        data.Characters.Add(new MenuCharacter { Id = "character-0", InParty = true });
        data.Magic.Add(new MenuMagic { Id = "magic-0-1", CharacterId = "character-0", Name = "Cure", Quantity = 100, RemainingUses = 0, Capacity = 4, OwnerIcon = new UiImageSource(UiImageKind.CharacterPortrait, 3), Availability = "Exhausted" });
        MenuController menu = new(() => data);
        menu.Open("magic");
        Assert.False(menu.Entries.Single(entry => entry.Id == "magic-exchange").Enabled);
        menu.Open("magic-stock");
        Assert.Equal("Cure", menu.Entries[0].Label);
        Assert.Equal("0/4  100", menu.Magic!.StockPresentation);
        Assert.Equal(100, menu.Magic!.Quantity);
    }

    [Fact]
    public void PointerHoverChangesTargetWithoutConfirmingAndPhysicalOverrideUsesOneMappedButton()
    {
        BattleSession battle = new(new BattleDefinition
        {
            Combatants = [new() { Id = "player", Name = "Player", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "first", Name = "First" }, new() { Id = "second", Name = "Second" }]
        }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { });
        hud.Attack();
        Assert.True(hud.SelectingTarget);
        UiView view = hud.BuildView();
        view.Pointer(80, 567, false);
        Assert.Equal("second", hud.SelectedTarget!.Id);
        Assert.Empty(battle.QueuedActions);
        hud.Navigate(MenuInput.Up);
        Assert.Equal("first", hud.SelectedTarget!.Id);
        view.Pointer(80, 567, false);
        Assert.Equal("first", hud.SelectedTarget!.Id);
        hud.SetNativeCommandButtons(0x01);
        Assert.True(hud.ForcePhysical);
        Assert.True(hud.TargetActionPreview!.ForcePhysical);
        hud.SetNativeCommandButtons(0x03);
        Assert.False(hud.ForcePhysical);
        hud.SetNativeCommandButtons(0);
        Assert.False(hud.ForcePhysical);
    }

    [Fact]
    public void OpeningNativeCommandMenuPreservesSmartMagicPreviewAndConfirmation()
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
            CombatParty = [3, 255, 255],
            DreamEnabled = false,
            GuardianForces = Enumerable.Range(0, 16).Select(_ => new NativeGuardianForceVital(0, 1, 10, new NativeGuardianForceLearnedBank(0x3FFFF, 0), 10, new byte[21].ToImmutableArray(), 0, 0)).ToArray(),
            Definitions = new NativeStatDefinitions(Enumerable.Repeat(curve, 11).ToImmutableArray(), Enumerable.Repeat((byte)1, 57).ToImmutableArray(), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray()),
            MenuDefinitions = new NativeGuardianForceMenuDefinitions(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), new byte[116].ToImmutableArray(), new byte[19].ToImmutableArray(), new byte[5].ToImmutableArray()),
            DreamWeapons = new byte[3],
            JunctionPartyBackup = new byte[6]
        };
        vitals = vitals.ChangeMagicStock(3, 4, 1, MagicOperationReason.ScriptReward, out _);
        vitals = MagicModeCoordinator.Preview(vitals, new GameplayPreferences { SmartAttack = true }).Vitals;
        vitals.MagicSystem.AttackModes[3] = MagicAttackMode.SmartMagic;
        BattleNativeAction blizzard = new(2, 4, 1, 4)
        {
            Name = "Blizzard", MagicId = 4, AttackType = 2, Power = 20, HitCount = 1, TargetFlags = 0x50, AttackFlags = 1, DamagePresentation = 0, Element = 2, ElementPercent = null, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, RawRecord = [], SupplementalRecord = []
        };
        BattleNativeAction attack = blizzard with { Command = 1, ActionId = 0, MagicId = 0, Name = "Attack", AttackType = 1, Power = 1, Hit = 255, Critical = 0, Element = 0 };
        BattleSession battle = new(new BattleDefinition
        {
            MagicVitals = vitals,
            NativeLimitsDisabled = false,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = [blizzard],
            ActionDefinitions = [new(BattleAbility.Magic, 4, "Blizzard", BattleActionEffect.Native, 0, false) { NativeAction = blizzard }],
            Combatants =
            [
                new() { Id = "quistis", Name = "Quistis", IsPlayer = true, ModelId = 3, MagicRecordId = 3, InitialAtb = BattleSession.AtbMaximum, Attack = 1, Magic = 100, Abilities = [BattleAbility.Attack, BattleAbility.Magic], NativeLimit = new(3, 15, 0, 255, new byte[32].ToImmutableArray()), NativeAttack = attack, NativeGrowth = new(0, new byte[6].ToImmutableArray()), NativeCommandSlots = [1, 2, 0, 0] },
                new() { Id = "enemy", Name = "Enemy", ModelId = 2, MaximumHp = 100, CurrentHp = 100, ElementDefenses = [800, 800, 800, 800, 800, 800, 800, 800] }
            ]
        }, BattleAtbMode.TurnBased);
        Assert.False(battle.HasNativeLimitSelection("quistis"));
        List<BattleInput> inputs = [];
        BattleHudController hud = new(battle, (_, _) => Assert.Fail("Expected the battle input dispatcher."), () => { }, () => { }, execute: inputs.Add);
        Assert.True(battle.HasNativeLimitSelection("quistis"));
        Assert.Equal((byte)0, battle.GetNativeLimitLevel("quistis"));
        Assert.Equal(1, battle.GetAvailableMagicCasts("quistis", 4));

        hud.Attack();

        Assert.True(hud.SelectingTarget);
        Assert.Equal(BattleAbility.Magic, hud.TargetActionPreview!.Ability);
        Assert.Equal("Blizzard", hud.TargetActionCaption);
        hud.SetNativeCommandButtons(0x01);
        Assert.True(hud.ForcePhysical);
        Assert.Equal(BattleAbility.Attack, hud.TargetActionPreview!.Ability);
        hud.SetNativeCommandButtons(0);
        BattleSmartAttackPreview preview = hud.TargetActionPreview!;
        Assert.Equal(BattleAbility.Magic, preview.Ability);

        hud.Attack();

        BattleInput input = Assert.Single(inputs);
        Assert.Equal(BattleInputKind.Attack, input.Kind);
        Assert.Equal("quistis", input.ActorId);
        Assert.Equal("enemy", input.TargetId);
        Assert.False(input.ForcePhysical);
        Assert.Equal(preview, input.SmartAttackPreview);
    }

    [Fact]
    public void FeatureBearingSavesOfferMemoriaSlotsAndPreserveOriginalBankSelectionForLoads()
    {
        string directory = Path.Combine(Path.GetTempPath(), "magic-save-routing-" + Guid.NewGuid().ToString("N"));
        PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), directory);
        PlayerSaveMenu menu = new(store) { RequiresMemoriaFormat = () => true };
        menu.Open(PlayerSaveMenuMode.Save);
        Assert.Equal(0, menu.Bank);
        Assert.Contains("Memoria", menu.Message!);
        Assert.False(menu.SelectBank(1));
        Assert.Equal(0, menu.Bank);
        Assert.False(Directory.Exists(directory));
        menu.Open(PlayerSaveMenuMode.Load);
        Assert.True(menu.SelectBank(1));
        Assert.Equal(1, menu.Bank);
        Assert.True(PlayerSaveStore.RequiresMemoriaFormat(new GameState { GameplayPreferences = new() { SmartAttack = true } }));
        Assert.True(PlayerSaveStore.RequiresMemoriaFormat(new GameState { GameplayPreferences = new() { GuardianFormsEnabled = true } }));
        Assert.False(PlayerSaveStore.RequiresMemoriaFormat(new GameState()));
    }
}
