using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class BattleRewardTests
{
    [Fact]
    public void ScriptItemsRetainOrderedPairsIncludingZeroAndRefuseUnmigratedRewardSnapshots()
    {
        BattleSession Create(byte[] init)
        {
            NativeBattleAiDefinition ai = new([init, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
            BattleSession battle = new(new() { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "party-0", Name = "Party", IsPlayer = true }, new() { Id = "enemy-0", Name = "Enemy", NativeAi = ai }] }, BattleAtbMode.Normal);
            battle.Tick();
            return battle;
        }
        BattleSession battle = Create([56, 1, 56, 0, 56, 1, 0]);
        Assert.Equal(new BattleStock[] { new(1, 1), new(0, 1), new(1, 1) }, battle.NativeRewardItems);
        BattleSnapshot snapshot = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Equal(battle.NativeRewardItems, restored.NativeRewardItems);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { NativeRewardItems = null }));
        Assert.Throws<NotSupportedException>(() => Create(Enumerable.Repeat(new byte[] { 56, 1 }, 25).SelectMany(pair => pair).Append((byte)0).ToArray()));
    }

    [Theory]
    [InlineData(BattleAbility.Attack, BattleDefeatCredit.Character, null)]
    [InlineData(BattleAbility.GuardianForce, BattleDefeatCredit.GuardianForce, 1)]
    public void LiveDeathCapturesTypedCreditAndChosenRewardsIndependentlyOfEventLog(BattleAbility ability, BattleDefeatCredit expected, int? gf)
    {
        BattleEnemyRewardDefinition rewards = new(10, 20, 20, 30, 0, true, 255, Enumerable.Range(0, 12).Select(index => new BattleStock(index + 1, 1)).ToImmutableArray());
        BattleDefinition definition = new()
        {
            GuardianForces = [new(1, 100, 100) { KillCount = ushort.MaxValue, DeathCount = 4 }],
            Combatants =
            [
                new() { Id = "party-0", Name = "Owner", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Level = 8, Attack = 100, Abilities = [BattleAbility.Attack, BattleAbility.GuardianForce], EquipmentIds = [1] },
                new() { Id = "enemy-2", Name = "Target", MaximumHp = 1, CurrentHp = 1, Speed = 1, Level = 8, RewardSlot = 2, Rewards = rewards }
            ],
            ActionDefinitions = [new(BattleAbility.GuardianForce, 1, "Shiva", BattleActionEffect.Damage, 100, false, Level: 1)]
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        session.QueueAction("party-0", "enemy-2", ability, gf ?? 0);
        for (int frame = 0; frame < 100 && session.Outcome == BattleOutcome.Active; frame++)
        {
            session.Tick();
        }
        Assert.Equal(BattleOutcome.Victory, session.Outcome);
        BattleEnemyDefeat fact = Assert.Single(session.EnemyDefeats);
        Assert.Equal((2, (byte)8, expected, (byte?)0, gf is int id ? (byte?)id : null), (fact.Slot, fact.Level, fact.Credit, fact.KillerRow, fact.GuardianForceId));
        if (gf is not null)
        {
            Assert.Equal((ushort)0, Assert.Single(session.GuardianForces).KillCount);
            Assert.Equal((ushort)65535, Assert.Single(session.GuardianForceCounterChanges).KillsBefore);
        }
        else
        {
            Assert.Empty(session.GuardianForceCounterChanges);
        }
        Assert.True(fact.CardSelected);
        Assert.InRange(fact.DropSelection, (sbyte)0, (sbyte)3);
        Assert.Equal(rewards.Drop(8, fact.DropSelection), Assert.Single(session.NativeRewardItems));
        BattleSnapshot snapshot = session.Capture() with { Events = [] };
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Equal(fact, Assert.Single(restored.EnemyDefeats));
        Assert.Empty(restored.Events);
    }

    [Theory]
    [InlineData(23, true)]
    [InlineData(24, false)]
    public void ScriptAndDeathDropsShareTheOriginalTwentyFourPairCapacity(int scriptedCount, bool expectDrop)
    {
        byte[] init = Enumerable.Repeat(new byte[] { 56, 1 }, scriptedCount).SelectMany(pair => pair).Append((byte)0).ToArray();
        NativeBattleAiDefinition ai = new([init, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleEnemyRewardDefinition rewards = new(0, 0, 20, 30, 255, false, 255, Enumerable.Repeat(new BattleStock(7, 1), 12).ToImmutableArray());
        BattleSession battle = new(new() { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "party-0", Name = "Party", IsPlayer = true, Attack = 100, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy-0", Name = "Enemy", MaximumHp = 1, CurrentHp = 1, Level = 8, RewardSlot = 0, Rewards = rewards, NativeAi = ai }] }, BattleAtbMode.TurnBased);
        battle.Tick();
        battle.QueueAttack("party-0", "enemy-0");
        for (int frame = 0; frame < 100 && battle.Outcome == BattleOutcome.Active; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(expectDrop, Assert.Single(battle.EnemyDefeats).DropSelection >= 0);
        Assert.Equal(24, battle.NativeRewardItems.Count);
    }

    [Theory]
    [InlineData(177, false, 0)]
    [InlineData(178, false, 1)]
    [InlineData(228, false, 1)]
    [InlineData(229, false, 2)]
    [InlineData(243, false, 2)]
    [InlineData(244, false, 3)]
    [InlineData(127, true, 0)]
    [InlineData(128, true, 1)]
    [InlineData(241, true, 1)]
    [InlineData(242, true, 2)]
    [InlineData(255, true, 2)]
    public void NativeDropRarityUsesOriginalUnsignedByteThresholds(byte sample, bool rare, int expected)
    {
        Assert.Equal(expected, BattleEnemyRewardDefinition.SelectDropSlot(sample, rare));
    }

    [Fact]
    public void CardAndItemChanceAreInclusiveAndOriginalFormationFlagsControlNoExperience()
    {
        BattleEnemyRewardDefinition rewards = new(0, 0, 20, 30, 1, false, 100, Enumerable.Repeat(new BattleStock(1, 1), 12).ToImmutableArray());
        Assert.True(rewards.SelectCard(8));
        Assert.False(rewards.SelectCard(9));
        Assert.True(rewards.SelectDrop(100));
        Assert.False(rewards.SelectDrop(101));
        Assert.False(NativeBattleRewardDistribution.NoExperience(0, 0));
        Assert.True(NativeBattleRewardDistribution.NoExperience(8, 0));
        Assert.True(NativeBattleRewardDistribution.NoExperience(0, 8));
    }
}
