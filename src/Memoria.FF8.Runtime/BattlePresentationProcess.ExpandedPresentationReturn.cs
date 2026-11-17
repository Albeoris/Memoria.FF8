namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{

    internal NativeBattleRetainedMemory CaptureRetainedMemory(NativeBattleRetainedMemory logical, BattleOutcome outcome)
    {
        if (!HasBattleEntry || NativeGraphics is not { } graphics || hostedFrameServices is not null || ActiveActionToken is not null || PendingCompletion is not null)
        {
            throw new InvalidOperationException("Retained field memory requires the idle hosted graphics owner.");
        }
        NativeBattleRetainedMemory retained = logical with
        {
            ActionPackets = nativeActionPackets.ExportRetained(),
            EffectIoResult = NativeEffectIoResult,
            EffectMemory = NativeEffectMemory,
            Stage = NativeStage,
            ModelTextureCache = NativeModelTextureCache,
            ModelLoader = NativeModelLoader,
            FileStaging = NativeFileStaging,
            ModelCpuResources = NativeModelCpuResources,
            Camera = NativeCamera,
            FileIo = NativeFileIo,
            VibrationProcess = NativeVibrationProcess,
            MusicProcess = NativeMusicProcess
        };
        retained = retained.WithEntryState(this.resources.InitialState, graphics, outcome);
        return NativeBattleStorageSnapshots.Capture(retained);
    }
}
