using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedBattleResultTests
{
    [Fact]
    public void Two_selected_members_at_capacity_four_settle_in_frozen_order()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create() with { CombatParty = [0, 255, 255], AdditionalCombatParty = [3] };
        byte[] rows = vitals.GetBattlePartyRows(4);
        Assert.Equal(new byte[] { 0, 3 }, rows);
        BattleDefinition admitted = Definition(vitals, rows);
        BattleResult result = Result(vitals, rows);

        Assert.Equal(new byte[] { 0, 255, 255 }, Project(result, vitals, admitted).CombatParty);
        Assert.Equal(new byte[] { 3 }, Project(result, vitals).AdditionalCombatParty);
        Assert.Throws<InvalidDataException>(() => Project(Result(vitals, [0, 4]), vitals, admitted));
        Assert.Throws<InvalidDataException>(() => Project(Result(vitals, [0, 4]), vitals));
        Assert.Throws<InvalidDataException>(() => Project(Result(vitals, [0, 4]), vitals, Definition(vitals, [0, 4])));
        Assert.Throws<InvalidDataException>(() => Project(Result(vitals, [0]), vitals, admitted));
    }

    [Fact]
    public void Historical_bound_extra_can_finish_without_unlocking_that_character()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals.Records[3] = vitals.Records[3] with { MemberFlags = 0 };
        byte[] rows = [0, 1, 2, 3];
        BattleDefinition historical = Definition(vitals, rows);
        NativeCharacterVitals frozen = ExpandedBattlePartyRows.RecoverSelection(vitals, ExpandedBattlePartyRows.ReadBoundRows(historical, vitals));
        BattleDefinition recovered = historical with { MagicVitals = frozen };
        BattleResult result = Result(vitals, rows);

        Assert.Throws<InvalidDataException>(() => Project(result, vitals));
        Assert.Throws<InvalidDataException>(() => Project(Result(vitals, [0, 1, 2, 4]), vitals, recovered));
        NativeCharacterVitals projected = Project(result, vitals, recovered);
        Assert.Equal(new byte[] { 3 }, projected.AdditionalCombatParty);
        Assert.False(projected.IsRosterMember(3));
        Assert.Equal(new byte[] { 0, 1, 2 }, projected.GetBattlePartyRows(11));
        Assert.Equal(vitals.Records[3].MemberFlags, projected.Records[3].MemberFlags);
        Assert.Null(vitals.AdditionalCombatParty);
    }

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
        Assert.Throws<InvalidDataException>(() => (restored with { ExpandedEnemyCount = expectedEnemyCount - 1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (restored with { ExpandedEnemyCount = ExpandedBattleFormation.MaximumSideSize + 1 }).Validate());
        if (expectedEnemyCount < ExpandedBattleFormation.MaximumSideSize)
        {
            BattleResult largerRoster = restored with { ExpandedEnemyCount = expectedEnemyCount + 1 };
            largerRoster.Validate();
            Assert.Equal(expectedEnemyCount + 1, largerRoster.EffectiveEnemySlotCount);
            Assert.Equal(defeats, largerRoster.EnemyDefeats);
        }
        if (recordedEnemyCount == 11)
        {
            Assert.Throws<InvalidDataException>(() => (restored with { ExpandedEnemyCount = null }).Validate());
        }
        else
        {
            Assert.DoesNotContain(nameof(BattleResult.ExpandedEnemyCount), json);
        }
    }

    private static BattleDefinition Definition(NativeCharacterVitals vitals, byte[] rows)
    {
        return new BattleDefinition
        {
            Name = "Expanded battle",
            EncounterId = 0,
            UsesExpandedRoster = true,
            MagicVitals = vitals.DeepCopy(),
            Combatants = rows.Select(row => new BattleCombatantDefinition { Id = $"party-{row}", Name = $"Character {row}", IsPlayer = true, MagicRecordId = row, ModelId = vitals.Records[row].CharacterId }).ToArray()
        };
    }

    private static BattleResult Result(NativeCharacterVitals vitals, byte[] rows)
    {
        string hash = new('0', 64);
        BattleEncounterOccurrence encounter = new("random-expanded", 0, hash, SceneKind.Field, 74, 1, hash, null);
        BattlePartyResult[] party = rows.Select(row => new BattlePartyResult(row, vitals.Records[row].CharacterId, vitals.Records[row].HitPoints.Minimum, vitals.Records[row].HitPoints.Minimum, 0, 0, [])).ToArray();
        return new BattleResult(encounter, BattleOutcome.Victory, party, [], [], [], true, BattleRewardPolicy.DebugNoRewards, [])
        {
            UsesExpandedRoster = true,
            NativeRewardItems = []
        };
    }

    private static NativeCharacterVitals Project(BattleResult result, NativeCharacterVitals vitals, BattleDefinition? admitted = null)
    {
        NativeEconomy economy = new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = new string('0', 64), Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 };
        NativeBattleExperienceDefinition experience = new(vitals.KernelSha256, ImmutableArray.CreateRange(new uint[20]));
        return BattleResultProjection.Apply(result, vitals, economy, new Dictionary<int, int>(), new Dictionary<int, BattleEnemyRewardDefinition>(), experience, null, false, admittedBattle: admitted).CharacterVitals;
    }
}
