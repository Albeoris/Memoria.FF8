using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattlePartyOccupancyTests
{
    [Fact]
    public void Reserve_selection_uses_experience_then_row_and_excludes_pending_and_knocked_out_rows()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create() with { AdditionalCombatParty = [3] };
        vitals.Records[4] = vitals.Records[4] with { Experience = 20 };
        vitals.Records[5] = vitals.Records[5] with { Experience = 10 };
        vitals.Records[6] = vitals.Records[6] with { Experience = 10 };
        vitals.Records[7] = vitals.Records[7] with { Status = 1, HitPoints = new(0, 0) };
        BattleSession battle = new(Definition(vitals), BattleAtbMode.TurnBased);
        MethodInfo select = typeof(BattleSession).GetMethod("SelectPartyReplacement", BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo pending = typeof(BattleSession).GetField("pendingPartyReplacements", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal((byte)5, select.Invoke(battle, [vitals, (byte)0]));

        ImmutableArray<PartyReplacementDescriptor> reserved = [new(new(BattleRosterSlot.Ally(1), 1, 5), "party-1", "party-5", PartyReplacementStage.Bind)];
        pending.SetValue(battle, reserved);
        Assert.Equal((byte)6, select.Invoke(battle, [vitals, (byte)0]));

        vitals.Records[4] = vitals.Records[4] with { MemberFlags = 0 };
        vitals.Records[6] = vitals.Records[6] with { MemberFlags = 0 };
        Assert.Null(select.Invoke(battle, [vitals, (byte)0]));
    }

    [Fact]
    public void Replaced_rows_keep_their_copyback_and_only_active_occupants_get_shared_experience()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create() with { AdditionalCombatParty = [3] };
        BattleDefinition definition = Definition(vitals);
        string hash = new('0', 64);
        BattleEnemyDefeat defeat = new(0, 5, BattleDefeatCredit.Unattributed, null, null, false, -1);
        BattleResult result = new(new("party-replacement", 511, hash, SceneKind.Field, 74, 0, hash, null),
            BattleOutcome.Victory, [new(0, 0, 10, 5, 0, 0, []), new(1, 1, 10, 8, 0, 0, []), new(2, 2, 10, 10, 0, 0, []), new(3, 3, 10, 10, 0, 0, []), new(4, 4, 10, 7, 0, 0, [])],
            [], [0], BattleResultProjection.EligibleGuardianForces(vitals, [4, 2, 3]), false, BattleRewardPolicy.NativeCombat, [defeat])
        {
            EnemySlotCount = 1,
            NativeRewardItems = [],
            PartyOccupancyChanges = [new(BattleRosterSlot.Ally(0), 0, 4), new(BattleRosterSlot.Ally(1), 1, null)]
        };
        NativeEconomy economy = new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = hash, Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 };
        BattleEnemyRewardDefinition reward = new(0, 10, 20, 30, 255, false, 0, Enumerable.Repeat(new BattleStock(0, 0), 12).ToImmutableArray());
        NativeBattleExperienceDefinition experience = new(vitals.KernelSha256, ImmutableArray.CreateRange(new uint[20]));
        string before = JsonSerializer.Serialize(vitals);

        BattleResultOwners projected = Apply(result);
        Assert.Equal(new byte[] { 4, 1, 2 }, projected.CharacterVitals.CombatParty);
        Assert.Equal(new byte[] { 3 }, projected.CharacterVitals.AdditionalCombatParty);
        Assert.Equal(new ushort[] { 5, 8, 10, 10, 7 }, projected.CharacterVitals.Records.Take(5).Select(row => row.HitPoints.Minimum));
        Assert.Equal(new byte[] { 2, 3, 4 }, projected.Rewards.CharacterExperience.Keys.Order());
        Assert.Equal(before, JsonSerializer.Serialize(vitals));
        BattleResult roundTrip = JsonSerializer.Deserialize<BattleResult>(JsonSerializer.Serialize(result))!;
        Assert.Equal(JsonSerializer.Serialize(projected), JsonSerializer.Serialize(Apply(roundTrip)));

        Assert.Throws<InvalidDataException>(() => Apply(result with { PartyOccupancyChanges = [.. result.PartyOccupancyChanges, result.PartyOccupancyChanges[0]] }));
        Assert.Throws<InvalidDataException>(() => Apply(result with { Party = result.Party.Where(member => member.Row != 0).ToArray() }));

        BattleResultOwners Apply(BattleResult current)
        {
            return BattleResultProjection.Apply(current, vitals, economy, new Dictionary<int, int> { [0] = 0 }, new Dictionary<int, BattleEnemyRewardDefinition> { [0] = reward }, experience, null, false, admittedBattle: definition);
        }
    }

    private static BattleDefinition Definition(NativeCharacterVitals vitals)
    {
        return new()
        {
            EncounterId = 511,
            MagicVitals = vitals,
            Combatants = [.. Enumerable.Range(0, 4).Select(PartyActor), new() { Id = "enemy", Name = "Enemy" }],
            PartyReplacementCandidates = Enumerable.Range(4, 4).ToImmutableDictionary(row => checked((byte)row), PartyActor)
        };
    }

    private static BattleCombatantDefinition PartyActor(int row)
    {
        return new() { Id = $"party-{row}", Name = $"Party {row}", IsPlayer = true, ModelId = row, MagicRecordId = row, CurrentHp = 10, MaximumHp = 10 };
    }
}
