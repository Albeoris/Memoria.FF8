namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (BattleCombatant[] Targets, BattleModelOwner? Source) ResolveNativeCover(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction native, BattleCombatant[] targets)
    {
        //48E830 passes native hit count to48EB90, rather than the number of selected recipients.
        bool all = command.TargetRequest.Selection is BattleTargetSelection.SideGroup;
        if (native.HitCount != 1 || actor.EncodeNativeSlot() < 3 || (command.NativeQueuedCommand ?? native.Command) != 8 || all || (native.AttackFlags & 3) != 0 || targets.Length == 0)
        {
            return (targets, null);
        }
        BattleCombatant original = targets[0];
        if (original.EncodeNativeSlot() is < 0 or >= 3 || (NativeSavedStatusBits(original) & 0x100) == 0)
        {
            return (targets, null);
        }
        int chosen;
        if (original.EncodeNativeSlot() == 0)
        {
            chosen = NativeCoverCandidate(1);
            if (chosen == 254)
            {
                chosen = NativeCoverCandidate(2);
            }
        }
        else if (original.EncodeNativeSlot() == 2)
        {
            chosen = NativeCoverCandidate(1);
            if (chosen == 254)
            {
                chosen = NativeCoverCandidate(0);
            }
        }
        else
        {
            int first = NativeCoverCandidate(0);
            int second = NativeCoverCandidate(2);
            chosen = first == 0 ? second == 2 && NextRandomByte() <= 127 ? 2 : 0 : second;
        }
        if (chosen is < 0 or >= 3)
        {
            return (targets, null);
        }
        BattleCombatant cover = combatants.Single(candidate => candidate.EncodeNativeSlot() == chosen && candidate.IsPlayer);
        return ([cover], CreatePresentationModelOwner(original));
    }

    private int NativeCoverCandidate(int index)
    {
        BattleCombatant? actor = combatants.FirstOrDefault(candidate => candidate.EncodeNativeSlot() == index && candidate.IsPlayer && candidate.IsActive);
        if (actor is null)
        {
            return 254;
        }
        uint flags = actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? throw new NotSupportedException("Native Cover requires the other physical party actor's original equipped passive flags.");
        return (flags & 0x10) != 0 && (NativeSavedStatusBits(actor) & 0x25) == 0 && (NativeBattleStatusBits(actor) & 0x2004009) == 0 ? index : 255;
    }
}
