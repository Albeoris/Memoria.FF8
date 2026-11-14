using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverGuestHostActions
{
    private string? SourceStatusTargetUnsupportedReason(BattleNativeAction action, int target)
    {
        uint operations = action.Statuses1 & ~((ProcessActive ? 0x2000000u : 0) | Ff7GuestBattleStatuses.SupportedOriginBattle | Ff7GuestBattleStatuses.SupportedOwnedOriginBattle | Ff7GuestBattleStatuses.OriginDefenseReduction | 0x8000u);
        if (operations == 0 || action.AttackType is 27 or 29 or 35)
        {
            return null;
        }
        bool healing = action.AttackType is 3 or 4 or 5 or 6 or 21 or 25 or 32;
        ushort mask = AbilityTargetMask(action, target);
        bool requested = host.Actors.Any(actor => (mask & (1 << actor.Actor.Slot)) != 0 && (host.GetOriginBattleStatuses(actor.Actor.Slot) & operations) != 0);
        return !healing || requested ? "This source status operation has no supported combat owner." : null;
    }
}
