using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class BattleFormationPresentation
{
    // Original47DBA0/B81030 party table, converted with the same(x,-y,-z)/100 basis as scene.out.
    // Slots follow the canonical combat party order.
    public static Vector3 PartyPosition(int slot, int count)
    {
        NativeBattleVector position = count <= 3 ? NativeBattleFormation.PartyPosition(slot, count) : ExpandedBattleFormation.Position(slot, count, isAlly: true);
        return new Vector3(position.X, -position.Y, -position.Z) / 100f;
    }

    public static Vector3 DebugEnemyPosition(int slot, int count)
    {
        // TODO(TD-009): Arbitrary laboratory opponents have no scene.out formation; use a separate opposing row until editable native formations are supported.
        if (count >= 5)
        {
            NativeBattleVector position = ExpandedBattleFormation.Position(slot, count, isAlly: false);
            return new Vector3(position.X, -position.Y, -position.Z) / 100f;
        }
        int rowCount = Math.Min(4, count);
        return new Vector3((slot % 4 - (rowCount - 1) / 2f) * 12, 0, 12 + slot / 4 * 12);
    }
}
