namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeBattleGuardianBoost? NativeGuardianBoost => Presentation?.GuardianBoost;

    public bool PressNativeGuardianBoost()
    {
        return Outcome == BattleOutcome.Active && Presentation?.PressGuardianBoost() == true;
    }

    public void PublishNativeGuardianBoostMappedButtons(ushort mappedButtons)
    {
        Presentation?.PublishGuardianBoostMappedButtons(mappedButtons);
    }
}
