namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePersistentStatusTests
{
    [Fact]
    public void BattleExitPreservesTheCompleteStatusWordExceptBerserk()
    {
        BattleSession battle = new(new BattleDefinition
        {
            Name = "Native persistent status component",
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 100, CurrentHp = 20, NativeStatus0 = 0xF8FE },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.Normal);

        Assert.Equal((ushort)0xFBDE, battle.Combatants.Single(actor => actor.IsPlayer).PersistentStatus);
        Assert.Equal((ushort)0xFBDE, BattleSession.Restore(battle.Capture()).Combatants.Single(actor => actor.IsPlayer).PersistentStatus);
    }

    [Theory]
    [InlineData(BattleOutcome.Defeat, 4, true)]
    [InlineData(BattleOutcome.Defeat, 0, false)]
    [InlineData(BattleOutcome.Victory, 4, false)]
    [InlineData(BattleOutcome.Victory, 0xF8DA, true)]
    public void FactualOutcomesCountPetrifiedSurvivorsAsUnableToBattle(BattleOutcome outcome, ushort status, bool valid)
    {
        BattleEncounterOccurrence encounter = new("native-status-component", 512, new string('A', 64), SceneKind.Field, 74, 0, new string('B', 64), null);
        BattleResult result = new(encounter, outcome, [new(0, 0, 100, 20, 0xF8DE, status, [])], [], [], [], true, BattleRewardPolicy.DebugNoRewards, []);
        if (outcome == BattleOutcome.Defeat)
        {
            result = result with { DebugVictory = false, RewardPolicy = BattleRewardPolicy.NativeCombat };
        }
        if (valid)
        {
            result.Validate();
        }
        else
        {
            Assert.Throws<InvalidDataException>(result.Validate);
        }
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 8)]
    public void ReadinessUsesNativePetrifySleepAndStopGates(ushort saved, uint transient)
    {
        BattleSession battle = new(new BattleDefinition
        {
            Name = "Native readiness component",
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, NativeStatus0 = saved, NativeStatus1 = transient },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.Normal);

        Assert.False(battle.Combatants.Single(actor => actor.IsPlayer).CanAct);
        Assert.False(battle.Combatants.Single(actor => actor.IsPlayer).IsReady);
    }
}
