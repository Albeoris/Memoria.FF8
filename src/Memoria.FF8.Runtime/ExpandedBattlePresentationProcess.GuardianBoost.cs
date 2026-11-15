using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class ExpandedBattlePresentationProcess
{
    internal NativeBattleGuardianBoost? GuardianBoost => presentation.HostedGuardianBoost;
    internal BattleSession.HostedGuardianBoostRequest? PendingGuardianBoost => presentation.PendingHostedGuardianBoost;

    internal bool PressGuardianBoost()
    {
        RequireIdle();
        return presentation.PressHostedGuardianBoost();
    }

    internal void PublishGuardianBoostMappedButtons(ushort mappedButtons)
    {
        RequireIdle();
        presentation.SetHostedGuardianBoostMappedButtons(mappedButtons);
    }

    internal BattleSession.PreparedHostedGuardianContacts PrepareGuardianContacts(BattleSession.HostedGuardianBoostRequest request, ImmutableArray<NativeBattleContactPacket> contacts)
    {
        RequireAdmissionWindow();
        return presentation.PrepareHostedGuardianContacts(request, contacts);
    }

    internal void ValidatePreparedGuardianContacts(BattleSession.PreparedHostedGuardianContacts prepared)
    {
        RequireAdmissionWindow();
        presentation.ValidatePreparedHostedGuardianContacts(prepared);
    }

    internal void CommitGuardianContacts(BattleSession.PreparedHostedGuardianContacts prepared)
    {
        RequireAdmissionWindow();
        presentation.CommitHostedGuardianContacts(prepared);
    }
}
