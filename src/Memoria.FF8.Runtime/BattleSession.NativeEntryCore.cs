namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool DispatchNativeEntryCore()
    {
        if (NativeGraphics is not { EntryLifecycle: 3 } graphics)
        {
            return false;
        }
        //47DD70 advances the entry case;47CCB0 case3 runs next outer frame before500900, separately from ordinary case4.
        NativeGraphics = graphics with { EntryLifecycle = 4, EntryReady = true };
        NativeEffectMemory = (NativeEffectMemory ?? throw new NotSupportedException("Native47CCB0 case3 requires its retained action-phase process byte.")) with { ActionPhaseDEA = 1 };
        QueueNativeInitialParticipants();
        if (nativeBattleClock is not null)
        {
            SetNativeBattleClockActive(true);
        }
        InitializeNativeSupportCallbacks();
        return true;
    }
}
