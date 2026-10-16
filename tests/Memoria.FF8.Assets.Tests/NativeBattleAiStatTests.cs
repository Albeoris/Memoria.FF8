using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleAiStatTests
{
    [Fact]
    public void OriginalProjectionRetainsSignedStatisticsUntilTheMultiplierIsApplied()
    {
        BattleEnemyNativeStats stats = new([0, 1, 0, 0], Enumerable.Repeat(new byte[] { 0, 1, 0, 1 }, 6).SelectMany(row => row).ToImmutableArray(), [800, 800, 800, 800, 800, 800, 800, 800]);

        BattleEnemyNativeProjection projection = stats.Project(10);

        Assert.Equal(-10, projection.BaseStatistics[0]);
        Assert.Equal(246, projection.Strength);
    }

    [Fact]
    public void Opcode40AssignsAllSixNativeMultipliersBeforeTheFinalByteProjection()
    {
        BattleSession battle = new(Definition([40, 0, 20, 40, 1, 0, 40, 2, 255, 40, 3, 5, 40, 4, 20, 40, 5, 5, 0]), BattleAtbMode.Normal);
        BattleCombatant enemy = battle.Combatants.Single(actor => !actor.IsPlayer);

        Assert.Equal(new[] { 40, 0, 255, 25, 120, 255 }, new[] { enemy.Attack, enemy.Defense, enemy.Magic, enemy.Spirit, enemy.Speed, enemy.Evasion });
        BattleSnapshot snapshot = battle.Capture();
        Assert.Equal(new byte[] { 20, 0, 255, 5, 20, 5 }, snapshot.Combatants.Single(actor => actor.Id == "enemy").NativeParticipant!.StatMultipliers);
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(restored.Capture()));
        BattleCombatant restoredEnemy = restored.Combatants.Single(actor => !actor.IsPlayer);
        Assert.Equal(enemy.Attack, restoredEnemy.Attack);
        Assert.Equal(enemy.Evasion, restoredEnemy.Evasion);
    }

    [Fact]
    public void RepeatedAssignmentsScaleTheOriginalStatisticRatherThanThePreviousResult()
    {
        BattleSession battle = new(Definition([40, 0, 20, 40, 0, 5, 0]), BattleAtbMode.Normal);

        Assert.Equal(10, battle.Combatants.Single(actor => !actor.IsPlayer).Attack);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(255)]
    public void Opcode40RejectsAnIndexOutsideTheSixOriginalStatisticRows(byte index)
    {
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => new BattleSession(Definition([40, index, 10, 0]), BattleAtbMode.Normal));

        Assert.Contains("six enemy statistic rows", exception.Message);
    }

    [Fact]
    public void Opcode40RequiresOriginalStatisticsInsteadOfScalingAnAlreadyWrappedByte()
    {
        BattleDefinition original = Definition([40, 5, 5, 0]);
        BattleDefinition missing = original with { Combatants = original.Combatants.Select(actor => actor with { NativeTemplate = null }).ToArray() };
        Assert.Contains("original six pre-byte", Assert.Throws<NotSupportedException>(() => new BattleSession(missing, BattleAtbMode.Normal)).Message);
    }

    [Fact]
    public void RestoringRejectsAChangedMultiplierWithMissingOriginalStatisticInputs()
    {
        BattleDefinition definition = Definition([0]);
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        BattleSnapshot snapshot = battle.Capture();
        snapshot.Combatants.Single(actor => actor.Id == "enemy").NativeParticipant!.StatMultipliers[0] = 20;
        BattleCombatantDefinition[] actors = snapshot.Definition.Combatants.Select(actor => actor.NativeTemplate is null ? actor : actor with { NativeTemplate = actor.NativeTemplate with { Levels = actor.NativeTemplate.Levels.Select(level => level with { BaseStatistics = [] }).ToArray() } }).ToArray();

        Assert.Throws<NotSupportedException>(() => BattleSession.Restore(snapshot with { Definition = snapshot.Definition with { Combatants = actors } }));
    }

    private static BattleDefinition Definition(byte[] initialScript)
    {
        NativeBattleEnemyLevel level = new(100, 20, 30, 40, 50, 60, [800, 800, 800, 800, 800, 800, 800, 800], [], [], 0) { Evasion = 253, BaseStatistics = [20, 30, 40, 50, 60, -3] };
        NativeBattleAiDefinition ai = new([initialScript, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        return new()
        {
            Name = "Native statistic multiplier component",
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy", Level = 7, Attack = 20, Defense = 30, Magic = 40, Spirit = 50, Speed = 60, Evasion = 253, NativeAi = ai, NativeTemplate = new(7, Enumerable.Repeat(level, 101).ToArray()) }
            ]
        };
    }
}
