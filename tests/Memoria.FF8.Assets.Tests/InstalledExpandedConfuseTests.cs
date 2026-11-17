using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class InstalledExpandedConfuseTests
{
    [Fact]
    public void EveryInstalledConfuseStockRecordHasAnExpandedContactContract()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleNativeAction[] spells = assets.NativeActions.Where(action => action.Command == 2 && action.ActionId is >= 1 and <= 56).ToArray();
        BattleNativeAction[] items = assets.NativeActions.Where(action => action.Command == 4 && action.ActionId > 0 && action.RawRecord.Length == 24 && (action.RawRecord[21] & 1) != 0).ToArray();
        Assert.Equal(Enumerable.Range(1, 56), spells.Select(action => (int)action.ActionId).Order());
        Assert.Equal(Enumerable.Range(1, 29), items.Select(action => (int)action.ActionId).Order());
        foreach (BattleNativeAction action in spells.Concat(items))
        {
            BattleAbility ability = action.Command == 2 ? BattleAbility.Magic : BattleAbility.Item;
            Assert.True(ExpandedBattleActionRules.SupportsCompelledAction(ability, action), $"{action.Command}/{action.ActionId} {action.Name}");
        }
    }

    [Theory]
    [InlineData(2, 27)]
    [InlineData(2, 51)]
    [InlineData(2, 53)]
    [InlineData(4, 9)]
    [InlineData(4, 10)]
    [InlineData(4, 17)]
    public void ConfuseExecutesAndSpendsOneOfThePreviouslyBlockedInstalledActions(byte command, ushort id)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        using GameAssets game = new(TestResources.GameDirectory);
        BattleAbility ability = command == 2 ? BattleAbility.Magic : BattleAbility.Item;
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, action => action.Ability == ability && action.Id == id);
        bool ejects = action.NativeAction!.Statuses1 == 0x10000;
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        NativeCharacterVital row = vitals.Records[1];
        BattleNativePlayerStatRules stats = assets.ReadPlayerNativeStatRules(vitals, 1);
        BattleNativeCharacterGrowth growth = new(row.HitPointBonus, [.. row.StatBonuses]);
        NativeMagicStock[] stocks = BattleCombatant.ExpandMagicStocks(command == 2 ? [new BattleStock(id, 2)] : []);
        BattleNativePlayerStatistics projected = stats.Project(stocks, growth);
        BattleSession battle = new(new()
        {
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 1, CurrentHp = projected.MaximumHp / 2, MaximumHp = projected.MaximumHp, Speed = projected.Speed, Magic = projected.Magic, InitialAtb = BattleSession.AtbMaximum,
                    NativeStatus0 = 2, NativeStatus1 = 0x4000, NativeCommandSlots = [command, command, command, command], Abilities = [ability], NativePlayerStatRules = stats,
                    NativeGrowth = growth, NativeStatusResistances = stats.ProjectStatusResistances(stocks).ToArray(), NativeMagicInventory = stocks },
                new() { Id = "enemy", Name = "Enemy", CurrentHp = 300, MaximumHp = 500, Speed = 1, NativeStatusResistances = new byte[40] }
            ],
            ActionDefinitions = [action],
            Items = command == 4 ? [new BattleStock(id, 2)] : [],
            NativeActions = assets.NativeActions.Where(candidate => candidate.ActionId == id && (candidate.Command == command || command == 2 && candidate.Command == 247)).ToArray(),
            NativeFieldBattleRules = 0,
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            InitialRandom = NativeBattleRandomState.ForecastPhase(8)
        }, BattleAtbMode.TurnBased);
        battle.Tick();
        Assert.Equal(command, battle.ActiveAction!.NativeQueuedCommand);
        Assert.Equal(id, battle.ActiveAction.ActionId);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame < 1200 && !(battle.ActiveAction is null && battle.QueuedActions.Count == 0); frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(1, Assert.Single(command == 2 ? battle.Combatants[0].MagicStocks : battle.Items).Count);
        Assert.Contains(battle.Events, entry => entry.Kind == "ActionComplete" && entry.ActorId == "party");
        Assert.Equal(ejects, battle.Combatants[1].IsEjected);
        if (!ejects)
        {
            Assert.Equal(0, battle.Combatants[0].PersistentStatus & 2);
        }
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }
}
