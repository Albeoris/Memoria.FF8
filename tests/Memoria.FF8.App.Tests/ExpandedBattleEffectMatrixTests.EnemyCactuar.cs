using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Fact]
    public void Installed_Cactuar_uses_its_original_random_turn_branch()
    {
        const ushort effectId = 177;
        const int model = 55;
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleEffectBinding effect = Assert.Single(game.ReadNativeEffectCatalog(), binding => binding.Effect == effectId);
        EnemyScenarioScriptedAction[] scripts = ScenarioEnemyDiscovery.Discover(assets).ScriptedActions
            .Where(script => script.ModelId == model && script.NativeEffectId == effectId).ToArray();
        EnemyRouteResult result = RunCactuarTurn(directory, PrepareField(directory), effect, scripts);
        Assert.True(result.Status is "native" or "logical", result.Detail);
        Assert.Contains("Formation 394, seed 121, c0m055 ", result.Detail);
        Assert.Contains("band 1, callback 1, command 8/25, sequence 13, 1 contact(s)", result.Detail);
    }

    private static EnemyRouteResult RunCactuarTurn(string directory, GameState field, NativeBattleEffectBinding effect, EnemyScenarioScriptedAction[] scripts)
    {
        const ushort formation = 394;
        // Cactuar's callback draws for both predicates, its target, and then its ability slot.
        const byte seed = 121;
        using GameWorld world = StartBattle(directory, field, formation, nativeSeed: seed);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        world.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: BattleAtbMode.Normal));
        Dictionary<string, EnemyScenarioScriptedAction[]> actors = battle.ReadDefinition().Combatants
            .Where(actor => !actor.IsPlayer && actor.NativeAi is not null)
            .ToDictionary(actor => actor.Id, actor => scripts.Where(script => script.ModelId == actor.ModelId && script.LevelBand == actor.NativeAi!.LevelBand).ToArray());
        Assert.NotEmpty(actors);

        EnemyRouteResult result = ObserveEnemyCast(world, battle, effect, actors);
        return result with { Detail = $"Formation {formation}, seed {seed}, {result.Detail}" };
    }
}
