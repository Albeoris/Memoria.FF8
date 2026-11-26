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
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(22)]
    public void GroupStatusUsesBothNativeBanksAndIgnoresDeadAndInactiveActors(int status)
    {
        BattleCombatantDefinition affected = Enemy("affected", 28) with { NativeStatus0 = status < 16 ? (ushort)(1 << status) : (ushort)0, NativeStatus1 = status >= 16 ? 1u << (status - 16) : 0 };
        Assert.Equal(1u, Create(44, 0, status, Party("party", 0), Enemy("unaffected", 28), affected).Capture().NativeAiVariables![0]);
        Assert.Equal(0u, Create(44, 0, status, Party("party", 0), affected with { NativeStatus0 = 1, CurrentHp = 0 }).Capture().NativeAiVariables![0]);
        Assert.Equal(0u, Create(44, 0, status, Party("party", 0), affected with { InitialActive = false, SceneSlot = 0 }).Capture().NativeAiVariables![0]);
    }

    [Theory]
    [InlineData(201)]
    [InlineData(44)]
    public void GroupStatusIncludesTheAllocatedEnemyBeyondOriginalSlots(int selector)
    {
        BattleSession battle = Create(selector, 0, 0, Party("party", 0), Enemy("one", 28), Enemy("two", 28), Enemy("three", 28), Enemy("four", 28), Enemy("outside", 28) with { NativeStatus0 = 1, CurrentHp = 0 });
        Assert.Null(battle.Combatants.Single(actor => actor.Id == "outside").NativeActorIndex);
        Assert.Equal(1u, battle.Capture().NativeAiVariables![0]);
    }

    [Fact]
    public void LivingModelPresenceIncludesTheAllocatedEnemyBeyondOriginalSlots()
    {
        BattleSession battle = CreateConditionWithOwner(1, 9, 200, 0, 44, Party("party", 0), Enemy("one", 1), Enemy("two", 1), Enemy("three", 1), Enemy("outside", 28));
        Assert.Null(battle.Combatants.Single(actor => actor.Id == "outside").NativeActorIndex);
        Assert.Equal(1u, battle.Capture().NativeAiVariables![0]);
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
        return CreateConditionWithOwner(28, subject, selector, comparison, status, others);
    }

    private static BattleSession CreateConditionWithOwner(int model, int subject, int selector, int comparison, int status, params BattleCombatantDefinition[] others)
    {
        // IF (8 bytes), SET shared96=1 (3 bytes), STOP; false branches skip exactly the SET.
        byte[] initial = [2, (byte)subject, (byte)selector, (byte)comparison, (byte)status, (byte)(status >> 8), 3, 0, 15, 96, 1, 0];
        NativeBattleAiDefinition ai = new([initial, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleCombatantDefinition[] actors = [Enemy("owner", model) with { NativeAi = ai }, .. others];
        bool authored = actors.Any(actor => !actor.InitialActive);
        if (authored)
        {
            NativeBattleEnemyLevel level = new(100, 20, 5, 20, 5, 10, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
            NativeBattleEnemyTemplate template = new(1, Enumerable.Repeat(level, 101).ToArray()) { Information254 = 0 };
            int sceneSlot = 0;
            for (int i = 0; i < actors.Length; i++)
            {
                if (!actors[i].IsPlayer)
                {
                    actors[i] = actors[i] with { SceneSlot = sceneSlot++, NativeTemplate = template };
                }
            }
        }
        BattleSession battle = new(new() { EncounterId = 40, Combatants = actors, UsesOriginalEnemySlots = authored }, BattleAtbMode.Normal);
        NativeBattleLogicalTestFixture.RunNativeAi(battle, battle.Combatants[0], 0);
        return battle;
    }

    private static BattleCombatantDefinition Party(string id, int model) => new() { Id = id, Name = id, ModelId = model, IsPlayer = true };
    private static BattleCombatantDefinition Enemy(string id, int model) => new() { Id = id, Name = id, ModelId = model };
}
