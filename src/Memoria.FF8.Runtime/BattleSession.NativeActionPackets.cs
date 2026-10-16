namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleActionPacketsState nativeActionPackets = NativeBattleActionPacketsState.Fresh();
    private NativeBattlePartyStatusMirrors nativePartyStatusMirrors = NativeBattlePartyStatusMirrors.Unknown();
    public NativeBattleActionPacketsState NativeActionPackets => nativeActionPackets;
    public NativeBattlePartyStatusMirrors NativePartyStatusMirrors => nativePartyStatusMirrors;
    public int? NativeEffectIoResult { get; private set; }
    public NativeBattleEffectMemory? NativeEffectMemory { get; private set; }
    public NativeBattleModelTextureCache? NativeModelTextureCache { get; private set; }

    private void InitializeNativeRetainedMemory()
    {
        if (definition.NativeRetainedMemory is not { } memory)
        {
            return;
        }
        memory.Validate();
        //47CCB0 initial entry calls48D1F0; the physical arenas, pointer and independent gates survive it.
        nativeActionPackets = memory.ActionPackets.ResetCounts();
        nativePartyStatusMirrors = memory.PartyStatusMirrors;
        nativeActionScratch = memory.ActionScratch;
        RestoreNativeElementAttack(memory.ElementAttack);
        NativeEffectIoResult = memory.EffectIoResult;
        NativeEffectMemory = memory.EffectMemory;
        NativeStage = memory.Stage;
        NativeModelTextureCache = memory.ModelTextureCache;
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
