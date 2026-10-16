namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDeathCountTests
{
    [Fact]
    public void RawKoStatusCommitsHpAndWrapsTheActualPlayerDeathWordOnce()
    {
        BattleSession battle = Create(ushort.MaxValue);
        Hit(battle);

        BattleCombatant ally = battle.Combatants.Single(actor => actor.Id == "ally");
        Assert.Equal(0, ally.CurrentHp);
        Assert.Equal((ushort)1, ally.PersistentStatus);
        Assert.Equal((ushort)0, ally.NativeDeathCount);
        Assert.Equal((ushort)0, BattleSession.Restore(battle.Capture()).Combatants.Single(actor => actor.Id == "ally").NativeDeathCount);
    }

    [Fact]
    public void OldUnknownDeathCounterRemainsUnknownAndFailsAtItsFirstActualKoProducer()
    {
        BattleSession battle = Create(null);
        Assert.Null(BattleSession.Restore(battle.Capture()).Combatants.Single(actor => actor.Id == "ally").NativeDeathCount);

        Assert.Contains("actual saved death counter", Assert.Throws<NotSupportedException>(() => Hit(battle)).Message);
    }

    private static void Hit(BattleSession battle)
    {
        battle.QueueAction("actor", "ally", BattleAbility.Magic, 1);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }
    }

    private static BattleSession Create(ushort? deathCount)
    {
        BattleNativeAction death = new(2, 1, 1, 1)
        {
            MagicId = 1, AttackType = 2, Power = 0, HitCount = 1, TargetFlags = 0x10, AttackFlags = 3, DamagePresentation = 0, Element = 0, StatusAttack = 255, Statuses0 = 1, Statuses1 = 0, ElementPercent = null, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        return new(new BattleDefinition
        {
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Native KO component", BattleActionEffect.Native, 0, true) { NativeAction = death }],
            Combatants =
            [
                new() { Id = "actor", Name = "Actor", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, MagicStocks = [new(1, 1)], Abilities = [BattleAbility.Attack, BattleAbility.Magic], NativeDeathCount = 0 },
                new() { Id = "ally", Name = "Ally", ModelId = 1, IsPlayer = true, NativeStatus0 = 0x0A, NativeDeathCount = deathCount },
                new() { Id = "enemy", Name = "Enemy", Speed = 1 }
            ]
        }, BattleAtbMode.Normal);
    }
}
