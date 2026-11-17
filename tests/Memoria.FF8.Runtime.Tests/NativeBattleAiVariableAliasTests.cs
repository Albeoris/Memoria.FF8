using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAiVariableAliasTests
{
    [Fact]
    public void NativeActorThreeVariable96WritesThePartyResistanceDwordAndRestoresIt()
    {
        BattleSession battle = Create([14, 96, 1, 0]);
        BattleSnapshot snapshot = battle.Capture();

        Assert.Equal(new byte[] { 1, 0, 0, 0 }, snapshot.Combatants[0].NativeStatus!.Resistances![20..24]);
        Assert.All(snapshot.Definition.Combatants[0].NativeStatusResistances, value => Assert.Equal(100, value));
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.Equal(snapshot.Combatants[0].NativeStatus!.Resistances, restored.Capture().Combatants[0].NativeStatus!.Resistances);
    }

    [Fact]
    public void NativeActorAliasAdditionCarriesAcrossAllFourBytesAndWrapsUnsigned()
    {
        BattleSession battle = Create([18, 96, 1, 0], 255);

        Assert.Equal(new byte[4], battle.Capture().Combatants[0].NativeStatus!.Resistances![20..24]);
    }

    [Fact]
    public void NativeActorAliasCanAddressAnotherActorsOwnedLocalVariables()
    {
        //Actor3+4*(64-211) is party0+36, the first local DWORD.
        BattleSession battle = Create([14, 64, 42, 0]);

        Assert.Equal(42u, battle.Capture().Combatants[0].NativeParticipant!.Variables[0]);
    }

    [Fact]
    public void NativeActorAliasesRejectUnownedMemoryAndMissingResistanceState()
    {
        Assert.Contains("unowned actor memory", Assert.Throws<NotSupportedException>(() => Create([14, 95, 1, 0], known: false)).Message);
        BattleSnapshot snapshot = Create([0]).Capture();
        snapshot = snapshot with { Combatants = snapshot.Combatants.Select((actor, index) => index == 0 ? actor with { NativeStatus = actor.NativeStatus! with { Resistances = null } } : actor).ToArray() };

        Assert.Contains("resistance bank", Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot)).Message);
    }

    [Theory]
    [InlineData(100, 101)]
    [InlineData(255, 0)]
    public void ExpandedOriginalSlotsCarryAndWrapAliasesWithoutTouchingTheAdditionalAlly(byte resistance, byte expectedFirst)
    {
        BattleSession battle = CreateExpanded([14, 64, 42, 18, 64, 1, 18, 96, 1, 14, 220, 5, 0], resistance);
        BattleSnapshot snapshot = battle.Capture();
        byte[] expected = resistance == byte.MaxValue ? [0, 0, 0, 0] : [expectedFirst, resistance, resistance, resistance];
        Assert.Equal(expected, snapshot.Combatants[0].NativeStatus!.Resistances![20..24]);
        Assert.Equal(43u, snapshot.Combatants[0].NativeParticipant!.Variables[0]);
        Assert.Equal(5u, snapshot.Combatants[4].NativeParticipant!.Variables[0]);
        Assert.Equal(0u, snapshot.Combatants[3].NativeParticipant!.Variables[0]);
        Assert.All(snapshot.Combatants[3].NativeStatus!.Resistances!, value => Assert.Equal(72, value));

        BattleSession restored = DetachedExpandedAiFixture.Restore(snapshot);
        Assert.Equal(expected, restored.Capture().Combatants[0].NativeStatus!.Resistances![20..24]);
        Assert.Equal(43u, restored.Capture().Combatants[0].NativeParticipant!.Variables[0]);
        Assert.Equal(0u, restored.Capture().Combatants[3].NativeParticipant!.Variables[0]);
    }

    [Fact]
    public void ExpandedOriginalSlotsRejectUnownedAliasesAndAnAdditionalCaller()
    {
        Assert.Contains("unowned actor memory", Assert.Throws<NotSupportedException>(() => CreateExpanded([14, 90, 1, 0])).Message);
        Assert.Contains("unowned actor memory", Assert.Throws<NotSupportedException>(() => CreateExpanded([14, 96, 1, 0], known: false)).Message);

        BattleSession battle = CreateExpanded([0]);
        BattleCombatant additional = Assert.Single(battle.Combatants, actor => actor.Id == "party-3");
        // An additional ally cannot normally enter the enemy AI interpreter. Exercise the alias boundary directly.
        MethodInfo write = typeof(BattleSession).GetMethod("WriteNativeActorVariable", BindingFlags.Instance | BindingFlags.NonPublic)!;
        TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => write.Invoke(battle, [additional, 96, 1u, false]));
        Assert.Contains("requires an actor-bank alias", Assert.IsType<NotSupportedException>(failure.InnerException).Message);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(18)]
    public void PhysicalVariableWritesRequireAnOriginalBindingAndActualNumericOperand(byte opcode)
    {
        NativeBattleAiDefinition numeric = Program([opcode, 96, 1, 0]);
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(numeric, [], 0));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(numeric, [], 0, originalEnemySlots: true));

        NativeBattleAiDefinition reference = Program([opcode, 96, 203, 0]);
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(reference, [], 0));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(reference, [], 0, originalEnemySlots: true));

        BattleSession battle = CreateExpanded([opcode, 96, 203, 0], interpret: false);
        BattleCombatant actor = Assert.Single(battle.Combatants, candidate => candidate.Id == "enemy");
        actor.LastAttackerId = "party-3";
        actor.LastNativeAction = new(null, null, null, null, null, null);
        Assert.Throws<NotSupportedException>(() => DetachedExpandedAiFixture.InterpretScalarCallback(battle, "enemy", 0));
    }

    private static BattleSession CreateExpanded(byte[] init, byte resistance = 100, bool known = true, bool interpret = true)
    {
        NativeBattleEnemyLevel level = new(100, 10, 10, 10, 10, 10, new int[8], [], [], 0);
        NativeBattleEnemyTemplate template = new(5, Enumerable.Repeat(level, 101).ToArray());
        BattleSession battle = new(new BattleDefinition
        {
            UsesOriginalEnemySlots = true,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "party-0", Name = "Original ally", IsPlayer = true, NativeStatusResistances = known ? Enumerable.Repeat(resistance, 40).ToArray() : [] },
                new() { Id = "party-1", Name = "Second ally", IsPlayer = true },
                new() { Id = "party-2", Name = "Third ally", IsPlayer = true },
                new() { Id = "party-3", Name = "Additional ally", IsPlayer = true, NativeStatusResistances = Enumerable.Repeat((byte)72, 40).ToArray() },
                new() { Id = "enemy", Name = "Enemy", ModelId = 71, SceneSlot = 0, Level = 5, NativeTemplate = template, NativeAi = Program(init) }
            ]
        }, BattleAtbMode.Normal);
        if (interpret)
        {
            DetachedExpandedAiFixture.InterpretScalarCallback(battle, "enemy", 0);
        }
        return battle;
    }

    private static NativeBattleAiDefinition Program(byte[] init)
    {
        return new([init, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
    }

    private static BattleSession Create(byte[] init, byte resistance = 100, bool known = true)
    {
        NativeBattleAiDefinition ai = new([init, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, NativeStatusResistances = known ? Enumerable.Repeat(resistance, 40).ToArray() : [] },
                new() { Id = "enemy", Name = "Enemy", ModelId = 71, NativeAi = ai }
            ]
        }, BattleAtbMode.Normal);
        DetachedExpandedAiFixture.InterpretScalarCallback(battle, "enemy", 0);
        return battle;
    }
}
