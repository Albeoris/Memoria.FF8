namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal NativeBattleRetainedMemory CaptureRetainedBattleMemory(BattleSnapshot logical, string? coldExecutableSha256)
    {
        return expandedPresentationProcess is { } process
            ? process.CaptureRetainedMemory(this, coldExecutableSha256)
            : NativeBattleRetainedMemory.Capture(logical, coldExecutableSha256);
    }

    internal NativeBattleRetainedMemory CaptureHostedRetainedMemory(BattleSession logicalOwner, string? coldExecutableSha256)
    {
        if (!hostedBattlePresentation || NativeGraphics is not { } graphics || hostedFrameServices is not null)
        {
            throw new InvalidOperationException("Retained field memory requires the idle hosted graphics owner.");
        }
        NativeBattleRetainedMemory retained = new(nativeActionPackets.ExportRetained(), logicalOwner.NativeScriptState.AllocationControl, logicalOwner.nativePartyStatusMirrors, logicalOwner.nativeActionScratch with { ComponentCompletion = null }, NativeEffectIoResult, coldExecutableSha256)
        {
            PresentationStatusMemory = logicalOwner.nativePresentationStatusMemory.State,
            ElementAttack = logicalOwner.nativeElementAttack,
            EffectMemory = NativeEffectMemory,
            Stage = NativeStage,
            ModelTextureCache = NativeModelTextureCache,
            ModelLoader = NativeModelLoader,
            FileStaging = NativeFileStaging,
            ModelCpuResources = NativeModelCpuResources,
            Camera = NativeCamera,
            FileIo = NativeFileIo,
            VibrationProcess = logicalOwner.NativeVibrationProcess,
            MusicProcess = logicalOwner.NativeMusicProcess,
            ActionMemory = logicalOwner.nativeActionMemory.State
        };
        retained = retained.WithEntryState(logicalOwner.definition.NativeRetainedMemory, graphics, logicalOwner.Outcome);
        return NativeBattleStorageSnapshots.Capture(retained);
    }
}
