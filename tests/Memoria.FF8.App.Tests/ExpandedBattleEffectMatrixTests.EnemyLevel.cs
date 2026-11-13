using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    private const int HigherBandPartyLevel = 40;
    private const int HigherEnemyBand = 2;

    [Theory]
    [InlineData(134, 227)]
    [InlineData(190, 155)]
    [InlineData(231, 67)]
    [InlineData(304, 133)]
    public void Installed_high_band_enemy_turn_executes_its_original_dispatch(int effectId, int formation)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleEffectBinding effect = Assert.Single(game.ReadNativeEffectCatalog(), binding => binding.Effect == effectId);
        EnemyScenarioScriptedAction[] scripts = ScenarioEnemyDiscovery.Discover(assets).ScriptedActions.Where(script => script.NativeEffectId == effectId).ToArray();
        EnemyRouteResult result = RunDiscoveredEnemyCast(directory, PrepareField(directory), assets, effect, scripts, probeEnemies: true);
        Assert.True(result.Status is "native" or "logical", result.Detail);
        Assert.Contains($"Formation {formation},", result.Detail);
        Assert.Contains($"band {HigherEnemyBand},", result.Detail);
    }

    private static int? HigherBandFormation(int effect) => effect switch
    {
        134 => 227,
        190 => 155,
        231 => 67,
        304 => 133,
        _ => null
    };

    private static NativeCharacterVitals PrepareHigherBandParty(string directory, NativeCharacterVitals vitals)
    {
        using GameAssets game = new(directory);
        byte[] rows = Enumerable.Range(0, vitals.Records.Length).Select(row => checked((byte)row)).ToArray();
        Dictionary<byte, ushort> awards = new();
        foreach (byte row in rows)
        {
            NativeCharacterVital record = vitals.Records[row];
            NativeStatCurve curve = vitals.Definitions.Characters[record.CharacterId];
            int low = 0;
            int high = ushort.MaxValue;
            Assert.True(NativeStatDefinitions.Level(record.Experience + (uint)high, curve) >= HigherBandPartyLevel);
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (NativeStatDefinitions.Level(record.Experience + (uint)middle, curve) >= HigherBandPartyLevel)
                {
                    high = middle;
                }
                else
                {
                    low = middle + 1;
                }
            }
            awards.Add(row, checked((ushort)low));
        }
        return vitals.AwardBattleExperience(awards, new Dictionary<byte, ushort>(), game.ReadNativeBattleExperienceDefinition(), rows);
    }
}
