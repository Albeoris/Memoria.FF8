using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverGuestHostActions
{
    private string? SourceStatusTargetUnsupportedReason(BattleNativeAction action, int target)
    {
        if ((action.Statuses1 & 0x68000) == 0 || action.AttackType is 27 or 29 or 35)
        {
            return null;
        }
        ushort mask = AbilityTargetMask(action, target);
        return host.Actors.Any(actor => (mask & (1 << actor.Actor.Slot)) != 0 && !host.GuestVitalsBindings.Any(binding => binding.HostSlot == actor.Actor.Slot)) ? "The original status operation currently requires an FFVIII character target." : null;
    }
}
