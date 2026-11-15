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
        BattleSnapshot input = logicalOwner.CaptureState() with
        {
            NativeGraphics = graphics,
            NativeActionPackets = nativeActionPackets,
            NativeEffectMemory = NativeEffectMemory,
            NativeStage = NativeStage,
            NativeModelCpuResources = NativeModelCpuResources,
            NativeModelTextureCache = NativeModelTextureCache,
            NativeModelLoader = NativeModelLoader,
            NativeFileStaging = NativeFileStaging,
            NativeCamera = NativeCamera,
            NativeFileIo = NativeFileIo,
            NativeEffectIoResult = NativeEffectIoResult
        };
        return NativeBattleRetainedMemory.Capture(NativeBattleStorageSnapshots.Capture(input), coldExecutableSha256);
    }
}
