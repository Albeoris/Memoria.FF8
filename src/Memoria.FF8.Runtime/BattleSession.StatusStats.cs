namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public int ReadEffectiveStat(string actorId, BattleStatId stat)
    {
        return EffectiveStat(GetActor(actorId), stat);
    }

    private int EffectiveStat(BattleCombatant actor, BattleStatId stat)
    {
        return EffectiveStat(actor, stat, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor));
    }

    private int EffectiveStat(BattleCombatant actor, BattleStatId stat, ushort saved, uint battle)
    {
        int baseValue = stat switch
        {
            BattleStatId.Strength => actor.Attack,
            BattleStatId.Vitality => actor.Defense,
            BattleStatId.Spirit => actor.Spirit,
            _ => throw new ArgumentOutOfRangeException(nameof(stat))
        };
        return StatusStatRules.Apply(definition.StatusStatModifiers, stat, baseValue, saved, battle);
    }
}
