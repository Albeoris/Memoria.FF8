using System.Collections.Immutable;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class CrossoverStatusStatProjection
{
    public static Ff7BattleDamageActor Apply(ImmutableArray<StatusStatModifier> modifiers, Ff7BattleDamageActor actor)
    {
        return actor with
        {
            Attack = checked((byte)Apply(BattleStatId.Strength, actor.Attack)),
            Defense = checked((ushort)Apply(BattleStatId.Vitality, actor.Defense)),
            MagicDefense = checked((ushort)Apply(BattleStatId.Spirit, actor.MagicDefense))
        };

        int Apply(BattleStatId stat, int value)
        {
            return StatusStatRules.Apply(modifiers, stat, value, actor.OriginSavedStatuses, actor.OriginBattleStatuses);
        }
    }
}
