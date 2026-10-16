using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class NativeBattleAiConditionTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(5, false)]
    [InlineData(255, false)]
    public void GroupDeathChecksEveryPartySlotAndUsesNativeBooleanComparison(int comparison, bool expected)
    {
        BattleSession battle = Create(200, comparison, 0, Party("living", 0), Party("dead", 3) with { NativeStatus0 = 1, CurrentHp = 0 });
        Assert.Equal(expected ? 1u : 0u, battle.Capture().NativeAiVariables![0]);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).Capture()));
    }

    [Theory]
    [InlineData(200, false)]
    [InlineData(201, true)]
    [InlineData(44, true)]
    [InlineData(45, false)]
    [InlineData(203, false)]
    public void GroupSelectorsUsePhysicalSidesOrAllMatchingModelsWithoutConsumingRandomness(int selector, bool expected)
    {
        BattleSession battle = Create(selector, 0, 0, Party("party", 0), Enemy("dead", 28) with { NativeStatus0 = 1, CurrentHp = 0, InitialHidden = true, InitialUntargetable = true, InitialSuspended = true });
        Assert.Equal(expected ? 1u : 0u, battle.Capture().NativeAiVariables![0]);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleRandomState.Initialize(0)), JsonSerializer.Serialize(battle.Capture().Random));
    }

    [Theory]
    [InlineData(1, BattleStatusKind.Poison)]
    [InlineData(3, BattleStatusKind.Blind)]
    [InlineData(4, BattleStatusKind.Silence)]
    [InlineData(16, BattleStatusKind.Sleep)]
    [InlineData(17, BattleStatusKind.Haste)]
    [InlineData(18, BattleStatusKind.Slow)]
    [InlineData(20, BattleStatusKind.Regen)]
    [InlineData(21, BattleStatusKind.Protect)]
    [InlineData(22, BattleStatusKind.Shell)]
    public void GroupStatusUsesBothNativeBanksAndIgnoresDeadAndInactiveActors(int status, BattleStatusKind kind)
    {
        BattleCombatantDefinition affected = Enemy("affected", 28) with { StatusEffects = [new() { Kind = kind, Permanent = true }] };
        Assert.Equal(1u, Create(44, 0, status, Party("party", 0), Enemy("unaffected", 28), affected).Capture().NativeAiVariables![0]);
        Assert.Equal(0u, Create(44, 0, status, Party("party", 0), affected with { NativeStatus0 = 1, CurrentHp = 0 }).Capture().NativeAiVariables![0]);
        Assert.Equal(0u, Create(44, 0, status, Party("party", 0), affected with { InitialActive = false, SceneSlot = 0 }).Capture().NativeAiVariables![0]);
    }

    [Theory]
    [InlineData(201)]
    [InlineData(44)]
    public void GroupStatusHonorsTheOriginalFourEnemySlots(int selector)
    {
        BattleSession battle = Create(selector, 0, 0, Party("party", 0), Enemy("one", 28), Enemy("two", 28), Enemy("three", 28), Enemy("four", 28), Enemy("outside", 28) with { NativeStatus0 = 1, CurrentHp = 0 });
        Assert.Equal(0u, battle.Capture().NativeAiVariables![0]);
    }

    [Fact]
    public void ModelPresenceUsesTheSameNativeSlotBoundary()
    {
        BattleSession battle = CreateCondition(9, 200, 0, 44, Party("party", 0), Enemy("one", 1), Enemy("two", 1), Enemy("three", 1), Enemy("four", 1));
        Assert.Equal(0u, battle.Capture().NativeAiVariables![0]);
    }

    [Theory]
    [InlineData(4, 256)]
    [InlineData(5, 256)]
    public void UnsupportedStatusReportsTheOriginalInstructionAndEncounterWithoutTruncatingTheWord(int subject, int status)
    {
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => CreateCondition(subject, 200, 0, status, Party("party", 0)));
        Assert.Contains("encounter 40", exception.Message);
        Assert.Contains("model 28, callback 0, byte 0", exception.Message);
        Assert.Contains($"status {status}", exception.Message);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void SavedWordBitSevenKeepsItsCanonicalOwnerAndSurvivesRestore(int subject)
    {
        BattleCombatantDefinition party = Party("party", 0) with { NativeRetainedStatusBits = 0x80 };
        BattleSession battle = CreateCondition(subject, subject == 4 ? 0 : 200, 0, 7, party);
        Assert.Equal(1u, battle.Capture().NativeAiVariables![0]);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Assert.Equal((ushort)0x80, restored.Combatants.Single(actor => actor.IsPlayer).NativeRetainedStatusBits);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void AbsentConfuseReadsClearWithoutChangingOtherNativeStatusBits(int subject)
    {
        Assert.Equal(0u, CreateCondition(subject, 200, 0, 30, Party("party", 0)).Capture().NativeAiVariables![0]);
        Assert.Equal(1u, CreateCondition(subject, 200, 3, 30, Party("party", 0)).Capture().NativeAiVariables![0]);
    }

    private static BattleSession Create(int selector, int comparison, int status, params BattleCombatantDefinition[] others)
    {
        return CreateCondition(5, selector, comparison, status, others);
    }

    private static BattleSession CreateCondition(int subject, int selector, int comparison, int status, params BattleCombatantDefinition[] others)
    {
        // IF (8 bytes), SET shared96=1 (3 bytes), STOP; false branches skip exactly the SET.
        byte[] initial = [2, (byte)subject, (byte)selector, (byte)comparison, (byte)status, (byte)(status >> 8), 3, 0, 15, 96, 1, 0];
        NativeBattleAiDefinition ai = new([initial, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        return new(new() { EncounterId = 40, Combatants = [.. others, Enemy("owner", 28) with { NativeAi = ai }] }, BattleAtbMode.Normal);
    }

    private static BattleCombatantDefinition Party(string id, int model) => new() { Id = id, Name = id, ModelId = model, IsPlayer = true };
    private static BattleCombatantDefinition Enemy(string id, int model) => new() { Id = id, Name = id, ModelId = model };
}
