using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    private const int BlasterEnemyBand = 1;
    // Level 33 gives Torama levels 27 or 39 so its high-band spells leave the party's half-HP gate reachable.
    private const int BlasterPartyLevel = 33;

    private static void PrepareBlasterParty(NativeCharacterVital[] records)
    {
        PrepareDemiStock(records);
        for (int row = 0; row < records.Length; row++)
        {
            records[row] = records[row] with { HitPointBonus = 7200, HitPoints = new(7200, 7200) };
        }
    }

    [Theory]
    [InlineData(186, 13, BlasterEnemyBand)]
    [InlineData(345, 11, HigherEnemyBand)]
    public void Installed_Torama_turn_dispatches_its_original_stance_or_level_branch(int effectId, int sequence, int band)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleEffectBinding effect = Assert.Single(game.ReadNativeEffectCatalog(), binding => binding.Effect == effectId);
        EnemyScenarioScriptedAction[] scripts = ScenarioEnemyDiscovery.Discover(assets).ScriptedActions
            .Where(script => script.ModelId == 57 && script.NativeEffectId == effectId).ToArray();

        EnemyRouteResult result = RunDiscoveredEnemyCast(directory, PrepareField(directory), assets, effect, scripts, probeEnemies: true);

        Assert.True(result.Status is "native" or "logical", result.Detail);
        Assert.Contains("Formation 247,", result.Detail);
        Assert.Contains("c0m057 ", result.Detail);
        Assert.Contains($"band {band}, callback 1,", result.Detail);
        Assert.Contains($"sequence {sequence},", result.Detail);
        if (effectId == 186)
        {
            // Below half HP, action 132 sets Torama's local stance flag before either Blaster action is reachable.
            Assert.Matches(@"command 8/12[23],", result.Detail);
            Assert.Contains("genuine Demi setup", result.Detail);
        }
        else
        {
            Assert.Contains("command 8/124,", result.Detail);
        }
    }
}
