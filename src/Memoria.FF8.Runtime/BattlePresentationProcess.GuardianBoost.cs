namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    internal NativeBattleGuardianBoost? GuardianBoost => HostedGuardianBoost;
    internal BattlePresentationProcess.HostedGuardianBoostRequest? PendingGuardianBoost => PendingHostedGuardianBoost;

    internal bool PressGuardianBoost()
    {
        RequireIdle();
        return PressHostedGuardianBoost();
    }

    internal void PublishGuardianBoostMappedButtons(ushort mappedButtons)
    {
        RequireIdle();
        SetHostedGuardianBoostMappedButtons(mappedButtons);
    }
}
