using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class LiveMenuCatalog
{
    public Dictionary<string, int> ItemRecoveryTargetScores(GameState state, int itemId)
    {
        Dictionary<string, int> result = new(StringComparer.Ordinal);
        if (state.Scene is not (SceneKind.Field or SceneKind.WorldMap) || state.CharacterVitals is not { } vitals || FieldItem(itemId) is not { } item)
        {
            return result;
        }
        if (item.Type is 1 or 2 or 4 or 5 && ((state.InputConfiguration?.CommandRestrictions ?? 0) & 0x40) != 0)
        {
            return result;
        }
        if (item.SupportsGuardianForceUse && item.Type is 4 or 5)
        {
            for (int id = 0; id < vitals.GuardianForces.Length; id++)
            {
                NativeGuardianForceVital force = vitals.GuardianForces[id];
                int restoredHp = item.Type == 4 ? vitals.Definitions.GuardianForceMaximum(id, force) / 8 : vitals.Definitions.GuardianForceMaximum(id, force);
                if ((force.Flags & 1) != 0 && force.HitPoints == 0 && restoredHp > 0)
                {
                    result[$"item-target-{id + 16}"] = StatusRecoveryTargeting.Score(1, 0);
                }
            }
        }
        if (!item.SupportsCharacterUse || item.Type is not (0 or 1 or 2 or 21))
        {
            return result;
        }
        for (int row = 0; row < Math.Min(8, vitals.Records.Length); row++)
        {
            NativeCharacterVital target = vitals.Records[row];
            if ((target.MemberFlags & 1) == 0 || vitals.DreamEnabled && !vitals.CombatParty.Contains((byte)row) || target.HitPoints.Minimum != target.HitPoints.Maximum)
            {
                continue;
            }
            uint removed = 0;
            if (item.Type is 1 or 2)
            {
                int restoredHp = item.Type == 1 ? vitals.GetMagicMaximumHitPoints(row) / 8 : vitals.GetMagicMaximumHitPoints(row);
                removed = (target.Status & 1) != 0 && restoredHp > 0 ? 1u : 0u;
            }
            else if (target.HitPoints.Minimum > 0 && (target.Status & 1) == 0 && !(item.Type == 0 && (target.Status & 0x40) != 0 && (item.StatusMask & 0x40) == 0))
            {
                removed = (uint)(target.Status & item.StatusMask & 0x7E);
            }
            result[$"character-{row}"] = StatusRecoveryTargeting.Score(removed, 0);
        }
        return result;
    }

    public Dictionary<string, int> MagicRecoveryTargetScores(GameState state, int magicId)
    {
        Dictionary<string, int> result = new(StringComparer.Ordinal);
        if (state.Scene is not (SceneKind.Field or SceneKind.WorldMap) || state.CharacterVitals is not { } vitals || !IsFieldMagic(magicId))
        {
            return result;
        }
        var spell = kernel.MagicSpells[magicId];
        for (int row = 0; row < vitals.Records.Length; row++)
        {
            NativeCharacterVital target = vitals.Records[row];
            if (target.HitPoints.Minimum != target.HitPoints.Maximum)
            {
                continue;
            }
            uint removed;
            if (spell.AttackType is 5 or 6)
            {
                int restoredHp = spell.AttackType == 5 ? vitals.GetMagicMaximumHitPoints(row) / 8 : vitals.GetMagicMaximumHitPoints(row);
                removed = (target.Status & 1) != 0 && restoredHp > 0 ? 1u : 0u;
            }
            else
            {
                removed = target.HitPoints.Minimum > 0 && (target.Status & 1) == 0 ? (uint)(target.Status & spell.StatusMagic5 & 0x7E) : 0;
            }
            result[$"character-{row}"] = StatusRecoveryTargeting.Score(removed, 0);
        }
        return result;
    }
}
