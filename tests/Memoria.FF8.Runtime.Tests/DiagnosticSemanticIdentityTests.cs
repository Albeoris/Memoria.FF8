using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticSemanticIdentityTests
{
    [Theory]
    [InlineData(true, BattleAbility.LimitBreak, 11, 241, BattleAbility.LimitBreak)]
    [InlineData(true, BattleAbility.Item, null, 244, BattleAbility.Item)]
    [InlineData(true, null, 5, 250, BattleAbility.LimitBreak)]
    [InlineData(true, null, 15, 250, BattleAbility.NativeCommand)]
    [InlineData(true, null, null, 13, BattleAbility.Item)]
    [InlineData(true, null, null, 99, BattleAbility.NativeCommand)]
    [InlineData(true, BattleAbility.EnemyAbility, null, 2, BattleAbility.Magic)]
    [InlineData(false, BattleAbility.Magic, null, 2, BattleAbility.EnemyAbility)]
    public void Header_observation_preserves_bound_semantics_and_requires_a_proved_fallback(bool isPlayer, BattleAbility? known, int? owner, int command, BattleAbility expected)
    {
        MethodInfo method = typeof(BattleSession).GetMethod("ResolveDiagnosticHeaderAbility", BindingFlags.Static | BindingFlags.NonPublic)!;
        BattleAbility actual = (BattleAbility)method.Invoke(null, [isPlayer, known, owner is int value ? (byte?)checked((byte)value) : null, checked((byte)command)])!;
        Assert.Equal(expected, actual);
    }
}
