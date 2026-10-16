using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public NativeBattleClockRules ReadNativeClockRules()
    {
        return new(battleKernel.TimerSettings.AtbSpeedMultiplier);
    }

    public NativeBattleSupportRules ReadNativeSupportRules()
    {
        //Kernel29 starts at1CF8B14; the BYTE at+15 is1CF8B23, not a guessed frame interval.
        return new(battleKernel.TimerSettings.DeadTimer);
    }
}
