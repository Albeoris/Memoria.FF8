namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal NativeBattleRetainedMemory CaptureRetainedBattleMemory(BattleSnapshot logical, string? coldExecutableSha256)
    {
        if (Presentation is not { } process)
        {
            return NativeBattleRetainedMemory.Capture(logical, coldExecutableSha256);
        }
        NativeBattleRetainedMemory retained = new(process.NativeActionPackets.ExportRetained(), nativeScriptState.AllocationControl, nativePartyStatusMirrors, nativeActionScratch, null, coldExecutableSha256)
        {
            PresentationStatusMemory = nativePresentationStatusMemory.State,
            ElementAttack = nativeElementAttack,
            ActionMemory = nativeActionMemory.State
        };
        return process.CaptureRetainedMemory(retained, Outcome);
    }
}
