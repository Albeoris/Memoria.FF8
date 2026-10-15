using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleDebugDefaultActionsTests
{
    [Fact]
    public void DefaultEditorCopiesProjectedActionsAndOwnedStocksThroughPresetRoundTrip()
    {
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets resources = new(assets.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        BattleActionDefinition[] actions = BattlePartyProjection.CreateActions(vitals, resources.ActionDefinitions);
        BattleStock[] items = assets.ReadNewGameEconomy().Items.Where(stock => stock.ItemId > 0 && stock.Count > 0).Select(stock => new BattleStock(stock.ItemId, stock.Count)).ToArray();
        // Explicit laboratory actor stock is separate from the authentic New Game owner above.
        BattleCombatantDefinition actor = new() { Id = "party-0", Name = "Squall", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Attack, BattleAbility.Magic, BattleAbility.GuardianForce, BattleAbility.Item], EquipmentIds = [0], MagicStocks = [new(1, 4)] };
        BattleDefinition initial = BattleDebugDefaults.Create([actor], null, 0, actionDefinitions: actions, items: items);
        using TemporaryPresetFile file = new();
        BattleDebugPresets presets = new(file.Path);
        BattleDefinition? started = null;
        BattleDebugController editor = new(initial, new BattleDebugCatalog(), BattleAtbMode.TurnBased, "NewGameFullCombat", false, (definition, _) => started = definition, (_, _, _) => { }, () => { }, presets);
        presets.Save(0, editor.Definition, editor.Mode);
        BattleDebugPreset loaded = new BattleDebugPresets(file.Path).Read(0)!;
        BattleDebugController reopened = new(loaded.Definition, new BattleDebugCatalog(), loaded.Mode, "NewGameFullCombat", false, (definition, _) => started = definition, (_, _, _) => { }, () => { }, presets);
        reopened.Start();
        Assert.NotNull(started);
        Assert.Equal(items, started.Items);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(actions), System.Text.Json.JsonSerializer.Serialize(started.ActionDefinitions));
        Assert.Equal(new BattleStock(1, 4), Assert.Single(started.Combatants[0].MagicStocks));
        foreach (BattleActionDefinition gf in started.ActionDefinitions.Where(action => action.Ability == BattleAbility.GuardianForce))
        {
            Assert.Equal(NativeStatDefinitions.Level(vitals.GuardianForces[gf.Id].Experience, vitals.Definitions.GuardianForces[gf.Id]), gf.Level);
            Assert.Equal(vitals.GuardianForces[gf.Id].HitPoints > 0 && (vitals.GuardianForces[gf.Id].Flags & 1) != 0, gf.Available);
        }
        BattleSession battle = new(started, BattleAtbMode.TurnBased);
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { }, execute: input => battle.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId, input.DrawCast));
        hud.Navigate(MenuInput.Down);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal("Fire", Assert.Single(hud.ActionChoices).Name);
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(BattleAbility.Magic, Assert.Single(battle.QueuedActions).Ability);
        Assert.Equal(3, Assert.Single(battle.Combatants[0].MagicStocks).Count);
        actions[0] = actions[0] with { Power = 255 };
        items[0] = items[0] with { Count = 1 };
        Assert.NotEqual(255, presets.Read(0)!.Definition.ActionDefinitions[0].Power);
        Assert.NotEqual(1, presets.Read(0)!.Definition.Items[0].Count);
    }

    [Fact]
    public void ExplicitCustomScenarioAndPresetDoNotAcquireInstalledActionsOrStock()
    {
        BattleDefinition custom = new()
        {
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Custom action", BattleActionEffect.Heal, 3, true)],
            Items = [new(91, 2)],
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, BattleDebugDefaults.Dummy]
        };
        BattleDebugPresets presets = new();
        presets.Save(0, custom, BattleAtbMode.TurnBased);
        BattleDebugController editor = new(presets.Read(0)!.Definition, new BattleDebugCatalog(), BattleAtbMode.TurnBased, "NewGameFullCombat", false, (_, _) => { }, (_, _, _) => { }, () => { });
        Assert.Equal("Custom action", Assert.Single(editor.Definition.ActionDefinitions).Name);
        Assert.Equal(new BattleStock(91, 2), Assert.Single(editor.Definition.Items));
    }

    [Fact]
    public void OpeningDefaultEditorDuringCombatCopiesCurrentMagicAndItemStocks()
    {
        BattleCombatantDefinition actor = new() { Id = "party-0", Name = "Squall", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Attack, BattleAbility.Magic], MagicStocks = [new(1, 2)] };
        BattleDefinition definition = new() { Combatants = [actor, BattleDebugDefaults.Dummy], ActionDefinitions = [new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Damage, 18, false)], Items = [new(1, 2)] };
        BattleSession active = new(definition, BattleAtbMode.TurnBased);
        active.QueueAction("party-0", "enemy-0", BattleAbility.Magic, 1);
        BattleDefinition defaults = BattleDebugDefaults.Create([actor], null, 0, active, definition.ActionDefinitions, [new(1, 99)]);
        Assert.Equal(new BattleStock(1, 1), Assert.Single(defaults.Combatants[0].MagicStocks));
        Assert.Equal(new BattleStock(1, 2), Assert.Single(defaults.Items));
        Assert.Single(defaults.ActionDefinitions);
        Assert.Equal(2, Assert.Single(actor.MagicStocks).Count);
    }

    private sealed class TemporaryPresetFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ff8-default-actions-" + Guid.NewGuid().ToString("N") + ".json");

        public void Dispose()
        {
            File.Delete(Path);
            File.Delete(Path + ".tmp");
        }
    }
}
