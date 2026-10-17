using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDeadAtbRestoreTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 0)]
    [InlineData(101, 0)]
    [InlineData(100, -1)]
    [InlineData(100, BattleSession.AtbMaximum + 1)]
    public void ComponentRestoreStillRejectsUnownedDeadAtbAndInvalidVitals(int hp, int atb)
    {
        BattleSnapshot original = Create().Capture();
        BattleCombatantSnapshot[] actors = original.Combatants.ToArray();
        actors[1] = actors[1] with { CurrentHp = hp, Atb = atb };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(original with { Combatants = actors }));
    }

    [Fact]
    public void ComponentDeadZeroAtbStillRoundTrips()
    {
        BattleSnapshot original = Create().Capture();
        BattleCombatantSnapshot[] actors = original.Combatants.ToArray();
        actors[1] = actors[1] with { CurrentHp = 0, Atb = 0, NativeStatus = actors[1].NativeStatus! with { Saved = 1 } };
        BattleSnapshot dead = original with { Combatants = actors };
        Assert.Equal(JsonSerializer.Serialize(dead), JsonSerializer.Serialize(BattleSession.Restore(dead).Capture()));
    }

    private static BattleSession Create()
    {
        return new(new() { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }, new() { Id = "surviving-enemy", Name = "Surviving enemy" }] }, BattleAtbMode.TurnBased);
    }
}
