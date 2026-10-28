namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleActionPacketsState nativeActionPackets = NativeBattleActionPacketsState.Fresh();
    private NativeBattlePartyStatusMirrors nativePartyStatusMirrors = NativeBattlePartyStatusMirrors.Unknown();
    private NativeBattleEffectMemory? nativeEffectMemory;
    public NativeBattleActionPacketsState NativeActionPackets => nativeActionPackets;
    public NativeBattlePartyStatusMirrors NativePartyStatusMirrors => nativePartyStatusMirrors;
    public int? NativeEffectIoResult { get; private set; }
    public NativeBattleEffectMemory? NativeEffectMemory
    {
        get => nativeEffectMemory;
        private set
        {
            if (NativeBattleTaskOperation.IsActive && !NativeBattleTaskCallerOperation.IsActive)
            {
                NativeBattleTaskOperation.AcceptPublishedMemory(value);
            }
            nativeEffectMemory = value;
        }
    }
    public NativeBattleModelTextureCache? NativeModelTextureCache { get; private set; }

    private void InitializeNativeRetainedMemory()
    {
        NativeFileIo = definition.NativeGraphics is null ? definition.NativeRetainedMemory?.FileIo : (definition.NativeRetainedMemory?.FileIo ?? NativeBattleFileIoState.Unknown()).Reset482560();
        NativeVibrationProcess = definition.NativeVibrationProcess ?? definition.NativeRetainedMemory?.VibrationProcess;
        if (definition.NativeRetainedMemory is not { } memory)
        {
            return;
        }
        memory.Validate();
        memory = NativeBattleStorageSnapshots.Restore(memory);
        //47CCB0 initial entry calls48D1F0; the physical arenas, pointer and independent gates survive it.
        nativeActionPackets = memory.ActionPackets.ResetCounts();
        nativePartyStatusMirrors = memory.PartyStatusMirrors;
        RestoreNativePresentationStatusMemory(memory.PresentationStatusMemory);
        nativeActionScratch = memory.ActionScratch;
        RestoreNativeElementAttack(memory.ElementAttack);
        NativeEffectIoResult = memory.EffectIoResult;
        NativeEffectMemory = memory.EffectMemory;
        NativeStage = memory.Stage;
        NativeModelTextureCache = memory.ModelTextureCache;
        NativeModelLoader = memory.ModelLoader;
        NativeFileStaging = memory.FileStaging;
        NativeModelCpuResources = memory.ModelCpuResources;
        if (NativeCamera is not null)
        {
            NativeCamera = NativeBattleCameraState.Initialize5041E0(memory.Camera);
        }
        RestoreNativeActionMemory(memory.ActionMemory);
        nativeScriptState = nativeScriptState with { AllocationControl = memory.AllocationControl };
    }

    private NativeBattleGraphicsState ResetNativeActionPacketCounts(NativeBattleGraphicsState graphics)
    {
        //47DD90 ->48D1F0 clears C0/C1 only, preserving C2, action pause and physical packet payload.
        nativeActionPackets = nativeActionPackets.ResetCounts();
        return graphics;
    }
}
