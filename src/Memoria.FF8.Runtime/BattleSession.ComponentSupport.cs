namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public void CompleteComponentBattleEntry()
    {
        if (NativeGraphics is not null || Frame != 0 || ActiveAction is not null || nativeSupport is null)
        {
            throw new InvalidOperationException("Component battle entry requires its fresh admitted host boundary and installed support rules.");
        }
        // TODO(TD-009): The component host admits47CCB0 case3 after queueing INIT without a raw112 entry barrier
        InitializeNativeSupportCallbacks();
    }

    private bool ComponentSupportClockAdmitted(bool clockAdmitted)
    {
        // TODO(TD-009): Map the existing component core/action completion boundary to the original DEB/action-pause admission
        return NativeGraphics is null && nativeSupport is { EntryInitialized: true } && clockAdmitted && ActiveAction is null && nativeTimedAction is null && nativeActionScratch.ComponentCompletion is null;
    }
}
