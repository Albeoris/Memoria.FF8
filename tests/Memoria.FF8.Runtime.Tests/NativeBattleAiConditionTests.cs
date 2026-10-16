namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAiConditionTests
{
    [Theory]
    [InlineData(0, 50, true)]
    [InlineData(0, 49, false)]
    [InlineData(1, 51, true)]
    [InlineData(1, 50, false)]
    [InlineData(2, 49, true)]
    [InlineData(2, 50, false)]
    [InlineData(3, 49, true)]
    [InlineData(3, 50, false)]
    [InlineData(4, 50, true)]
    [InlineData(4, 49, false)]
    [InlineData(5, 50, true)]
    [InlineData(5, 51, false)]
    public void HpPredicatesUseAllSixOriginalScalarComparisons(byte comparison, int threshold, bool expected)
    {
        Assert.Equal(expected, Passed(Definition(Condition(0, 200, comparison, threshold))));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(10, false)]
    public void HpThresholdsUseDecilesAndTheSpecialQuarterValue(int threshold, bool expected)
    {
        Assert.Equal(expected, Passed(Definition(Condition(0, 200, 0, threshold))));
    }

    [Fact]
    public void GroupHpPredicatesConsiderLivingPhysicalPartySlotsAndDoNotDrawRandomTargets()
    {
        BattleDefinition definition = Definition(Condition(1, 200, 0, 10));
        definition = definition with { Combatants = [definition.Combatants[0] with { MaximumHp = 101, CurrentHp = 25 }, definition.Combatants[1]] };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        battle.Tick();

        Assert.Equal(1u, battle.Capture().NativeAiVariables![0]);
        Assert.Equal(NativeBattleRandomState.Initialize(0).Indexes.ToArray(), battle.Capture().Random!.Indexes.ToArray());
        Assert.Equal(NativeBattleRandomState.Initialize(0).Lane, battle.Capture().Random!.Lane);
    }

    [Theory]
    [InlineData(7, 200, 7, true)]
    [InlineData(7, 0, 9, true)]
    [InlineData(8, 0, 0, true)]
    [InlineData(9, 0, 17, true)]
    [InlineData(14, 0, 0, true)]
    [InlineData(15, 0, 3, true)]
    [InlineData(15, 0, 4, false)]
    [InlineData(11, 255, 999, true)]
    [InlineData(12, 255, 999, true)]
    [InlineData(13, 255, 999, true)]
    public void OriginalIdentityAllocationAndNoOpConditionsUseTheirNativeOwners(byte subject, byte selector, int value, bool expected)
    {
        Assert.Equal(expected, Passed(Definition(Condition(subject, selector, 0, value))));
    }

    [Fact]
    public void PresenceIncludesDeadModelsWhileLivingPresenceExcludesThem()
    {
        BattleDefinition presence = Definition(Condition(8, 0, 0, 0));
        presence = presence with { Combatants = [presence.Combatants[0] with { CurrentHp = 0, NativeStatus0 = 1 }, presence.Combatants[1], new() { Id = "living-party", Name = "Living", IsPlayer = true, ModelId = 3 }] };
        BattleDefinition living = presence with { Combatants = [presence.Combatants[0], presence.Combatants[1] with { NativeAi = Ai(Condition(9, 0, 0, 0)) }, presence.Combatants[2]] };

        Assert.True(Passed(presence));
        Assert.False(Passed(living));
    }

    [Fact]
    public void ModelConditionSelectorsChooseTheFirstPhysicalMatchRatherThanActionTargetRules()
    {
        BattleDefinition definition = Definition(Condition(7, 201, 0, 7));
        definition = definition with { Combatants = [definition.Combatants[0], definition.Combatants[1] with { ModelId = 185 }] };

        Assert.True(Passed(definition));
    }

    [Theory]
    [InlineData(16, 202, true)]
    [InlineData(16, 201, false)]
    [InlineData(17, 0, false)]
    [InlineData(18, 0, true)]
    [InlineData(18, 1, false)]
    [InlineData(19, 0, true)]
    [InlineData(20, 7, true)]
    public void RemainingConditionsUseExplicitCurrentCharacteristicFlagsTimerAndStatusOwners(byte subject, int value, bool expected)
    {
        BattleDefinition definition = Definition(Condition(subject, 0, 0, value));
        definition = definition with { NativeSavedBattleFlags = 1, NativeSavedBattleFlagsKnownMask = 255, NativeExamTimerZero = true, Combatants = [definition.Combatants[0] with { NativeCharacterFlag100 = false, NativeRetainedStatusBits = 0x80 }, definition.Combatants[1]] };

        Assert.Equal(expected, Passed(definition));
    }

    [Fact]
    public void SavedFlagConditionRejectsUnownedBitsRatherThanAssumingTheyAreClear()
    {
        BattleDefinition definition = Definition(Condition(18, 0, 0, 3)) with { NativeSavedBattleFlags = 1, NativeSavedBattleFlagsKnownMask = 1 };

        Assert.Contains("explicit owner", Assert.Throws<NotSupportedException>(() => AdmitInitial(definition)).Message);
    }

    [Fact]
    public void TimerConditionUsesCurrentBattleCallbacksInsteadOfAStaleEntryValue()
    {
        NativeExamTimerState timer = NativeExamTimerState.FromColdProcess() with { TimerMemoryActivated = true, Alternatives = [new(NativeExamTimerState.Threshold - 1, NativeExamTimerState.Threshold - 1, 1)] };
        byte[] condition = Condition(19, 0, 0, 0);
        BattleDefinition definition = Definition(condition) with { NativeExamTimerAtEntry = timer };
        definition = definition with { Combatants = [definition.Combatants[0], definition.Combatants[1] with { InitialAtb = BattleSession.AtbMaximum, NativeAi = Ai(condition) with { Scripts = [condition, condition, [0], [0], [0]] } }] };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        Assert.Equal(0u, battle.Capture().NativeAiVariables![0]);

        battle.Tick();

        Assert.Equal(1u, battle.Capture().NativeAiVariables![0]);
        Assert.Equal(1, battle.Frame);
        Assert.Equal(1, timer.Alternatives.Single().Remaining);
    }

    [Theory]
    [InlineData(3, 254)]
    [InlineData(4, 65)]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(5, 1)]
    [InlineData(203, 200)]
    public void LastActionPredicatesReadPersistedNativeBytes(byte selector, int value)
    {
        NativeBattleAiDefinition ai = Ai([0]) with { Scripts = [[0], Condition(10, selector, 0, value), [0], [0], [0]] };
        BattleDefinition definition = Definition([0]);
        definition = definition with { Combatants = [definition.Combatants[0], definition.Combatants[1] with { NativeAi = ai, InitialAtb = BattleSession.AtbMaximum }] };
        BattleSession original = new(definition, BattleAtbMode.Normal);
        original.Tick();
        BattleSnapshot snapshot = original.Capture();
        int enemyIndex = Array.FindIndex(snapshot.Combatants, actor => actor.Id == "enemy");
        BattleCombatantSnapshot enemy = snapshot.Combatants[enemyIndex];
        enemy = enemy with { NativeParticipant = enemy.NativeParticipant! with { LastAction = new(1, 0, 254, 65, 2, 3) } };
        snapshot.Combatants[enemyIndex] = enemy;
        BattleSession restored = BattleSession.Restore(snapshot);

        restored.Tick();

        Assert.Equal(1u, restored.Capture().NativeAiVariables![0]);
        Assert.Equal(enemy.NativeParticipant.LastAction, restored.Capture().Combatants.Single(actor => actor.Id == "enemy").NativeParticipant!.LastAction);
    }

    [Fact]
    public void LastCommandWithoutAnExecutedMetadataOwnerFailsAtTheActualCondition()
    {
        Assert.Contains("executed action metadata owner", Assert.Throws<NotSupportedException>(() => AdmitInitial(Definition(Condition(10, 3, 0, 254)))).Message);
    }

    [Fact]
    public void ScriptedKoControlsLivingCountsWithoutInventingHpDamage()
    {
        byte[] initial = [39, 0, 1, .. Condition(6, 201, 0, 0)];
        BattleSession battle = new(Definition(initial), BattleAtbMode.Normal);
        battle.Tick();

        Assert.Equal(50, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.Equal(1u, battle.Capture().NativeAiVariables![0]);
    }

    [Fact]
    public void ClearingScriptedKoIsNotOverwrittenByAConditionQueryAtZeroHp()
    {
        byte[] initial = [39, 0, 1, 39, 0, 0, .. Condition(6, 201, 0, 1)];
        BattleDefinition definition = Definition(initial);
        definition = definition with { Combatants = [definition.Combatants[0], definition.Combatants[1] with { CurrentHp = 0, NativeStatus0 = 0 }] };

        Assert.True(Passed(definition));
    }

    private static bool Passed(BattleDefinition definition)
    {
        return AdmitInitial(definition).Capture().NativeAiVariables![0] == 1;
    }

    private static BattleSession AdmitInitial(BattleDefinition definition)
    {
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        battle.Tick();
        return battle;
    }

    private static byte[] Condition(byte subject, byte selector, byte comparison, int value)
    {
        return [2, subject, selector, comparison, unchecked((byte)value), unchecked((byte)(value >> 8)), 3, 0, 15, 96, 1, 0];
    }

    private static NativeBattleAiDefinition Ai(byte[] initial)
    {
        return new([initial, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
    }

    private static BattleDefinition Definition(byte[] initial)
    {
        return new()
        {
            Name = "Native condition component",
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, Level = 9 },
                new() { Id = "enemy", Name = "Enemy", ModelId = 1, Level = 7, MaximumHp = 500, CurrentHp = 50, NativeAi = Ai(initial) }
            ]
        };
    }
}
