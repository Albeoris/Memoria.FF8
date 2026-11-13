using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(153, 37, HigherEnemyBand)]
    [InlineData(154, 37, HigherEnemyBand)]
    [InlineData(238, 117, HigherEnemyBand)]
    public void Installed_enemy_turn_follows_original_hp_band_and_random_branches(int effectId, int formation, int band)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleEffectBinding effect = Assert.Single(game.ReadNativeEffectCatalog(), binding => binding.Effect == effectId);
        EnemyScenarioScriptedAction[] scripts = ScenarioEnemyDiscovery.Discover(assets).ScriptedActions.Where(script => script.NativeEffectId == effectId).ToArray();
        EnemyRouteResult result = RunDiscoveredEnemyCast(directory, PrepareField(directory), assets, effect, scripts, probeEnemies: true);
        Assert.True(result.Status is "native" or "logical", result.Detail);
        Assert.Contains($"Formation {formation},", result.Detail);
        Assert.Contains($"band {band},", result.Detail);
        if (HpTurnSetup(effectId) is not null)
        {
            Assert.Contains("genuine Attack setup", result.Detail);
        }
    }

    private static EnemyAttackSetup? HpTurnSetup(int effect) => effect switch
    {
        // c0m017 callback 1 tests the original owner's HP against five tenths before either action.
        153 or 154 => new(37, 17, 0, 5, HigherEnemyBand),
        _ => null
    };

    private static int? SeededTurnFormation(int effect) => effect switch
    {
        238 => 117,
        _ => null
    };

    private static void PrepareThreeLivingParty(NativeCharacterVital[] records)
    {
        for (int index = 3; index < records.Length; index++)
        {
            records[index] = records[index] with { HitPoints = new(0, 0), Status = 1 };
        }
    }
}
