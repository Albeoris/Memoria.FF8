using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private static BattleNativeDeathCountChange[] CaptureNativeDeathCounts(BattleSession battle, BattleSnapshot snapshot)
    {
        List<BattleNativeDeathCountChange> changes = [];
        foreach (BattleCombatant actor in battle.Combatants.Where(actor => actor.IsPlayer))
        {
            ushort? before = snapshot.Definition.Combatants.Single(source => source.Id == actor.Id).NativeDeathCount;
            if (before == actor.NativeDeathCount)
            {
                continue;
            }
            if (before is not ushort initial || actor.NativeDeathCount is not ushort after)
            {
                throw new InvalidDataException("Native party death counter lost its exact battle entry or exit WORD.");
            }
            byte row = byte.Parse(actor.Id.AsSpan("party-".Length), System.Globalization.CultureInfo.InvariantCulture);
            changes.Add(new(row, initial, after));
        }
        return changes.ToArray();
    }
}
