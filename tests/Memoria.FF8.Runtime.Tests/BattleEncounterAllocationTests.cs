using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleEncounterAllocationTests
{
    [Fact]
    public void CopiesReserveTogetherPreserveExistingAllyAndActivateAfterArrival()
    {
        BattleSession battle = Create(1);
        BattleSnapshot before = battle.Capture();
        BattleEncounterAllocationState batch = battle.BeginEncounterAllocation("fixture.summon", "original", "support", "assault");
        Assert.Equal(BattleEncounterAllocationPhase.Ready, batch.Phase);
        Assert.Equal(4, battle.Combatants.Count(actor => !actor.IsPlayer));
        Assert.True(battle.Combatants.Single(actor => actor.Id == "ally-0").IsActive);
        Assert.All(batch.ActorIds, id =>
        {
            BattleCombatant copy = battle.Combatants.Single(actor => actor.Id == id);
            Assert.False(copy.IsActive);
            Assert.True(copy.Hidden);
            Assert.True(copy.Untargetable);
            Assert.True(copy.Suspended);
            Assert.Equal(250, copy.MaximumHp);
            Assert.Equal(250, copy.CurrentHp);
            Assert.Equal(17, copy.Level);
            Assert.Equal(0, copy.Atb);
            Assert.All(battle.GetCombatantDefinition(id).NativeAi!.Scripts, script => Assert.Equal(new byte[] { 0 }, script));
            Assert.Empty(battle.GetCombatantDefinition(id).NativeAi!.Texts);
            Assert.Null(battle.GetCombatantDefinition(id).Rewards);
            Assert.Null(battle.GetCombatantDefinition(id).NativeMug);
        });
        Assert.Equal(JsonSerializer.Serialize(before.Random), JsonSerializer.Serialize(battle.Capture().Random));
        Assert.Equal(new[] { 5, 6 }, battle.Capture().Combatants.Where(actor => batch.ActorIds.Contains(actor.Id)).Select(actor => actor.NativeParticipant!.ActorIndex));
        battle.BeginEncounterAllocationArrival();
        BattleSnapshot arrival = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(arrival))!);
        Assert.Equal(BattleEncounterAllocationPhase.Arriving, restored.EncounterAllocation!.Phase);
        restored.CompleteEncounterAllocationArrival();
        restored.CompleteEncounterAllocationArrival();
        Assert.All(batch.ActorIds, id => Assert.True(restored.Combatants.Single(actor => actor.Id == id).IsAlive));
        Assert.Single(restored.Events, item => item.Kind == "EncounterSpawn");
        Assert.Equal(2, restored.Capture().NativeAllocations.Length);
        Assert.Equal(batch.ActorIds, restored.BeginEncounterAllocation("fixture.summon", "original", "support", "assault").ActorIds);
    }

    [Fact]
    public void CapacityFailurePublishesNoActorAndDoesNotEvictExistingEnemies()
    {
        BattleSession battle = Create(2);
        string before = JsonSerializer.Serialize(battle.Capture());
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => battle.BeginEncounterAllocation("fixture.summon", "original", "support", "assault"));
        Assert.Contains("free enemy slots", error.Message);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void ResolvedCopyHpDoesNotReprojectFromNativeLevelTableOnRestore()
    {
        BattleSession battle = Create(0, 100);
        BattleEncounterAllocationState batch = battle.BeginEncounterAllocation("fixture.summon", "original", "support", "assault");
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Assert.All(batch.ActorIds, id => Assert.Equal(250, restored.Combatants.Single(actor => actor.Id == id).MaximumHp));
    }

    [Fact]
    public void CopiesRetainImmutableInnateStatusesWithoutLivePoisonOrDouble()
    {
        BattleSnapshot snapshot = Create(0).Capture();
        BattleCombatantDefinition original = snapshot.Definition.Combatants.Single(actor => actor.Id == "original");
        NativeBattleEnemyTemplate template = original.NativeTemplate! with { NativeStatusAllocation = new(0x40, 0x2020, original.NativeTemplate!.NativeStatusAllocation!.Resistances) };
        snapshot = snapshot with { Definition = snapshot.Definition with { Combatants = snapshot.Definition.Combatants.Select(actor => actor.Id == "original" ? actor with { NativeTemplate = template } : actor).ToArray() } };
        int index = Array.FindIndex(snapshot.Combatants, actor => actor.Id == "original");
        snapshot.Combatants[index] = snapshot.Combatants[index] with { NativeStatus = snapshot.Combatants[index].NativeStatus! with { Saved = 0x42, Battle = 0x22020 } };
        BattleSession battle = BattleSession.Restore(snapshot);
        BattleEncounterAllocationState batch = battle.BeginEncounterAllocation("fixture.summon", "original", "support", "assault");
        BattleSnapshot copies = battle.Capture();
        foreach (string id in batch.ActorIds)
        {
            NativeBattleStatusSnapshot status = copies.Combatants.Single(actor => actor.Id == id).NativeStatus!;
            Assert.Equal(0x40, status.Saved & 0x42);
            Assert.Equal(0x2020u, status.Battle & 0x22020);
        }
    }

    [Theory]
    [InlineData(1.0, 312)]
    [InlineData(1.5, 468)]
    public void TemplateHpMultiplierUsesComposedOriginalMaximumOnceAndRestoresResolvedCopies(double multiplier, int expectedMaximum)
    {
        BattleDefinition baseline = Create(0).ReadDefinition();
        BattleCombatantDefinition original = baseline.Combatants.Single(actor => actor.Id == "original");
        NativeBattleEnemyLevel level = original.NativeTemplate!.Levels[original.Level];
        original = original with { MaximumHp = EncounterActorProperties.ScaleMaximumHp(original.MaximumHp, 1.25), MaximumHpMultiplier = 1.25, CurrentHp = 80, Speed = level.Speed, Attack = level.Strength, Defense = level.Vitality, Magic = level.Magic, Spirit = level.Spirit, Evasion = level.Evasion, ElementDefenses = level.ElementDefenses.ToArray() };
        EncounterCompiledDefinition mod = new("fixture.encounter", "Classic", "fixture-hp", "original", 11,
            ImmutableDictionary<string, EncounterCompiledPolicy>.Empty, ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, EncounterCompiledTemplate>.Empty.Add("fixture/support", new("fixture/support", "original", "fixture/family", "fixture/policy", "fixture/arrival")),
            ImmutableDictionary<string, ImmutableArray<string>>.Empty, ImmutableDictionary<string, EncounterCompiledPresentation>.Empty, ImmutableDictionary<string, EncounterCompiledDamageRoute>.Empty, ImmutableDictionary<string, EncounterCompiledEventGate>.Empty, ImmutableDictionary<string, EncounterCompiledPlacement>.Empty,
            ImmutableDictionary<string, double>.Empty.Add("original/stats.maximumHpMultiplier", 1.25).Add("fixture/support/stats.maximumHpMultiplier", multiplier));
        BattleSession battle = new(baseline with { Combatants = baseline.Combatants.Select(actor => actor.Id == "original" ? original : actor).ToArray(), EncounterMod = mod }, BattleAtbMode.Normal);
        BattleEncounterAllocationState batch = battle.BeginEncounterAllocation("fixture.summon", "original", "fixture/support", "assault");
        BattleCombatant support = battle.Combatants.Single(actor => actor.Id == batch.ActorIds[0]);
        Assert.Equal(expectedMaximum, support.MaximumHp);
        Assert.Equal(expectedMaximum, support.CurrentHp);
        Assert.Equal(312, battle.Combatants.Single(actor => actor.Id == batch.ActorIds[1]).MaximumHp);
        Assert.Equal(80, battle.Combatants.Single(actor => actor.Id == "original").CurrentHp);
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
        Assert.Equal(1, snapshot.NativeAllocations[0].ResolvedDefinition!.MaximumHpMultiplier);
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.Equal(expectedMaximum, restored.Combatants.Single(actor => actor.Id == support.Id).MaximumHp);
        Assert.Equal(expectedMaximum, restored.Combatants.Single(actor => actor.Id == support.Id).CurrentHp);
        Assert.Equal(312, restored.Combatants.Single(actor => actor.Id == "original").MaximumHp);
        Assert.Equal(80, restored.Combatants.Single(actor => actor.Id == "original").CurrentHp);
    }

    [Fact]
    public void BerserkCopyUsesInheritedPhysicalAbilityWithoutOriginalLifecycleCallbacks()
    {
        BattleSession battle = Create(0);
        BattleEncounterAllocationState batch = battle.BeginEncounterAllocation("fixture.summon", "original", "support", "assault");
        battle.BeginEncounterAllocationArrival();
        battle.CompleteEncounterAllocationArrival();
        BattleSnapshot snapshot = battle.Capture();
        int index = Array.FindIndex(snapshot.Combatants, actor => actor.Id == batch.ActorIds[0]);
        snapshot.Combatants[index] = snapshot.Combatants[index] with { Atb = BattleSession.AtbMaximum, NativeStatus = snapshot.Combatants[index].NativeStatus! with { Saved = 0x20 } };
        battle = BattleSession.Restore(snapshot);
        for (int frame = 0; frame < 8 && battle.NativeActionPackets.ActionCount == 0; frame++)
        {
            battle.Tick();
        }
        Assert.Equal((byte)1, battle.NativeActionPackets.ActionCount);
        NativeBattleActionHeaderSlot header = battle.NativeActionPackets.Headers[0];
        Assert.Equal((byte)4, header.ReadByte(0));
        Assert.Equal((byte)8, header.ReadByte(1));
        Assert.Equal((byte)7, header.ReadByte(2));
        Assert.Equal((byte)0, header.ReadByte(4));
        Assert.Equal((byte)0, battle.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value).Primary.ActorIndex);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Message");
        Assert.Null(battle.GetCombatantDefinition(batch.ActorIds[0]).Rewards);
    }

    private static BattleSession Create(int allies, int projectedHp = 250)
    {
        NativeBattleEnemyLevel level = new(projectedHp, 10, 10, 10, 10, 10, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
        NativeBattleEnemyTemplate template = new(17, Enumerable.Repeat(level, 101).ToArray()) { NativeStatusAllocation = new(0, 0, Enumerable.Repeat((byte)100, 40).ToImmutableArray()) };
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(8, 7, 0), 48).ToArray();
        BattleNativeAction physical = new(8, 0, 3, 0) { MagicId = 1, AttackType = 1, Power = 1, HitCount = 1, TargetFlags = 0, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = new byte[20].ToImmutableArray(), SupplementalRecord = [] };
        List<BattleCombatantDefinition> actors =
        [
            new() { Id = "party", Name = "Party", IsPlayer = true },
            new() { Id = "original", Name = "Original", SceneSlot = 0, MaximumHp = 250, CurrentHp = 80, Level = 17, NativeTemplate = template, NativeAi = new([[0], [0], [0], [0], [0]], abilities, 0, []) }
        ];
        actors.AddRange(Enumerable.Range(0, allies).Select(index => new BattleCombatantDefinition { Id = "ally-" + index, Name = "Ally", SceneSlot = index + 1 }));
        return new(new() { NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = actors.ToArray(), NativeActions = [physical] }, BattleAtbMode.Normal);
    }
}
