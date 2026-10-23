using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAiDifficultyTests
{
    [Fact]
    public void Hardcore_keeps_hard_random_idle_suppression_and_explicit_do_not_attack()
    {
        foreach (byte[] script in new byte[][] { [2, 2, 3, 0, 0, 0, 1, 0, 0, 15, 96, 7, 0], [11, 0, 253, 0, 0] })
        {
            for (byte seed = 0; seed < 24; seed++)
            {
                BattleSession hard = Create(script, BattleDifficulty.Hard, seed);
                BattleSession hardcore = Create(script, BattleDifficulty.Hardcore, seed);
                hard.Tick();
                hardcore.Tick();
                Assert.Equal(hard.Capture().NativeAiVariables, hardcore.Capture().NativeAiVariables);
                Assert.Equal(hard.QueuedActions.Count, hardcore.QueuedActions.Count);
                AssertRandomEqual(hard.Capture().Random!, hardcore.Capture().Random!);
            }
        }
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void HardPreservesTheExplicitDoNotAttackChoiceInEveryRandomPosition(int emptyPosition)
    {
        for (byte seed = 0; seed < 24; seed++)
        {
            byte[] choices = [0, 0, 0];
            choices[emptyPosition] = 253;
            BattleSession battle = Create([11, .. choices, 0], BattleDifficulty.Hard, seed);
            BattleSession normal = Create([11, .. choices, 0], BattleDifficulty.Normal, seed);
            battle.Tick();
            normal.Tick();
            NativeBattleRandomState.Initialize(seed).Draw(out byte roll);
            Assert.Equal(roll % 3 == emptyPosition ? 0 : 1, battle.QueuedActions.Count);
            Assert.Equal(normal.QueuedActions.Count, battle.QueuedActions.Count);
            AssertRandomEqual(normal.Capture().Random!, battle.Capture().Random!);
        }
    }

    [Fact]
    public void HardPreservesZeroIdRowsExplicitSelectionsAndAllEmptyChoices()
    {
        foreach (byte[] script in new byte[][] { [12, 1, 0], [11, 1, 253, 1, 0], [11, 253, 253, 253, 0], [0] })
        {
            BattleSession battle = Create(script, BattleDifficulty.Hard);
            battle.Tick();
            Assert.Empty(battle.QueuedActions);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void HardUsesTheCurrentLevelBandAndPreservesDuplicateChoiceWeights(int band)
    {
        for (byte seed = 0; seed < 24; seed++)
        {
            NativeBattleRandomState.Initialize(seed).Draw(out byte roll);
            BattleSession weighted = Create([11, 0, 0, 2, 0], BattleDifficulty.Hard, seed, band: band);
            weighted.Tick();
            Assert.Equal((ushort)(roll % 3 == 2 ? 2 : 1), Assert.Single(weighted.QueuedActions).NativeAction!.ActionId);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void HardSkipsRandomReturnEdgesForEitherComparisonOutcome(byte comparison)
    {
        for (byte seed = 0; seed < 24; seed++)
        {
            // False edge skips return and its unconditional jump; true edge has no side effects.
            BattleSession battle = Create([2, 2, 3, comparison, 0, 0, 4, 0, 0, 35, 0, 0, 15, 96, 7, 0], BattleDifficulty.Hard, seed);
            battle.Tick();
            Assert.Equal(7u, battle.Capture().NativeAiVariables![0]);
            NativeBattleRandomState expected = NativeBattleRandomState.Initialize(seed).Draw(out _);
            AssertRandomEqual(expected, battle.Capture().Random!);
        }
    }

    [Fact]
    public void HardSkipsFalseReturnEdgeThroughUnconditionalJump()
    {
        BattleSession battle = Create([2, 2, 3, 0, 0, 0, 4, 0, 15, 96, 7, 0, 35, 0, 0, 0], BattleDifficulty.Hard);
        battle.Tick();
        Assert.Equal(7u, battle.Capture().NativeAiVariables![0]);
    }

    [Fact]
    public void StateBasedReturnsAndSideEffectfulRandomBranchesRemainAuthored()
    {
        BattleSession stateGuard = Create([2, 96, 0, 0, 0, 0, 1, 0, 0, 15, 96, 7, 0], BattleDifficulty.Hard);
        stateGuard.Tick();
        Assert.Equal(0u, stateGuard.Capture().NativeAiVariables![0]);
        BattleSession bothEmpty = Create([2, 2, 3, 0, 0, 0, 1, 0, 0, 0], BattleDifficulty.Hard);
        bothEmpty.Tick();
        Assert.Empty(bothEmpty.QueuedActions);
        BattleSession sideEffects = Create([2, 2, 1, 0, 0, 0, 4, 0, 15, 96, 9, 0, 15, 96, 7, 0], BattleDifficulty.Hard);
        sideEffects.Tick();
        Assert.Equal(9u, sideEffects.Capture().NativeAiVariables![0]);
    }

    [Fact]
    public void NormalKeepsRandomReturnsAndHardLeavesInitialCallbacksUnchanged()
    {
        bool observedEmpty = false;
        bool observedAction = false;
        byte[] guard = [2, 2, 3, 0, 0, 0, 1, 0, 0, 15, 96, 7, 0];
        for (byte seed = 0; seed < 24; seed++)
        {
            NativeBattleRandomState.Initialize(seed).Draw(out byte roll);
            uint expected = roll % 3 == 0 ? 0u : 7u;
            BattleSession normalGuard = Create(guard, BattleDifficulty.Normal, seed);
            normalGuard.Tick();
            Assert.Equal(expected, normalGuard.Capture().NativeAiVariables![0]);
            BattleSession initialGuard = Create([0], BattleDifficulty.Hard, seed, guard);
            initialGuard.Tick();
            Assert.Equal(expected, initialGuard.Capture().NativeAiVariables![0]);
            BattleSession normal = Create([11, 0, 253, 253, 0], BattleDifficulty.Normal, seed);
            normal.Tick();
            observedEmpty |= normal.QueuedActions.Count == 0;
            observedAction |= normal.QueuedActions.Count == 1;
            BattleSession initial = Create([0], BattleDifficulty.Hard, seed, [11, 0, 253, 253, 0]);
            initial.Tick();
            Assert.Equal(normal.QueuedActions.Count, initial.QueuedActions.Count);
        }
        Assert.True(observedEmpty);
        Assert.True(observedAction);
    }

    [Fact]
    public void HardRandomPolicyContinuesIdenticallyAfterJsonRestore()
    {
        BattleSession original = Create([2, 2, 3, 0, 0, 0, 1, 0, 0, 11, 0, 0, 0, 0], BattleDifficulty.Hard);
        string json = JsonSerializer.Serialize(original.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        original.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(original.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Single(restored.QueuedActions);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void HardLeavesReactionAndDeathCallbacksUnchanged(int callback)
    {
        byte[] script = [2, 2, 3, 0, 0, 0, 1, 0, 0, 15, 96, 7, 0];
        for (byte seed = 0; seed < 24; seed++)
        {
            BattleSession normal = Create(script, BattleDifficulty.Normal, seed, callback: callback);
            BattleSession hard = Create(script, BattleDifficulty.Hard, seed, callback: callback);
            System.Reflection.MethodInfo invoke = typeof(BattleSession).GetMethod("RunNativeAi", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            invoke.Invoke(normal, [normal.Combatants[1], callback]);
            invoke.Invoke(hard, [hard.Combatants[1], callback]);
            Assert.Equal(normal.Capture().NativeAiVariables, hard.Capture().NativeAiVariables);
            AssertRandomEqual(normal.Capture().Random!, hard.Capture().Random!);
        }
    }

    private static void AssertRandomEqual(NativeBattleRandomState expected, NativeBattleRandomState actual)
    {
        Assert.Equal(expected.Indexes.ToArray(), actual.Indexes.ToArray());
        Assert.Equal(expected.Lane, actual.Lane);
    }

    private static BattleSession Create(byte[] turn, BattleDifficulty difficulty, byte seed = 0, byte[]? initial = null, int callback = 1, int band = 0)
    {
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray();
        abilities[16 * band] = new(2, 7, 1);
        abilities[16 * band + 2] = new(2, 7, 2);
        byte[][] scripts = [initial ?? [0], [0], [0], [0], [0]];
        scripts[callback] = turn;
        NativeBattleAiDefinition ai = new(scripts, abilities, band, []);
        BattleNativeAction action = new(2, 1, 1, 1)
        {
            MagicId = 1, AttackType = 2, Power = 0, HitCount = 1, TargetFlags = 0, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        BattleSession battle = new(new BattleDefinition
        {
            NativeSeed = seed,
            NativeActions = [action, action with { ActionId = 2, KernelIndex = 2 }],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, Speed = 1 },
                new() { Id = "enemy", Name = "Enemy", ModelId = 1, MaximumHp = 500, CurrentHp = 500, NativeAi = ai, InitialAtb = initial is null ? BattleSession.AtbMaximum : 0 }
            ]
        }, BattleAtbMode.Normal);
        if (initial is null)
        {
            battle.Tick();
        }
        battle.SetDifficulty(difficulty);
        return battle;
    }
}
