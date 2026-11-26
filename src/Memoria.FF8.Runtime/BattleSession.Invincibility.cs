namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private const uint InvincibilityStatus = 0x800;

    private bool LimitsInvincibility => definition.InvincibilityOncePerBattle || Difficulty == BattleDifficulty.Hardcore;

    private bool HasUsedInvincibility(BattleCombatant target) => LimitsInvincibility && target.ReceivedInvincibility;

    public bool CanReceiveInvincibility(BattleCombatant target, BattleNativeAction action)
    {
        uint statuses = NativeBattleStatusBits(target);
        return target is { IsPlayer: true, IsAlive: true, Untargetable: false, IsEjected: false } &&
            (statuses & InvincibilityStatus) == 0 && !HasUsedInvincibility(target) &&
            !NativeMagicContactBlocked(action, NativeSavedStatusBits(target), statuses) && !NativeActionReflects(target, action);
    }

    private static bool GrantsInvincibility(BattleNativeAction? action) => action is { AttackType: 2 } && (action.AppliedBattleStatuses & InvincibilityStatus) != 0;

    public static bool IsInvincibilityItem(BattleActionDefinition action) => action.Ability == BattleAbility.Item && GrantsInvincibility(action.NativeAction);

    private BattleNativeAction? FindInvincibilityItem(BattleAbility ability, int actionId)
    {
        return ability == BattleAbility.Item && GetActionDefinition(ability, actionId) is { } item && IsInvincibilityItem(item) ? item.NativeAction : null;
    }

    public bool CanUseInvincibilityItem(BattleCombatant actor, BattleActionDefinition item, BattleCombatant selected)
    {
        if (item.NativeAction is not { } native || !IsInvincibilityItem(item))
        {
            return false;
        }
        BattleTargetRequest request = BuildActionTargetRequest(actor, selected, item);
        return (request.Selection is not BattleTargetSelection.Explicit || IsValidSelectedActionTarget(actor, item, selected)) &&
            InvincibilityItemTargets(request, native).Length != 0;
    }

    private BattleCombatant[] InvincibilityItemTargets(BattleTargetRequest request, BattleNativeAction item)
    {
        if (request.Replacement != BattleTargetReplacement.None)
        {
            request = new(request.Selection, request.Eligibility, request.AllowsKnockedOutAllies, BattleTargetReplacement.None, request.DeathCallbackSelf) { OriginalProjection = request.OriginalProjection };
        }
        return ResolveBattleTargets(request, sampleRandom: false).Where(target => CanReceiveInvincibility(target, item)).ToArray();
    }

    private bool IsUnavailableInvincibilityFinisher(BattleNativeAction action)
    {
        return LimitsInvincibility && GrantsInvincibility(action) && !allies.Any(target => CanReceiveInvincibility(target, action));
    }
}
