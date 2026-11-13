using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(78, 248, HigherEnemyBand)]
    [InlineData(153, 37, HigherEnemyBand)]
    [InlineData(154, 37, HigherEnemyBand)]
    [InlineData(158, 413, HigherEnemyBand)]
    [InlineData(170, 71, HigherEnemyBand)]
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
        if (HpTurnSetup(effectId) is { } setup)
        {
            Assert.Contains(setup.UsesDemi ? "genuine Demi setup" : "genuine Attack setup", result.Detail);
        }
    }

    private static EnemyAttackSetup? HpTurnSetup(int effect) => effect switch
    {
        78 => new(248, 49, 0, 5, HigherEnemyBand, UsesDemi: true),
        // c0m017 callback 1 tests the original owner's HP against five tenths before either action.
        153 or 154 => new(37, 17, 0, 5, HigherEnemyBand),
        158 => new(413, 24, 0, 3, HigherEnemyBand, UsesDemi: true),
        170 => new(71, 36, 0, 0, HigherEnemyBand, UsesDemi: true, QuarterHp: true),
        186 => new(247, 57, 0, 5, BlasterEnemyBand, UsesDemi: true),
        _ => null
    };

    private const byte DemiSpellId = 13;

    private static void PrepareDemiStock(NativeCharacterVital[] records)
    {
        NativeCharacterVital owner = records[10];
        Assert.Equal(DemiSpellId, owner.MagicInventory[0].SpellId);
        foreach (int row in new[] { 0, 1, 2, 3, 10 })
        {
            NativeCharacterVital actor = records[row];
            NativeMagicStock[] stock = actor.MagicInventory.ToArray();
            int slot = Array.FindIndex(stock, entry => entry.SpellId == DemiSpellId);
            if (slot < 0)
            {
                slot = Array.FindIndex(stock, entry => entry.SpellId == 0);
            }
            Assert.InRange(slot, 0, stock.Length - 1);
            stock[slot] = new(DemiSpellId, 5);
            records[row] = actor with { Junction = actor.Junction with { Commands = owner.Junction.Commands }, MagicInventory = stock };
        }
    }

    private static int? SeededTurnFormation(int effect) => effect switch
    {
        68 => 377,
        238 => 117,
        345 => 247,
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
