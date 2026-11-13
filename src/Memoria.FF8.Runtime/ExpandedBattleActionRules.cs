namespace Memoria.FF8.Runtime;

internal static class ExpandedBattleActionRules
{
    public static bool SupportsContacts(BattleNativeAction action)
    {
        return action.HitCount > 0 && !action.PresentationOnly && action.CrisisLevel is null &&
            action.AttackType is 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 11 or 15 or 18 or 20 or 21 or 22 or 25 or 26 or 27 or 28 or 29 or 30 or 32 or 33 or 34 or 35 or 36 &&
            (action.Statuses1 & 0x10000) == 0;
    }

    public static bool SupportsEnemyContacts(BattleNativeAction action)
    {
        return SupportsContacts(action) || action is { Command: 8, AttackType: 0, Power: 0, Statuses0: 0, Statuses1: 0, AttackFlags: 3, HitCount: 1 };
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
