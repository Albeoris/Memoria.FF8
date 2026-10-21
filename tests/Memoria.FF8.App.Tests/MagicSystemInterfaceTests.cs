using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MagicSystemInterfaceTests
{
    [Fact]
    public void NewGameExposesThreeIndependentMagicRules()
    {
        MenuController menu = new();
        menu.Open("new-game-options", demo: true);
        Assert.Contains(menu.Entries, entry => entry.Id == "Shared magic stock");
        Assert.Contains(menu.Entries, entry => entry.Id == "Infinite magic");
        Assert.Contains(menu.Entries, entry => entry.Id == "Smart Attack");
        Assert.All(new[] { "Shared magic stock", "Infinite magic", "Smart Attack" }, key => Assert.Equal("Off", menu.Data.Settings[key]));
    }

    [Fact]
    public void MigrationPreviewDoesNotMutateUntilAppliedAndCancelLeavesItUnchanged()
    {
        MenuData data = new();
        MenuController menu = new(() => data);
        menu.Open("config-gameplay");
        int applications = 0;
        menu.ShowMagicRulesReview(["Fire ×50 moved; ×50 overflow retained", "HP 500 → 300"], () => applications++);
        Assert.Equal(0, applications);
        Assert.Contains("HP 500 → 300", menu.CurrentScreen.Details(menu));
        menu.Execute(new MenuAction("magic-rules-cancel"));
        Assert.Equal("config-gameplay", menu.ScreenId);
        Assert.Equal(0, applications);
        menu.ShowMagicRulesReview(["Fire ×50 moved"], () => applications++);
        menu.Execute(new MenuAction("magic-rules-apply"));
        Assert.Equal(1, applications);
        Assert.Equal("config-gameplay", menu.ScreenId);
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
        menu.Open("config-gameplay");
        _ = menu.Data;
        int cancellations = 0;
        menu.ShowMagicRulesReview(["Fire �50 moved"], () => Assert.Fail("Unexpected migration."), () => cancellations++);
        menu.Back();
        Assert.Equal(1, cancellations);
        Assert.Equal("config-gameplay", menu.ScreenId);
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
        Assert.False(menu.Entries[0].Enabled);
        Assert.Equal("SmartMagic", data.Characters[0].AttackMode);
    }

    [Fact]
    public void SharedInventoryDisablesExchangeWhileStockAndUsesRemainDistinct()
    {
        MenuData data = new() { SharedMagicStock = true };
        data.Characters.Add(new MenuCharacter { Id = "character-0", InParty = true });
        data.Magic.Add(new MenuMagic { Id = "magic-0-1", CharacterId = "character-0", Name = "Cure", Quantity = 100, RemainingUses = 0, Capacity = 4, Owner = "Owner: Caster", Availability = "Exhausted" });
        MenuController menu = new(() => data);
        menu.Open("magic");
        Assert.False(menu.Entries.Single(entry => entry.Id == "magic-exchange").Enabled);
        menu.Open("magic-stock");
        Assert.Contains("×100", menu.Entries[0].Label);
        Assert.Contains("0/4", menu.Entries[0].Label);
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
        Assert.False(PlayerSaveStore.RequiresMemoriaFormat(new GameState()));
    }
}
