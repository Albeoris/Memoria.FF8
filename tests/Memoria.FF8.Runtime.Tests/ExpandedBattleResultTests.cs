using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedBattleResultTests
{
    [Theory]
    [InlineData(4, null, 6)]
    [InlineData(8, null, 8)]
    [InlineData(11, null, 8)]
    [InlineData(11, 11, 11)]
    public void EnemySlotsSurviveSerializationAndUseTheRecordedRosterSize(int allyCount, int? recordedEnemyCount, int expectedEnemyCount)
    {
        string hash = new('0', 64);
        BattleEncounterOccurrence encounter = new("random-expanded", 0, hash, SceneKind.Field, 74, 1, hash, null);
        BattlePartyResult[] party = Enumerable.Range(0, allyCount).Select(row => new BattlePartyResult((byte)row, (byte)row, 100, 100, 0, 0, [])).ToArray();
        BattleEnemyDefeat[] defeats = Enumerable.Range(0, expectedEnemyCount).Select(slot => new BattleEnemyDefeat(slot, 1, BattleDefeatCredit.Unattributed, null, null, false, -1)).ToArray();
        BattleResult result = new(encounter, BattleOutcome.Victory, party, [], defeats.Select(defeat => defeat.Slot).ToArray(), [], false, BattleRewardPolicy.NativeCombat, defeats)
        {
            UsesExpandedRoster = true,
            ExpandedEnemyCount = recordedEnemyCount,
            NativeRewardItems = []
        };

        string json = JsonSerializer.Serialize(result);
        BattleResult restored = JsonSerializer.Deserialize<BattleResult>(json)!;
        restored.Validate();
        Assert.Equal(expectedEnemyCount, restored.EffectiveEnemySlotCount);
        Assert.Equal(expectedEnemyCount - 1, restored.EnemyDefeats[^1].Slot);
        Assert.Throws<InvalidDataException>(() => (restored with { ExpandedEnemyCount = expectedEnemyCount + 1 }).Validate());
        if (recordedEnemyCount == 11)
        {
            Assert.Throws<InvalidDataException>(() => (restored with { ExpandedEnemyCount = null }).Validate());
        }
        else
        {
            Assert.DoesNotContain(nameof(BattleResult.ExpandedEnemyCount), json);
        }
    }
}
