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
}
