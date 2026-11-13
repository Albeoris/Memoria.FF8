using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    private const int PetrifyStareEffect = 127;
    private const ushort PetrifyStareFormation = 666;

    [Fact]
    public void Installed_Petrify_Stare_reacts_to_original_party_magic()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleEffectBinding effect = Assert.Single(game.ReadNativeEffectCatalog(), binding => binding.Effect == PetrifyStareEffect);
        EnemyScenarioScriptedAction[] scripts = ScenarioEnemyDiscovery.Discover(assets).ScriptedActions.Where(script => script.NativeEffectId == PetrifyStareEffect).ToArray();

        EnemyRouteResult result = RunExpandedPetrifyStare(directory, PrepareField(directory), assets, effect, scripts);

        Assert.True(result.Status is "native" or "logical", result.Detail);
        Assert.Contains($"Formation {PetrifyStareFormation},", result.Detail);
        Assert.Contains($"band {HigherEnemyBand}, callback 2, command 8/29, sequence 14,", result.Detail);
        Assert.Contains("genuine Fire setup", result.Detail);
    }

    private static EnemyRouteResult RunExpandedPetrifyStare(string directory, GameState field, BattleAssets assets, NativeBattleEffectBinding effect, EnemyScenarioScriptedAction[] scripts)
    {
        EnemyScenarioScriptedAction script = Assert.Single(scripts, script => script is { ModelId: 16, Callback: 2, LevelBand: HigherEnemyBand, Command: 8, ActionId: 29, Sequence: 14 } &&
            script.Formations.Any(scene => scene.FormationId == PetrifyStareFormation && scene.Enabled));
        BattleActionDefinition fire = Assert.Single(assets.ActionDefinitions, action => action is { Ability: BattleAbility.Magic, Id: 1 });
        EnemyAttackSetup setup = new(PetrifyStareFormation, script.ModelId, 1, 0, script.LevelBand);
        EnemyRouteResult? blocker = null;
        foreach (byte seed in new byte[] { 0, 5, 9 })
        {
            using GameWorld world = StartBattle(directory, field, PetrifyStareFormation, fire, nativeSeed: seed, prepareHigherBand: true);
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            BattleCombatant owner = battle.Combatants.Last(actor => !actor.IsPlayer && actor.ModelId == script.ModelId && actor.NativeAi?.LevelBand == script.LevelBand);
            EnemyAttackPreparation preparation = new(setup, owner, battle.Events.Count, fire);
            world.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: BattleAtbMode.Normal));
            EnemyRouteResult result = ObserveEnemyCast(world, battle, effect, new() { [owner.Id] = [script] }, preparation);
            if (result.Status is "native" or "logical")
            {
                return result with { Detail = $"Formation {PetrifyStareFormation}, seed {seed}, {result.Detail}" };
            }
            blocker = result with { Detail = $"Formation {PetrifyStareFormation}, seed {seed}, {result.Detail}" };
        }
        return blocker!;
    }
}
