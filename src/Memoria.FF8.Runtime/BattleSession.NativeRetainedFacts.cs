namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattlePartyStatusMirrors nativePartyStatusMirrors = NativeBattlePartyStatusMirrors.Unknown();
    public NativeBattlePartyStatusMirrors NativePartyStatusMirrors => nativePartyStatusMirrors;

    private void InitializeNativeRetainedMemory()
    {
        if (definition.NativeRetainedMemory is not { } memory)
        {
            return;
        }
        memory.Validate();
        nativePartyStatusMirrors = memory.PartyStatusMirrors;
        RestoreNativePresentationStatusMemory(memory.PresentationStatusMemory);
        nativeActionScratch = memory.ActionScratch with { PublishedTargetCount = memory.ActionScratch.LastPublishedTargetCountE05 };
        RestoreNativeElementAttack(memory.ElementAttack);
        RestoreNativeActionMemory(memory.ActionMemory);
        nativeScriptState = nativeScriptState with { AllocationControl = memory.AllocationControl };
    }
}
