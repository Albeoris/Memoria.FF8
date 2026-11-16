using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleActionPacketsState nativeActionPackets;
    private NativeBattlePartyStatusMirrors nativePartyStatusMirrors = NativeBattlePartyStatusMirrors.Unknown();
    private NativeBattleEffectMemory? nativeEffectMemory;
    private ImmutableDictionary<long, NativeBattleSceneReplaySource>? nativeRetainedSceneReplaySources;
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

    [MemberNotNull(nameof(nativeActionPackets))]
    private void InitializeNativeRetainedMemory()
    {
        NativeFileIo = definition.NativeGraphics is null ? definition.NativeRetainedMemory?.FileIo : (definition.NativeRetainedMemory?.FileIo ?? NativeBattleFileIoState.Unknown()).Reset482560();
        NativeVibrationProcess = definition.NativeVibrationProcess ?? definition.NativeRetainedMemory?.VibrationProcess;
        BattleModelRoster emptyRoster = new([]);
        NativeBattleModelAliases emptyAliases = new(emptyRoster, []);
        if (definition.NativeRetainedMemory is not { } memory)
        {
            nativeActionPackets = NativeBattleActionPacketsState.Create(emptyRoster, NativeBattleRetainedActionPackets.Fresh(), emptyAliases);
            return;
        }
        memory.Validate();
        memory = NativeBattleSceneCopyProvenance.RestoreRetainedSources(NativeBattleStorageSnapshots.RestoreForNewBattle(memory));
        nativeRetainedSceneReplaySources = memory.SceneReplaySources;
        //47CCB0 initial entry calls48D1F0; the physical arenas, pointer and independent gates survive it.
        nativeActionPackets = NativeBattleActionPacketsState.Create(emptyRoster, memory.ActionPackets, emptyAliases);
        ResetDiagnosticReflectionHeaders();
        nativePartyStatusMirrors = memory.PartyStatusMirrors;
        RestoreNativePresentationStatusMemory(memory.PresentationStatusMemory);
        nativeActionScratch = memory.ActionScratch with { PublishedTargetCount = memory.ActionScratch.LastPublishedTargetCountE05 };
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

    private void PublishNativeSingleActionHeader(int targetCount)
    {
        (nativeActionPackets, BattleActionHeaderReference header) = nativeActionPackets.Publish(targetCount);
        // The native arena retains BYTE17 on Prepare. Every session publisher starts with one
        // dispatchable header; an explicit group may extend it after preparing further casts.
        nativeActionPackets = nativeActionPackets.WritePlayerRepeatMode(header, 1);
    }

    private void RefreshNativePacketBindings()
    {
        var (roster, aliases) = ReadBattlePresentationPacketBindings();
        nativeActionPackets = nativeActionPackets.WithRoster(roster, aliases);
    }

    private NativeBattleGraphicsState ResetNativeActionPacketCounts(NativeBattleGraphicsState graphics)
    {
        //47DD90 ->48D1F0 clears C0/C1 only, preserving C2, action pause and physical packet payload.
        nativeActionPackets = nativeActionPackets.ResetCounts();
        ResetDiagnosticReflectionHeaders(retainHostedPending: true);
        return graphics;
    }
}
