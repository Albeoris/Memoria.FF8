namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static bool IsExpandedAutomaticGuardian(BattleNativeAction? action, BattleNativePlayerReaction? reaction)
    {
        return reaction == BattleNativePlayerReaction.AutomaticGuardian && IsExpandedAutomaticGuardianAction(action);
    }

    private static bool IsExpandedAutomaticGuardianAction(BattleNativeAction? action)
    {
        return action is { Command: 245, KernelSection: 9, ActionId: 0 or 7 or 8 or 9 or 10, AttackType: 11 or 35, HitCount: 1, TargetFlags: byte flags } &&
            (flags & 0x72) == 0x40;
    }

    private BattleCombatant[] ExpandedAutomaticGuardianTargets()
    {
        return enemies.Where(actor => actor.IsActive && actor.IsAlive && !actor.Untargetable).ToArray();
    }

    private void QueueExpandedAutomaticGuardian(byte id)
    {
        BattleNativeAction action = FindInstalledNativeAction(245, id);
        if (!IsExpandedAutomaticGuardianAction(action))
        {
            throw new NotSupportedException("The automatic guardian lacks an expanded contact contract.");
        }
        BattleCombatant[] targets = ExpandedAutomaticGuardianTargets();
        if (targets.Length == 0)
        {
            return;
        }
        BattleCombatant actor = NativeSupportActor();
        BattleTargetRequest request = new(new BattleTargetSelection.SideGroup(BattleTargetSide.Enemies), BattleTargetEligibility.Living, false, BattleTargetReplacement.None);
        actions.Enqueue(new(actor.Id, targets[0].Id, request, BattleAbility.EnemyAbility, id)
        {
            NativePlayerReaction = BattleNativePlayerReaction.AutomaticGuardian,
            NativeQueuedCommand = 245,
            NativeAction = action,
            NativeSequence = NativeBattleActionGraphics.ResolvePlayerSequence(245, 0, id, definition.NativeCommandSequences)
        });
        actor.IsBusy = true;
        Log("Queued", actor.Id, targets[0].Id, id, $"{actor.Name}: {action.Name} queued.");
    }
}
