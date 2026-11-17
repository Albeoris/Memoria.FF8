using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private static BattleNativeDeathCountChange[] CaptureNativeDeathCounts(BattleSession battle, BattleSnapshot snapshot)
    {
        List<BattleNativeDeathCountChange> changes = [];
        foreach (BattleCombatant actor in battle.Combatants.Where(actor => actor.IsPlayer))
        {
            byte row = checked((byte)(actor.MagicRecordId ?? throw new InvalidDataException("Native party death count requires its admitted row.")));
            ushort? before = ReadAdmittedPartyMember(snapshot.Definition, row).NativeDeathCount;
            if (before == actor.NativeDeathCount)
            {
                continue;
            }
            if (before is not ushort initial || actor.NativeDeathCount is not ushort after)
            {
                throw new InvalidDataException("Native party death counter lost its exact battle entry or exit WORD.");
            }
            changes.Add(new(row, initial, after));
        }
        return changes.ToArray();
    }
}
