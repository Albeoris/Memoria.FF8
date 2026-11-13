namespace Memoria.FF8.Runtime;

internal static class ExpandedBattleActionRules
{
    public static bool SupportsContacts(BattleNativeAction action)
    {
        return action.HitCount > 0 && !action.PresentationOnly && action.CrisisLevel is null &&
            (action.AttackType is 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 11 or 15 or 18 or 20 or 21 or 22 or 25 or 26 or 27 or 28 or 29 or 30 or 32 or 33 or 34 or 35 or 36 ||
             action is { Command: 2 or 6, KernelSection: 1, AttackType: 12, HitCount: 1, Power: 0, Element: 0, StatusAttack: 0, Statuses0: 0, Statuses1: 0 }) &&
            (action.Statuses1 & 0x10000) == 0;
    }

    public static bool SupportsEnemyContacts(BattleNativeAction action)
    {
        return SupportsContacts(action) || action is { Command: 8, AttackType: 0, Power: 0, Statuses0: 0, Statuses1: 0, AttackFlags: 3, HitCount: 1 } ||
            action is { Command: 8, KernelSection: 3, AttackType: 0, Power: 0, Element: 0, StatusAttack: 0, Statuses0: 0, Statuses1: 0, AttackFlags: 0 or 1, HitCount: 1, PresentationOnly: false, CrisisLevel: null };
    }

    public static bool SupportsDrawnEnemyMagic(BattleNativeAction action)
    {
        return action is { Command: 2, KernelSection: 1, ActionId: > 0, TargetFlags: byte flags } && (flags & 0x30) != 0x30 &&
            (SupportsContacts(action) || SupportsEnemyMagicCure(action) ||
             action is { AttackType: 2, Power: 0, HitCount: 1, AttackFlags: 0x23, Statuses0: 0, Statuses1: 0x10000, StatusAttack: 254 });
    }

    public static bool SupportsEnemyMagicCure(BattleNativeAction action)
    {
        // Esuna and Full-cure erase Eject along with other statuses. They never apply Eject.
        return action is { KernelSection: 1, ActionId: 27, AttackType: 3, HitCount: 1, Statuses1: 0x100520D } or
            { KernelSection: 1, ActionId: 51, AttackType: 32, HitCount: 1, Statuses1: 0x100560D };
    }

    public static bool SupportsPlayerAction(BattleAbility ability, BattleNativeAction action)
    {
        return (ability, action.Command) is (BattleAbility.Magic, 2) or (BattleAbility.Item, 4) &&
            action.TargetFlags is byte flags && (flags & 0x30) != 0x30 && ((flags & 1) == 0 || action.AttackType is 5 or 6) && SupportsContacts(action);
    }

    public static bool HasNativeSpellPresentation(BattleNativeAction action, int recipientCount)
    {
        return action.HitCount == 1 && (action.MagicId == 38 || recipientCount < NativeBattleSlots.Count &&
            (action.ActionId is >= 1 and <= 9 or >= 21 and <= 27 || action.MagicId == 39));
    }
}
