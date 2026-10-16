using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private uint? ReadNativeElapsedCounter()
    {
        NativeScalarValue value = Events.EventMemory.Read(4, 4);
        return value.IsExact ? unchecked((uint)value.Values[0]) : null;
    }

    private static BattleNativeGrowthChange[] CaptureNativeGrowthChanges(BattleSession active, BattleSnapshot completed)
    {
        List<BattleNativeGrowthChange> changes = [];
        foreach (BattleCombatant actor in active.Combatants.Where(actor => actor.IsPlayer))
        {
            BattleNativeCharacterGrowth? before = completed.Definition.Combatants.Single(source => source.Id == actor.Id).NativeGrowth;
            if (before is null || actor.NativeGrowth is not { } after || before.HitPointBonus == after.HitPointBonus && before.StatBonuses.AsSpan().SequenceEqual(after.StatBonuses.AsSpan()))
            {
                continue;
            }
            byte row = byte.Parse(actor.Id.AsSpan("party-".Length), System.Globalization.CultureInfo.InvariantCulture);
            changes.Add(new(row, before, after));
        }
        return changes.ToArray();
    }
}
