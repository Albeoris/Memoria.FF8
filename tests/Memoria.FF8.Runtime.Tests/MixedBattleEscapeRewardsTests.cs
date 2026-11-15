using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class MixedBattleEscapeRewardsTests
{
    private static readonly string SourceHash = new('A', 64);
    private static readonly BattleEnemyRewardDefinition Rewards = new(0, 10, 20, 30, 255, false, 0, Enumerable.Repeat(new BattleStock(0, 0), 12).ToImmutableArray());
    private static readonly ImmutableSortedDictionary<int, BattleEnemyRosterSource> Added = ImmutableSortedDictionary<int, BattleEnemyRosterSource>.Empty.Add(8, new(2, 0, SourceHash));

    [Fact]
    public void EscapeCaptureAndRepeatedProjectionCreditEachLiveAllocationOnce()
    {
        BattleCombatantDefinition original = Enemy(0, initialActive: true);
        NativeBattleAiDefinition ai = new([[31, 1, 31, 1, 0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession battle = new(new()
        {
            EncounterId = 1,
            UsesExpandedRoster = true,
            UsesOriginalEnemySlots = true,
            AddedEnemySources = Added,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            InitialRandom = NativeBattleRandomState.ForecastPhase(1),
            Combatants =
            [
                .. Allies(4),
                original with { NativeAi = ai },
                Enemy(1, initialActive: false),
                Enemy(8, initialActive: true) with { NativeTemplate = null, Information254 = 8, CurrentHp = 90 }
            ]
        }, BattleAtbMode.TurnBased);

        DetachedExpandedAiFixture.DispatchLeadingInitializers(battle);
        BattleCombatant first = battle.Combatants.Single(actor => actor.Id == "native-allocation-1");
        BattleCombatant second = battle.Combatants.Single(actor => actor.Id == "native-allocation-2");
        first.CurrentHp = 75;
        second.CurrentHp = 50;
        Escape(battle);

        BattleEnemyRemainingExperience[] receipts = battle.CaptureEscapeRemainingExperience();
        Assert.Equal(new BattleEnemyRemainingExperience[] { new(0, 5, 100, 100, 3), new(1, 5, 100, 75, 4), new(1, 5, 100, 50, 5), new(8, 5, 100, 90, null) }, receipts);
        BattleSession restoredBattle = BattleSession.Restore(battle.Capture());
        Assert.Equal(receipts, restoredBattle.CaptureEscapeRemainingExperience());

        NativeCharacterVitals vitals = MagicSystemFixture.Create() with { AdditionalCombatParty = [3] };
        BattleResult result = EscapedResult(vitals, receipts, expanded: true, originalSlots: true, added: Added);
        BattleResult restored = JsonSerializer.Deserialize<BattleResult>(JsonSerializer.Serialize(result))!;
        Assert.Equal(receipts, restored.RemainingEnemyExperience);
        NativeEconomy economy = new()
        {
            Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = SourceHash,
            Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0
        };
        Dictionary<int, BattleEnemyRewardDefinition> rewards = new() { [0] = Rewards, [1] = Rewards, [8] = Rewards };
        NativeBattleExperienceDefinition experience = new(vitals.KernelSha256, ImmutableArray.CreateRange(new uint[20]));
        string input = JsonSerializer.Serialize(new { vitals, economy, result });
        BattleResultOwners preview = BattleResultProjection.Apply(result, vitals, economy, new Dictionary<int, int>(), rewards, experience, null, false);
        BattleResultOwners repeated = BattleResultProjection.Apply(restored, vitals, economy, new Dictionary<int, int>(), rewards, experience, null, false);
        Assert.Equal(4, preview.Rewards.CharacterExperience.Count);
        // Level-5 enemies against a level-1 party produce 240 shared EXP at full damage.
        Assert.All(preview.Rewards.CharacterExperience.Values, award => Assert.Equal((ushort)204, award)); // 60 + 120 + 24
        Assert.Equal(JsonSerializer.Serialize(preview), JsonSerializer.Serialize(repeated));
        Assert.Equal(input, JsonSerializer.Serialize(new { vitals, economy, result }));
    }

    [Fact]
    public void MixedReceiptValidationRejectsAmbiguousAndUnownedAllocations()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create() with { AdditionalCombatParty = [3] };
        BattleEnemyRemainingExperience first = new(1, 5, 100, 75, 4);
        BattleEnemyRemainingExperience second = new(1, 5, 100, 50, 5);
        BattleEnemyRemainingExperience added = new(8, 5, 100, 90, null);
        BattleResult result = EscapedResult(vitals, [first, second, added], expanded: true, originalSlots: true, added: Added);
        result.Validate();

        Reject(first, second with { Slot = 0, NativeIndex = 4 }, added);
        Reject(first with { NativeIndex = null }, second, added);
        Reject(first, second, added with { NativeIndex = 6 });
        Reject(first, second, added with { Slot = 9 });
        Reject(first, second with { NativeIndex = 7 }, added);
        Reject(first, second, added, added);

        // A physical index and an unrelated legacy source slot occupy distinct namespaces.
        (result with { RemainingEnemyExperience = [first, new(4, 5, 100, 60, null), added] }).Validate();

        void Reject(params BattleEnemyRemainingExperience[] receipts)
        {
            Assert.Throws<InvalidDataException>(() => (result with { RemainingEnemyExperience = receipts }).Validate());
        }
    }

    [Fact]
    public void NativeCaptureAndOldExpandedReceiptShapesRemainValid()
    {
        BattleSession native = new(new()
        {
            EncounterId = 1,
            InitialRandom = NativeBattleRandomState.ForecastPhase(1),
            Combatants = [.. Allies(3), .. Enumerable.Range(0, 4).Reverse().Select(slot => Enemy(slot, initialActive: true))]
        }, BattleAtbMode.TurnBased);
        Escape(native);
        BattleEnemyRemainingExperience[] nativeReceipts = native.CaptureEscapeRemainingExperience();
        Assert.Equal(Enumerable.Range(3, 4).Select(index => (int?)index), nativeReceipts.Select(receipt => receipt.NativeIndex));
        Assert.Equal(new[] { 3, 2, 1, 0 }, nativeReceipts.Select(receipt => receipt.Slot));
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        BattleResult nativeResult = EscapedResult(vitals, nativeReceipts, expanded: false, originalSlots: false);
        nativeResult.Validate();
        Assert.Throws<InvalidDataException>(() => (nativeResult with { RemainingEnemyExperience = [nativeReceipts[0] with { NativeIndex = null }] }).Validate());

        BattleEnemyRemainingExperience[] legacyReceipts = [new(0, 5, 100, 70, null), new(1, 5, 100, 85, null)];
        BattleResult legacyOriginal = EscapedResult(vitals, legacyReceipts, expanded: true, originalSlots: true);
        BattleResult legacyGeneral = EscapedResult(vitals, legacyReceipts, expanded: true, originalSlots: false);
        JsonSerializer.Deserialize<BattleResult>(JsonSerializer.Serialize(legacyOriginal))!.Validate();
        JsonSerializer.Deserialize<BattleResult>(JsonSerializer.Serialize(legacyGeneral))!.Validate();
        Assert.Throws<InvalidDataException>(() => (legacyGeneral with { RemainingEnemyExperience = [legacyReceipts[0] with { NativeIndex = 3 }] }).Validate());
    }

    private static void Escape(BattleSession battle)
    {
        battle.PublishNativeEscapeButtons(3);
        for (int frame = 0; frame < 600 && battle.Outcome == BattleOutcome.Active; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(BattleOutcome.Escaped, battle.Outcome);
    }

    private static BattleResult EscapedResult(NativeCharacterVitals vitals, BattleEnemyRemainingExperience[] receipts, bool expanded, bool originalSlots, ImmutableSortedDictionary<int, BattleEnemyRosterSource>? added = null)
    {
        BattleEncounterOccurrence encounter = new("escape", 1, SourceHash, SceneKind.Field, 74, 0, SourceHash, null);
        BattlePartyResult[] party = vitals.GetBattlePartyRows(expanded ? 11 : 3).Select(row =>
        {
            NativeCharacterVital actor = vitals.Records[row];
            return new BattlePartyResult(row, actor.CharacterId, actor.HitPoints.Minimum, actor.HitPoints.Minimum, actor.Status, actor.Status, []);
        }).ToArray();
        int[] eligibleGuardianForces = BattleResultProjection.EligibleGuardianForces(vitals, party.Select(member => member.Row).ToArray());
        return new(encounter, BattleOutcome.Escaped, party, [], [], eligibleGuardianForces, false, BattleRewardPolicy.NativeCombat, [])
        {
            UsesExpandedRoster = expanded, UsesOriginalEnemySlots = originalSlots, ExpandedEnemyCount = expanded ? 2 + (added?.Count ?? 0) : null,
            AddedEnemySources = added, RemainingEnemyExperience = receipts, NativeResult = 2
        };
    }

    private static IEnumerable<BattleCombatantDefinition> Allies(int count)
    {
        return Enumerable.Range(0, count).Select(index => new BattleCombatantDefinition
        {
            Id = $"ally-{index}", Name = $"Ally {index}", IsPlayer = true, ModelId = index, Speed = 1
        });
    }

    private static BattleCombatantDefinition Enemy(int slot, bool initialActive)
    {
        NativeBattleEnemyLevel level = new(100, 10, 10, 10, 10, 1, new int[8], [], [], 0);
        NativeBattleEnemyTemplate template = new(5, Enumerable.Repeat(level, 101).ToArray()) { Information254 = 8 };
        return new()
        {
            Id = $"enemy-{slot}", Name = $"Enemy {slot}", SceneSlot = slot, RewardSlot = slot, ModelId = 28 + slot, InitialActive = initialActive,
            MaximumHp = level.MaximumHp, CurrentHp = level.MaximumHp, Level = 5, NativeTemplate = template, Rewards = Rewards,
            Attack = level.Strength, Defense = level.Vitality, Magic = level.Magic, Spirit = level.Spirit, Speed = level.Speed, Evasion = level.Evasion, ElementDefenses = level.ElementDefenses
        };
    }
}
