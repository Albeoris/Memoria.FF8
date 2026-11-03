using System.Collections.Immutable;
using System.Reflection;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class EncounterActorPropertyPublicationTests
{
    [Fact]
    public void ReflectResistancePublishesIntoBothNativeBanksAndFreshCopiesWithoutChangingTheResource()
    {
        byte[] resistances = Enumerable.Repeat((byte)73, 40).ToArray();
        resistances[15] = 255;
        NativeBattleEnemyLevel level = new(1000, 20, 5, 20, 5, 100, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
        NativeBattleEnemyTemplate native = new(1, Enumerable.Repeat(level, 101).ToArray()) { NativeStatusAllocation = new(0, 0, resistances.ToImmutableArray()) };
        BattleDefinition baseline = new()
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy-0", Name = "Original", SceneSlot = 0, MaximumHp = 1000, CurrentHp = 1000, NativeTemplate = native, NativeStatusResistances = resistances }]
        };
        EncounterCompiledDefinition compiled = new("fixture.encounter", "Classic", "fixture-reflect", "enemy-0", 11,
            ImmutableDictionary<string, EncounterCompiledPolicy>.Empty, ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, EncounterCompiledTemplate>.Empty.Add("fixture/support", new("fixture/support", "original", "fixture/family", "fixture/policy", "fixture/arrival")),
            ImmutableDictionary<string, ImmutableArray<string>>.Empty, ImmutableDictionary<string, EncounterCompiledPresentation>.Empty, ImmutableDictionary<string, EncounterCompiledDamageRoute>.Empty, ImmutableDictionary<string, EncounterCompiledEventGate>.Empty, ImmutableDictionary<string, EncounterCompiledPlacement>.Empty,
            ImmutableDictionary<string, double>.Empty.Add("original/" + EncounterActorProperties.ReflectResistanceMultiplier, 0));
        MethodInfo apply = typeof(GameResourceHost).GetMethod("ApplyCompiledEncounterDefinition", BindingFlags.Static | BindingFlags.NonPublic)!;
        BattleDefinition published = (BattleDefinition)apply.Invoke(null, [baseline, new EncounterBindingResult("fixture.encounter", "enemy-0", 11, 11, [], false), compiled])!;
        Assert.Equal((byte)255, resistances[15]);
        Assert.Equal((byte)255, native.NativeStatusAllocation!.Resistances[15]);
        Assert.Equal((byte)0, published.Combatants[1].NativeStatusResistances[15]);
        Assert.Equal((byte)0, published.Combatants[1].NativeTemplate!.NativeStatusAllocation!.Resistances[15]);
        BattleSession battle = new(published, BattleAtbMode.Normal);
        BattleEncounterAllocationState batch = battle.BeginEncounterAllocation("fixture.summon", "enemy-0", "fixture/support");
        foreach (string id in new[] { "enemy-0", batch.ActorIds[0] })
        {
            BattleCombatantSnapshot actor = battle.Capture().Combatants.Single(actor => actor.Id == id);
            Assert.Equal((byte)0, actor.NativeStatus!.Resistances![15]);
            Assert.Equal((byte)73, actor.NativeStatus.Resistances[14]);
            Assert.Equal((byte)73, actor.NativeStatus.Resistances[16]);
        }
        BattleSession restored = BattleSession.Restore(System.Text.Json.JsonSerializer.Deserialize<BattleSnapshot>(System.Text.Json.JsonSerializer.Serialize(battle.Capture()))!);
        Assert.Equal((byte)0, restored.Capture().Combatants.Single(actor => actor.Id == batch.ActorIds[0]).NativeStatus!.Resistances![15]);
    }

    [Fact]
    public void IndependentOriginalAndTemplateHpPropertiesPublishAtTheirOwnInitializationBoundaries()
    {
        NativeBattleEnemyLevel level = new(1000, 20, 5, 20, 5, 100, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
        NativeBattleEnemyTemplate native = new(1, Enumerable.Repeat(level, 101).ToArray()) { NativeStatusAllocation = new(0, 0, Enumerable.Repeat((byte)100, 40).ToImmutableArray()) };
        BattleDefinition baseline = new()
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy-0", Name = "Original", SceneSlot = 0, MaximumHp = 1000, CurrentHp = 1000, NativeTemplate = native }]
        };
        EncounterCompiledDefinition compiled = new("fixture.encounter", "Classic", "fixture-publication", "enemy-0", 11,
            ImmutableDictionary<string, EncounterCompiledPolicy>.Empty, ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, EncounterCompiledTemplate>.Empty.Add("fixture/support", new("fixture/support", "original", "fixture/family", "fixture/policy", "fixture/arrival")),
            ImmutableDictionary<string, ImmutableArray<string>>.Empty, ImmutableDictionary<string, EncounterCompiledPresentation>.Empty, ImmutableDictionary<string, EncounterCompiledDamageRoute>.Empty, ImmutableDictionary<string, EncounterCompiledEventGate>.Empty, ImmutableDictionary<string, EncounterCompiledPlacement>.Empty,
            ImmutableDictionary<string, double>.Empty.Add("original/stats.maximumHpMultiplier", 1.25).Add("fixture/support/stats.maximumHpMultiplier", 1.5));
        MethodInfo apply = typeof(GameResourceHost).GetMethod("ApplyCompiledEncounterDefinition", BindingFlags.Static | BindingFlags.NonPublic)!;
        BattleDefinition published = (BattleDefinition)apply.Invoke(null, [baseline, new EncounterBindingResult("fixture.encounter", "enemy-0", 11, 11, [], false), compiled])!;
        Assert.Equal(1250, published.Combatants[1].MaximumHp);
        Assert.Equal(1250, published.Combatants[1].CurrentHp);
        Assert.Equal(1.5, published.EncounterMod!.ActorProperties["fixture/support/stats.maximumHpMultiplier"]);
        published = published with { Combatants = published.Combatants.Select(actor => actor.Id == "enemy-0" ? actor with { CurrentHp = 200 } : actor).ToArray() };
        BattleSession battle = new(published, BattleAtbMode.Normal);
        BattleEncounterAllocationState batch = battle.BeginEncounterAllocation("fixture.summon", "enemy-0", "fixture/support");
        BattleCombatant copy = battle.Combatants.Single(actor => actor.Id == batch.ActorIds[0]);
        Assert.Equal(1875, copy.MaximumHp);
        Assert.Equal(1875, copy.CurrentHp);
        Assert.Equal(200, battle.Combatants.Single(actor => actor.Id == "enemy-0").CurrentHp);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Assert.Equal(1875, restored.Combatants.Single(actor => actor.Id == copy.Id).MaximumHp);
        Assert.Equal(1875, restored.Combatants.Single(actor => actor.Id == copy.Id).CurrentHp);
    }
}
