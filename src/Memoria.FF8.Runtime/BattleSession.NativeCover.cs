namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (BattleCombatant[] Targets, BattleModelOwner? Source) ResolveNativeCover(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction native, BattleCombatant[] targets)
    {
        //48E830 passes native hit count to48EB90, rather than the number of selected recipients.
        if (native.HitCount != 1 || actor.IsPlayer || (command.NativeQueuedCommand ?? native.Command) != 8 ||
            command.TargetRequest.Selection is BattleTargetSelection.SideGroup || (native.AttackFlags & 3) != 0 || targets.Length == 0)
        {
            return (targets, null);
        }
        BattleCombatant original = targets[0];
        if (!original.IsPlayer || (NativeSavedStatusBits(original) & 0x100) == 0)
        {
            return (targets, null);
        }

        BattleCombatant? cover;
        if (original.NativeActorIndex == 1)
        {
            BattleCombatant? left = FindActiveNativeActor(0);
            BattleCombatant? right = FindActiveNativeActor(2);
            bool leftEligible = left is not null && CanCover(left);
            bool rightEligible = right is not null && CanCover(right);
            cover = leftEligible ? left : rightEligible ? right : null;
            if (leftEligible && rightEligible && NextRandomByte() <= 127)
            {
                cover = right;
            }
        }
        else if (original.NativeActorIndex is 0 or 2)
        {
            // A present, ineligible middle actor blocks the original opposite-slot fallback.
            BattleCombatant? adjacent = FindActiveNativeActor(1) ?? FindActiveNativeActor(2 - original.NativeActorIndex.Value);
            cover = adjacent is not null && CanCover(adjacent) ? adjacent : null;
        }
        else
        {
            cover = null;
        }

        // Extra rows have no original slot precedence. Preserve original selection when it succeeds,
        // then consider the eligible additional rows in battle order. An additional target can use any row.
        cover ??= allies.FirstOrDefault(candidate => candidate != original && candidate.IsActive && candidate.Slot is not null &&
            (original.NativeActorIndex is null || candidate.NativeActorIndex is null) && CanCover(candidate));
        return cover is null ? (targets, null) : ([cover], CreatePresentationModelOwner(original));
    }

    private static bool CanCover(BattleCombatant actor)
    {
        uint flags = actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? 0;
        return (flags & 0x10) != 0 && (NativeSavedStatusBits(actor) & 0x25) == 0 && (NativeBattleStatusBits(actor) & 0x2004009) == 0;
    }
}
