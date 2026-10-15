using System.Numerics;

namespace Memoria.FF8;

internal static class BattleFormationPresentation
{
    // Original47DBA0/B81030 party table, converted with the same(x,-y,-z)/100 basis as scene.out.
    // Slots follow the canonical combat party order.
    public static Vector3 PartyPosition(int slot, int count)
    {
        if (count is < 1 or > 3 || slot < 0 || slot >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        return count switch
        {
            1 => new Vector3(0, 0, -18),
            2 => new Vector3(slot == 0 ? -10 : 10, 0, -18),
            _ => new Vector3((slot - 1) * 12, 0, slot == 1 ? -15 : -21)
        };
    }

    public static Vector3 DebugEnemyPosition(int slot, int count)
    {
        // TODO(TD-009): Arbitrary laboratory opponents have no scene.out formation; use a separate opposing row until editable native formations are supported
        int rowCount = Math.Min(4, count - slot / 4 * 4);
        return new Vector3((slot % 4 - (rowCount - 1) / 2f) * 12, 0, 12 + slot / 4 * 12);
    }
}
