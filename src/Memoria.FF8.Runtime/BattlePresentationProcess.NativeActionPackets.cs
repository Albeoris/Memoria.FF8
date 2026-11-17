using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleActionPacketsState nativeActionPackets;
    private NativeBattleEffectMemory? nativeEffectMemory;
    private ImmutableDictionary<long, NativeBattleSceneReplaySource>? nativeRetainedSceneReplaySources;
    public NativeBattleActionPacketsState NativeActionPackets => nativeActionPackets;
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

    [MemberNotNull(nameof(nativeActionPackets))]
    private void InitializeNativeRetainedMemory()
    {
        NativeFileIo = this.resources.NativeGraphics is null ? this.resources.InitialState?.FileIo : (this.resources.InitialState?.FileIo ?? NativeBattleFileIoState.Unknown()).Reset482560();
        NativeVibrationProcess = this.resources.InitialVibration ?? this.resources.InitialState?.VibrationProcess;
        BattleModelRoster emptyRoster = new([]);
        NativeBattleModelAliases emptyAliases = new(emptyRoster, []);
        if (this.resources.InitialState is not { } memory)
        {
            nativeActionPackets = NativeBattleActionPacketsState.Create(emptyRoster, NativeBattleRetainedActionPackets.Fresh(), emptyAliases);
            return;
        }
        memory.Validate();
        memory = NativeBattleSceneCopyProvenance.RestoreRetainedSources(NativeBattleStorageSnapshots.RestoreForNewBattle(memory));
        nativeRetainedSceneReplaySources = memory.SceneReplaySources;
        //47CCB0 initial entry calls48D1F0; the physical arenas, pointer and independent gates survive it.
        nativeActionPackets = NativeBattleActionPacketsState.Create(emptyRoster, memory.ActionPackets, emptyAliases);
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
    }

    private NativeBattleGraphicsState ResetNativeActionPacketCounts(NativeBattleGraphicsState graphics)
    {
        //47DD90 ->48D1F0 clears C0/C1 only, preserving C2, action pause and physical packet payload.
        nativeActionPackets = nativeActionPackets.ResetCounts();
        return graphics;
    }
}
