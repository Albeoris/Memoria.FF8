using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class InstalledBattleActionCoverageTests
{
    [Fact]
    public void EveryInstalledPlayerSpellBattleItemAndJunctionableGuardianDispatchesAndRestores()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleActionDefinition[] actions = assets.ActionDefinitions.Where(action => action.Ability is BattleAbility.Magic or BattleAbility.Item or BattleAbility.GuardianForce).ToArray();
        Assert.Equal(104, actions.Length);
        List<string> failures = [];
        foreach (BattleActionDefinition action in actions)
        {
            try
            {
                BattleSession battle = Create(game, assets, action);
                bool revive = action.NativeAction?.AttackType is 5 or 6;
                string target = revive ? "ko" : action.Allies ? "ally" : "enemy";
                battle.QueueAction("caster", target, action.Ability, action.Id);
                battle.Tick();
                BattleSession restored = BattleSession.Restore(battle.Capture());
                for (int frame = 0; frame < 600 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0 || battle.GuardianForceCasts.Count != 0); frame++)
                {
                    battle.Tick();
                    restored.Tick();
                }
                Assert.Null(battle.ActiveAction);
                Assert.Empty(battle.QueuedActions);
                Assert.Empty(battle.GuardianForceCasts);
                Assert.Equal(JsonSerializer.Serialize(battle.Capture().Combatants), JsonSerializer.Serialize(restored.Capture().Combatants));
                Assert.Equal(JsonSerializer.Serialize(battle.Capture().NativeScriptState), JsonSerializer.Serialize(restored.Capture().NativeScriptState));
                Assert.Equal(JsonSerializer.Serialize(battle.Capture().Random), JsonSerializer.Serialize(restored.Capture().Random));
                Assert.Equal(battle.Items, restored.Items);
                if (action.Ability == BattleAbility.Magic)
                {
                    Assert.Equal(1, battle.Combatants[0].MagicStocks.Single(stock => stock.Id == action.Id).Count);
                }
                else if (action.Ability == BattleAbility.Item)
                {
                    Assert.Equal(1, battle.Items.Single(stock => stock.Id == action.Id).Count);
                }
            }
            catch (Exception exception)
            {
                failures.Add($"{action.Ability}/{action.Id} {action.Name}: {exception.GetType().Name}: {exception.Message}");
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static BattleSession Create(GameAssets game, BattleAssets assets, BattleActionDefinition action)
    {
        BattleCombatantDefinition caster = new()
        {
            Id = "caster", Name = "Caster", ModelId = 0, IsPlayer = true, Level = 8, MaximumHp = 1000, CurrentHp = 900, InitialAtb = BattleSession.AtbMaximum,
            Abilities = [action.Ability], EquipmentIds = Enumerable.Range(0, 16).ToArray(), GuardianForceCompatibility = Enumerable.Repeat((ushort)1000, 16).ToArray(),
            MagicStocks = action.Ability == BattleAbility.Magic ? [new(action.Id, 2)] : [], NativeStatusResistances = new byte[40]
        };
        return new(new BattleDefinition
        {
            Name = "Declared exhaustive installed player action component",
            NativeFieldBattleRules = 0,
            NativeActions = assets.NativeActions.ToArray(), ActionDefinitions = assets.ActionDefinitions.ToArray(),
            GuardianForces = Enumerable.Range(0, 16).Select(id => new BattleGuardianForceVital(id, 1000, 1000)).ToArray(), NativeBattleSpeed = 0,
            NativePocketStation = game.ReadNewGamePocketStation(), NativeSavedBattleFlags = 0, NativeSavedBattleFlagsKnownMask = 255,
            NativeScannedModels = game.ReadNewGameScannedModels(), NativeScanAlwaysShow = false, NativeElapsedCounter = 1000,
            NativeStatusRules = assets.ReadNativeStatusRules(0), NativeMessageSpeed = 0, NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            Items = action.Ability == BattleAbility.Item ? [new(action.Id, 2)] : [],
            Combatants =
            [
                caster,
                new() { Id = "ally", Name = "Ally", ModelId = 1, IsPlayer = true, MaximumHp = 1000, CurrentHp = 500, Speed = 1, NativeStatusResistances = new byte[40] },
                new() { Id = "ko", Name = "KO", ModelId = 2, IsPlayer = true, MaximumHp = 1000, CurrentHp = 0, NativeStatus0 = 1, Speed = 1, NativeStatusResistances = new byte[40] },
                new() { Id = "enemy", Name = "Enemy", ModelId = 0, MaximumHp = 1000000, CurrentHp = 1000000, Speed = 1, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.TurnBased);
    }
}
