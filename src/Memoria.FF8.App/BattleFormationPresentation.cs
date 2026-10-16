using System.Numerics;

namespace Memoria.FF8;

internal static class BattleFormationPresentation
{
    // Original47DBA0/B81030 party table, converted with the same(x,-y,-z)/100 basis as scene.out.
    // Slots follow the canonical combat party order.
    public static Vector3 PartyPosition(int slot, int count)
    {
        Memoria.FF8.Runtime.NativeBattleVector position = Memoria.FF8.Runtime.NativeBattleFormation.PartyPosition(slot, count);
        return new Vector3(position.X, -position.Y, -position.Z) / 100f;
    }

    public static Vector3 DebugEnemyPosition(int slot, int count)
    {
        // TODO(TD-009): Arbitrary laboratory opponents have no scene.out formation; use a separate opposing row until editable native formations are supported.
        int rowCount = Math.Min(4, count - slot / 4 * 4);
        return new Vector3((slot % 4 - (rowCount - 1) / 2f) * 12, 0, 12 + slot / 4 * 12);
    }
}
