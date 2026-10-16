namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleReactionAdmissionTests
{
    [Theory]
    [InlineData(0, 0, 0, true)]
    [InlineData(4, 0, 0, false)]
    [InlineData(0x20, 0, 0, false)]
    [InlineData(0, 1, 0, false)]
    [InlineData(0, 8, 0, false)]
    [InlineData(0, 0, 0x4000, false)]
    [InlineData(2, 0, 0, true)]
    [InlineData(0, 0x4000, 0, true)]
    [InlineData(0, 0x80000, 0, true)]
    public void DeferredEnemyReactionUsesTheOriginalSavedBattleAndActorFlagGates(ushort saved, uint battleStatus, uint actorFlags, bool admitted)
    {
        NativeBattleAiDefinition ai = new([[0], [0], [15, 96, 1, 0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession session = new(new()
        {
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Speed = 1 },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 10000, CurrentHp = 10000, NativeAi = ai, NativeStatus0 = saved, NativeStatus1 = battleStatus, NativeCombatantFlags = actorFlags, Speed = 1 },
                new() { Id = "other", Name = "Other", MaximumHp = 10000, CurrentHp = 10000, Speed = 1 }
            ]
        }, BattleAtbMode.Normal);
        session.QueueAttack("party", "enemy");
        for (int frame = 0; frame < 20; frame++)
        {
            session.Tick();
        }
        Assert.Equal(admitted ? 1u : 0u, session.Capture().NativeAiVariables![0]);
    }
}
