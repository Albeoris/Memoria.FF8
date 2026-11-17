using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAiAdmissionTests
{
    private static readonly BattleNativeAction EnemyAction = new(8, 1, 3, 0)
    {
        Name = "Counter", MagicId = 0, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 0, DamagePresentation = 4, Element = 0, ElementPercent = null,
        StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
    };

    [Fact]
    public void FirstTurnCannotUseAnAttackerReferenceAssignedOnlyByCounter()
    {
        NativeBattleAiDefinition program = Program(turn: [4, 220, 7, 1, 6, 0], counter: [14, 220, 203, 0]);
        List<string> blockers = [];

        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(program, [EnemyAction], 0, blockers));
        Assert.Contains("actor-reference-target:220", blockers);
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(program, [EnemyAction], 0, originalEnemySlots: true));
    }

    [Theory]
    [InlineData(220, ExpandedAiAdmission.Supported)]
    [InlineData(221, ExpandedAiAdmission.RequiresOriginalSlots)]
    public void EveryIncomingBranchMustAssignTheReferenceBeforeASelectorReadsIt(byte secondAssignment, ExpandedAiAdmission expected)
    {
        byte[] counter = [2, 2, 2, 0, 0, 0, 6, 0, 14, 220, 203, 35, 3, 0, 14, secondAssignment, 203, 4, 220, 7, 1, 6, 0];

        Assert.Equal(expected, NativeBattleExpandedAi.Evaluate(Program(counter: counter), [EnemyAction], 0));
    }

    [Fact]
    public void InitializedMarkerGuardsBothActorConditionAndTargetSelectionUntilCounterRuns()
    {
        NativeBattleAiDefinition program = Program(init: [14, 220, 240, 0], counter: [14, 220, 203, 0],
            turn: [2, 220, 200, 3, 240, 0, 13, 0, 2, 7, 220, 0, 7, 0, 5, 0, 4, 220, 7, 1, 6, 0]);

        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(program, [EnemyAction], 0));
    }

    [Fact]
    public void AReachableLaterCallbackCanInvalidateAnInitiallySafeSelector()
    {
        NativeBattleAiDefinition program = Program(init: [14, 220, 240, 0], turn: [4, 220, 7, 1, 6, 0], counter: [14, 220, 203, 0]);
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(program, [EnemyAction], 0));
        program.Scripts[4] = [14, 220, 0, 0];

        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(program, [EnemyAction], 0));
    }

    [Fact]
    public void WritingANumberReplacesTheReferenceAndConditionSelectorsStillNeedAnActorSlot()
    {
        byte[] numericCounter = [14, 220, 203, 14, 220, 255, 4, 220, 0];
        byte[] actorCondition = [2, 7, 220, 0, 7, 0, 0, 0, 0];
        byte[] groupCondition = [2, 5, 220, 0, 7, 0, 0, 0, 0];

        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(Program(counter: numericCounter), [EnemyAction], 0));
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(Program(counter: [.. numericCounter[..^1], .. actorCondition]), [EnemyAction], 0));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(Program(counter: [.. numericCounter[..^1], .. groupCondition]), [EnemyAction], 0));
    }

    [Theory]
    [InlineData(0, 240, ExpandedAiAdmission.Supported)]
    [InlineData(3, 240, ExpandedAiAdmission.Supported)]
    [InlineData(1, 240, ExpandedAiAdmission.RequiresUnsupportedService)]
    [InlineData(3, 6, ExpandedAiAdmission.RequiresUnsupportedService)]
    public void OnlyEqualityComparisonsToNonSlotMarkersCanReadLogicalReferences(byte comparison, byte value, ExpandedAiAdmission expected)
    {
        byte[] counter = [14, 220, 203, 2, 220, 200, comparison, value, 0, 0, 0, 0];

        Assert.Equal(expected, NativeBattleExpandedAi.Evaluate(Program(counter: counter), [EnemyAction], 0));
    }

    [Theory]
    [InlineData(false, ExpandedAiAdmission.RequiresUnsupportedService)]
    [InlineData(true, ExpandedAiAdmission.Supported)]
    public void ArithmeticDependsOnTheValueThatReachesIt(bool overwrite, ExpandedAiAdmission expected)
    {
        byte[] counter = [14, 220, 203, .. (overwrite ? new byte[] { 14, 220, 240 } : []), 18, 220, 1, 0];

        Assert.Equal(expected, NativeBattleExpandedAi.Evaluate(Program(counter: counter), [EnemyAction], 0));
    }

    [Fact]
    public void StaticAndExecutingBranchesUseSignedJumpAndUnsignedConditionalWordFromTheEndOfTheRecord()
    {
        byte[] backwards = [35, 4, 0, 14, 220, 240, 0, 35, 249, 255];
        byte[] largeSkip = new byte[65556];
        byte[] skip = [2, 3, 0, 3, 0, 0, 0, 128];
        skip.CopyTo(largeSkip, 0);
        skip.CopyTo(largeSkip, 32776);
        new byte[] { 14, 220, 240, 0 }.CopyTo(largeSkip, 65552);
        foreach (byte[] init in new[] { backwards, largeSkip })
        {
            NativeBattleAiDefinition program = Program(init: init, turn: [4, 220, 0]);
            Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(program, [EnemyAction], 0));

            BattleSession battle = new(Definition(program), BattleAtbMode.Normal);
            battle.Tick();
            Assert.Equal(240u, Enemy(battle).AiVariables[0]);
        }
    }

    [Theory]
    [InlineData("0200", 0, "truncated-opcode:2")]
    [InlineData("2300", 0, "truncated-opcode:35")]
    [InlineData("23FCFF", -1, "branch-outside-script")]
    [InlineData("020300030000FFFF00", 65543, "branch-outside-script")]
    public void InvalidBranchRecordsReportCallbackAndByteOffset(string hex, int offset, string reason)
    {
        NativeBattleAiDefinition program = Program(init: Convert.FromHexString(hex));
        List<string> blockers = [];
        Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(program, [EnemyAction], 0, blockers));
        Assert.Contains(reason, blockers);
        Assert.Contains($"AI callback 0, byte {offset}: {reason}",
            Assert.Throws<InvalidDataException>(() => NativeBattleExpandedAi.Evaluate(program, [EnemyAction], 0, rosterRestrictions: new HashSet<BattleEncounterRosterRestrictionReason>())).Message);

        BattleSession battle = new(Definition(program), BattleAtbMode.Normal);
        Assert.Contains("callback 0, byte 0", Assert.Throws<InvalidDataException>(() => battle.Tick()).Message);
    }

    [Theory]
    [InlineData(240, 0, true)]
    [InlineData(0, 240, false)]
    public void LevelBandChangeAndRestoreUseRetainedLocalsWithoutReplayingDestinationInit(byte retained, byte destinationInit, bool allowed)
    {
        NativeBattleAiDefinition program = Program(init: [2, 14, 0, 0, 0, 0, 4, 0, 14, 220, retained, 0, 14, 220, destinationInit, 0],
            turn: [2, 14, 0, 0, 1, 0, 5, 0, 4, 220, 14, 221, 7, 0]);
        Assert.Equal(allowed ? ExpandedAiAdmission.RequiresOriginalSlots : ExpandedAiAdmission.Supported,
            NativeBattleExpandedAi.Evaluate(program with { LevelBand = 1 }, [EnemyAction], 0));

        BattleNativeAction levelUp = EnemyAction with { Command = 34, ActionId = 0, Name = "Level Up", AttackType = 16, Power = 16, KernelSection = 10, StatusAttack = 255, TargetFlags = 0x50 };
        ImmutableArray<NativeBattleDrawSlot> drawBand = [.. Enumerable.Repeat(new NativeBattleDrawSlot(0, 0), 4)];
        ExpandedEnemyLevelProfile levels = new([.. Enumerable.Range(1, 100).Select(level => new ExpandedEnemyLevel(100, [0, 0, 0, 0, 1, 0], level < 8 ? 0 : 1))], [drawBand, drawBand, drawBand]);
        string?[] messages = new string?[96];
        messages[11] = " 0123456789";
        messages[88] = " Level ";
        messages[89] = "raised";
        BattleDefinition definition = Definition(program);
        BattleSession battle = new(definition with
        {
            NativeActions = [EnemyAction, levelUp],
            ExpandedEnemyLevels = ImmutableSortedDictionary<int, ExpandedEnemyLevelProfile>.Empty.Add(0, levels),
            Combatants = [definition.Combatants[0] with { NativeCommands = [new(34, "Level Up", 0x50, 0)], InitialAtb = BattleSession.AtbMaximum }, definition.Combatants[1]],
            NativeGlobalMessages = messages,
            NativeMessageSpeed = 0,
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeFieldBattleMode = 0x200
        }, BattleAtbMode.TurnBased);
        DetachedExpandedAiFixture.DispatchLeadingInitializers(battle);
        if (!allowed)
        {
            Assert.Throws<InvalidOperationException>(() => battle.QueueNativePlayerCommand("party", "enemy", 34, 0));
            Assert.Equal(4, Enemy(battle).Level);
            Assert.Equal((uint)retained, Enemy(battle).AiVariables[0]);
            return;
        }
        battle.QueueNativePlayerCommand("party", "enemy", 34, 0);
        DetachedExpandedAiFixture.AdvanceTo(battle, () => battle.QueuedActions.Count == 0 && battle.ActiveAction is null, "The level change command must finish.");

        BattleSession restored = DetachedExpandedAiFixture.Restore(battle.Capture());
        foreach (BattleSession owner in new[] { battle, restored })
        {
            BattleCombatant enemy = Enemy(owner);
            Assert.Equal(8, enemy.Level);
            Assert.Equal((uint)retained, enemy.AiVariables[0]);
            Assert.Single(owner.Combatants, actor => actor.IsPlayer).Atb = 0;
            enemy.Atb = BattleSession.AtbMaximum;
            DetachedExpandedAiFixture.AdvanceTo(owner, () => enemy.AiTurnCount == 1, "The enemy must execute its next turn.");
            Assert.Equal(7u, enemy.AiVariables[1]);
        }
        DetachedExpandedAiFixture.AssertSameContinuation(battle, restored);
    }

    private static NativeBattleAiDefinition Program(byte[]? init = null, byte[]? turn = null, byte[]? counter = null)
    {
        return new([init ?? [0], turn ?? [0], counter ?? [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
    }

    private static BattleDefinition Definition(NativeBattleAiDefinition program)
    {
        return new()
        {
            EncounterId = 0,
            NativeSeed = 0,
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, CurrentHp = 100, MaximumHp = 100, Speed = 1 },
                new() { Id = "enemy", Name = "Enemy", ModelId = 0, Level = 4, CurrentHp = 100, MaximumHp = 100, Speed = 1, NativeAi = program }],
            NativeActions = [EnemyAction]
        };
    }

    private static BattleCombatant Enemy(BattleSession battle) => Assert.Single(battle.Combatants, actor => actor.Id == "enemy");
}
