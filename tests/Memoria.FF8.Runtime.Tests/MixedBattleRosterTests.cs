using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class MixedBattleRosterTests
{
    private static readonly string SourceHash = new('A', 64);
    private static readonly BattleEnemyRewardDefinition Rewards = new(10, 0, 20, 30, 255, false, 0, Enumerable.Repeat(new BattleStock(0, 0), 12).ToImmutableArray());

    [Fact]
    public void OriginalBankAllocatesIntoAHoleAndReplacesWithoutChangingAdditions()
    {
        BattleDefinition definition = MixedDefinition(4, 2, 1, 3);
        BattleCombatantDefinition[] combatants = definition.Combatants.Select(actor => actor.Id switch
        {
            "original-0" => actor with { NativeAi = Program([52, 1, 59, 1, 4, 0]) },
            "original-1" => actor with { NativeAi = Program([0]) },
            _ => actor
        }).ToArray();
        BattleSession battle = Start(definition with { Combatants = combatants });
        Assert.Equal(new BattleExpandedAiCallbackRequest(new BattleEnemyActorReference("original-0"), 0), Assert.Single(battle.QueuedActions).ExpandedAiCallback);
        battle.Tick();
        Assert.Equal(3, Participant(battle, "original-0").ActorIndex);
        Assert.Equal(-1, Participant(battle, "ally-3").ActorIndex);
        Assert.Null(battle.Combatants.Single(actor => actor.Id == "ally-3").NativeActorIndex);
        BattleCombatant first = battle.Combatants.Single(actor => actor.Id == "native-allocation-1");
        BattleCombatant replacement = battle.Combatants.Single(actor => actor.Id == "native-allocation-2");
        Assert.False(first.IsActive);
        Assert.Null(first.Slot);
        Assert.Equal(4, replacement.NativeActorIndex);
        Assert.Equal(BattleRosterSlot.Enemy(1), replacement.Slot);
        Assert.Equal(4, Participant(battle, replacement.Id).ActorIndex);
        Assert.Equal(new[] { "native-allocation-1", "native-allocation-2" }, battle.Events.Where(entry => entry.Kind == "NativeSpawn").Select(entry => entry.TargetId));
        Assert.Equal(new BattleExpandedAiCallbackRequest(new BattleEnemyActorReference(replacement.Id), 0), Assert.Single(battle.QueuedActions).ExpandedAiCallback);

        BattleCombatant[] extras = battle.Combatants.Where(actor => actor.SceneSlot >= 8).ToArray();
        Assert.Equal(new[] { "additional-0", "additional-1", "additional-2" }, extras.Select(actor => actor.Id));
        for (int index = 0; index < extras.Length; index++)
        {
            BattleCombatant actor = extras[index];
            Assert.Equal(BattleRosterSlot.Enemy(4 + index), actor.Slot);
            Assert.True(actor.IsActive);
            Assert.Equal(80 + index, actor.CurrentHp);
            Assert.Null(actor.NativeActorIndex);
            Assert.Equal(-1, Participant(battle, actor.Id).ActorIndex);
        }
        Assert.Equal(definition.AddedEnemySources!.ToArray(), battle.ReadDefinition().AddedEnemySources!.ToArray());
        AssertNextCallbackAfterRestore(battle);
    }

    [Fact]
    public void LiteralPhysicalSelectorCannotResolveAnAddedEnemyIntoAnEmptyOriginalRow()
    {
        BattleDefinition definition = MixedDefinition(4, 1, 1, 1);
        BattleCombatantDefinition[] combatants = definition.Combatants.Select(actor => actor.Id == "original-0"
            ? actor with { NativeAi = Program([2, 15, 0, 0, 4, 0, 3, 0, 14, 220, 1, 0]) }
            : actor).ToArray();
        BattleSession battle = Start(definition with { Combatants = combatants });
        DetachedExpandedAiFixture.DispatchCallback(battle, "original-0", 0);
        Assert.Equal(0u, Participant(battle, "original-0").Variables[0]);
        Assert.Null(battle.Combatants.Single(actor => actor.Id == "additional-0").NativeActorIndex);
    }

    [Fact]
    public void PhysicalVariableAliasCannotReadOrWriteAnAddedEnemy()
    {
        BattleDefinition definition = MixedDefinition(4, 1, 1, 1);
        BattleCombatantDefinition[] combatants = definition.Combatants.Select(actor => actor.Id == "additional-0"
            ? actor with { NativeAi = Program([14, 96, 1, 0]) }
            : actor).ToArray();
        BattleSession battle = Start(definition with { Combatants = combatants });
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => DetachedExpandedAiFixture.DispatchCallback(battle, "additional-0", 0));
        Assert.Contains("physical actor-bank alias", exception.Message);
    }

    [Fact]
    public void EightPrototypesAndTenAdditionsSupportFourteenActiveEnemies()
    {
        BattleSession battle = Start(MixedDefinition(11, 8, 4, 10));
        Assert.Equal(18, battle.Combatants.Count(actor => !actor.IsPlayer));
        Assert.Equal(14, battle.Combatants.Count(actor => !actor.IsPlayer && actor.IsActive));
        Assert.Equal(Enumerable.Range(0, 14), battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive).Select(actor => actor.Slot!.Value.Position));
        Assert.Equal(Enumerable.Range(3, 4).Select(index => (int?)index), battle.Combatants.Where(actor => !actor.IsPlayer && actor.NativeActorIndex.HasValue).Select(actor => actor.NativeActorIndex));
        Assert.True(ExpandedBattleFormation.SupportsRoster(11, 14));
        Assert.False(ExpandedBattleFormation.SupportsRoster(12, 14));
        Assert.False(ExpandedBattleFormation.SupportsRoster(11, 15));
        Assert.Equal(new NativeBattleVector(0, 0, -3600), ExpandedBattleFormation.Position(10, 11, false));
        Assert.Equal(new NativeBattleVector(-600, 0, -3600), ExpandedBattleFormation.Position(10, 12, false));
        Assert.Equal(new NativeBattleVector(1800, 0, -3600), ExpandedBattleFormation.Position(13, 14, false));
        AssertNextCallbackAfterRestore(battle);
    }

    [Fact]
    public void MixedDefinitionsRejectInvalidProvenanceAndNativeOwnership()
    {
        BattleDefinition definition = MixedDefinition(4, 5, 1, 2);
        BattleCombatantDefinition[] actors = definition.Combatants;
        BattleCombatantDefinition added = actors[^1];
        var sources = definition.AddedEnemySources!;
        Assert.Throws<InvalidDataException>(() => Start(definition with { AddedEnemySources = sources.Remove(8).Add(10, sources[8]) }));
        Assert.Throws<InvalidDataException>(() => Start(definition with { AddedEnemySources = sources.SetItem(9, sources[9] with { SceneSlot = 8 }) }));
        Assert.Throws<InvalidDataException>(() => Start(definition with { Combatants = [.. actors[..^1], added with { SceneSlot = 8 }] }));
        Assert.Throws<InvalidDataException>(() => Start(definition with { Combatants = [.. actors[..^1], added with { InitialActive = false }] }));
        Assert.Throws<InvalidDataException>(() => Start(definition with { Combatants = [.. actors[..^1], added with { NativeTemplate = actors[4].NativeTemplate }] }));
        Assert.Throws<InvalidDataException>(() => Start(definition with { Combatants = [.. actors[..^1], added with { RewardSlot = 8 }] }));
        Assert.Throws<InvalidDataException>(() => Start(MixedDefinition(4, 5, 5, 1)));
        Assert.Throws<InvalidDataException>(() => Start(MixedDefinition(4, 1, 1, 11)));

        BattleSnapshot snapshot = Start(definition).Capture();
        BattleCombatantSnapshot[] forged = snapshot.Combatants.Select(actor => actor.Id == added.Id
            ? actor with { NativeParticipant = actor.NativeParticipant! with { ActorIndex = 4 } }
            : actor).ToArray();
        Assert.Throws<InvalidDataException>(() => DetachedExpandedAiFixture.Restore(snapshot with { Combatants = forged }));
    }

    [Fact]
    public void NativeAndLegacyGeneralRostersKeepTheirOriginalSlotRules()
    {
        BattleDefinition definition = MixedDefinition(3, 4, 4, 0);
        BattleSession native = Start(definition with { UsesOriginalEnemySlots = false, AddedEnemySources = null });
        Assert.Equal(Enumerable.Range(0, 7), native.Capture().Combatants.Select(actor => actor.NativeParticipant!.ActorIndex));
        AssertNextCallbackAfterRestore(native);

        BattleSession original = Start(definition with { AddedEnemySources = null });
        Assert.Equal(Enumerable.Range(3, 4), original.Capture().Combatants.Where(actor => actor.Id.StartsWith("original-", StringComparison.Ordinal)).Select(actor => actor.NativeParticipant!.ActorIndex));
        AssertNextCallbackAfterRestore(original);

        BattleDefinition legacy = MixedDefinition(4, 1, 1, 2);
        BattleCombatantDefinition[] roster = legacy.Combatants.Select(actor => actor.SceneSlot is >= 8
            ? actor with { SceneSlot = actor.SceneSlot - 7, RewardSlot = actor.RewardSlot - 7 }
            : actor).ToArray();
        BattleSession general = Start(legacy with { UsesOriginalEnemySlots = false, Combatants = roster, AddedEnemySources = Sources(1, 2) });
        Assert.Equal(BattleRosterSlot.Enemy(1), general.Combatants.Single(actor => actor.Id == "additional-0").Slot);
        Assert.Null(general.Combatants.Single(actor => actor.Id == "additional-0").NativeActorIndex);
        AssertNextCallbackAfterRestore(general);
    }

    [Fact]
    public void ScriptedExpandedResultValidatesAddedRewardAndEscapeSlots()
    {
        ScriptedBattleOwner caller = new(74, 0, 2, 13, 0, SourceHash);
        BattleEncounterOccurrence occurrence = new("scripted-mixed", 1, SourceHash, SceneKind.Field, 74, 0, SourceHash, caller);
        BattlePartyResult[] party = Enumerable.Range(0, 4).Select(row => new BattlePartyResult((byte)row, (byte)row, 100, 100, 0, 0, [])).ToArray();
        BattleEnemyDefeat[] defeats = Enumerable.Range(8, 10).Select(slot => new BattleEnemyDefeat(slot, 5, BattleDefeatCredit.Unattributed, null, null, false, -1)).ToArray();
        BattleResult victory = new(occurrence, BattleOutcome.Victory, party, [], defeats.Select(defeat => defeat.Slot).ToArray(), [], false, BattleRewardPolicy.NativeCombat, defeats)
        {
            UsesOriginalEnemySlots = true, EnemySlotCount = 18, AddedEnemySources = Sources(8, 10)
        };
        BattleResult restored = JsonSerializer.Deserialize<BattleResult>(JsonSerializer.Serialize(victory))!;
        restored.Validate();
        Assert.Equal(18, restored.EnemySlotCount);
        Assert.Equal(caller, restored.Encounter.ScriptedOwner);
        Assert.Throws<InvalidDataException>(() => (restored with { EnemySlotCount = 19 }).Validate());
        Assert.Throws<InvalidDataException>(() => (restored with { EnemySlotCount = 10 }).Validate());
        Assert.Throws<InvalidDataException>(() => (restored with { AddedEnemySources = Sources(9, 10) }).Validate());
        Assert.Throws<InvalidDataException>(() => (restored with { DefeatedEnemySlots = [.. restored.DefeatedEnemySlots, 18] }).Validate());

        BattleResult escaped = restored with
        {
            Outcome = BattleOutcome.Escaped,
            DefeatedEnemySlots = [],
            EnemyDefeats = [],
            RemainingEnemyExperience = [new(17, 5, 100, 70, null)]
        };
        escaped.Validate();
        Assert.Throws<InvalidDataException>(() => (escaped with { RemainingEnemyExperience = [new(18, 5, 100, 70, null)] }).Validate());
        Assert.Throws<InvalidDataException>(() => (escaped with { RemainingEnemyExperience = [new(17, 5, 100, 70, 4)] }).Validate());
    }

    private static BattleDefinition MixedDefinition(int allyCount, int originalCount, int activeOriginalCount, int addedCount)
    {
        NativeBattleEnemyLevel level = new(100, 10, 10, 10, 10, 10, new int[8], [], [], 0);
        NativeBattleEnemyTemplate template = new(5, Enumerable.Repeat(level, 101).ToArray())
        {
            NativeStatusAllocation = new(0, 0, Enumerable.Repeat((byte)100, 40).ToImmutableArray())
        };
        return new()
        {
            UsesOriginalEnemySlots = true, AddedEnemySources = Sources(8, addedCount),
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                .. Enumerable.Range(0, allyCount).Select(index => new BattleCombatantDefinition { Id = $"ally-{index}", Name = $"Ally {index}", IsPlayer = true, ModelId = index }),
                .. Enumerable.Range(0, originalCount).Select(index => new BattleCombatantDefinition
                {
                    Id = $"original-{index}", Name = $"Original {index}", SceneSlot = index, ModelId = 28 + index, InitialActive = index < activeOriginalCount,
                    MaximumHp = level.MaximumHp, CurrentHp = level.MaximumHp, Level = 5, NativeTemplate = template, RewardSlot = index, Rewards = Rewards,
                    Attack = level.Strength, Defense = level.Vitality, Magic = level.Magic, Spirit = level.Spirit, Speed = level.Speed, Evasion = level.Evasion, ElementDefenses = level.ElementDefenses
                }),
                .. Enumerable.Range(0, addedCount).Select(index => new BattleCombatantDefinition
                {
                    Id = $"additional-{index}", Name = $"Additional {index}", SceneSlot = 8 + index, ModelId = 40 + index,
                    MaximumHp = 100, CurrentHp = 80 + index, Level = 5, RewardSlot = 8 + index, Rewards = Rewards
                })
            ]
        };
    }

    private static ImmutableSortedDictionary<int, BattleEnemyRosterSource> Sources(int firstSlot, int count)
    {
        return Enumerable.Range(firstSlot, count).ToImmutableSortedDictionary(slot => slot, slot => new BattleEnemyRosterSource(2, 0, SourceHash));
    }

    private static NativeBattleAiDefinition Program(byte[] init)
    {
        return new([init, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
    }

    private static BattleSession Start(BattleDefinition definition) => new(definition, BattleAtbMode.Normal);

    private static NativeBattleParticipantSnapshot Participant(BattleSession battle, string id) => battle.Capture().Combatants.Single(actor => actor.Id == id).NativeParticipant!;

    private static void AssertNextCallbackAfterRestore(BattleSession battle)
    {
        BattleSession restored = DetachedExpandedAiFixture.Restore(battle.Capture());
        DetachedExpandedAiFixture.AssertSameContinuation(battle, restored);
        battle.Tick();
        restored.Tick();
        DetachedExpandedAiFixture.AssertSameContinuation(battle, restored);
    }
}
