namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private BattlePresentationProcess(BattlePresentationProcess source)
    {
        if (source.presentationForeignActors is null || source.NativeGraphics is not { } graphics ||
            graphics.ActorModels.Owners.Any(owner => !graphics.NativeBindings.TryEncodeExactActor(owner, graphics.ActorModels, out _)))
        {
            throw new InvalidOperationException("Only an isolated presentation owner can fork its capability probe.");
        }
        if (source.hostedGuardianFormMode is not null || source.presentationNativeTargets?.Values.Any(binding => binding.CanActivate) == true || source.presentationActivations.Count != 0 || source.presentationActivationSequences.Count != 0)
        {
            throw new InvalidOperationException("A presentation with model activation or guardian form ownership cannot fork a capability probe.");
        }
        resources = source.resources;
        BattleSnapshot native = NativeBattleStorageSnapshots.Fork(new BattleSnapshot
        {
            NativeGraphics = source.NativeGraphics,
            NativeCamera = source.NativeCamera,
            NativeEffectMemory = source.NativeEffectMemory,
            NativeFileIo = source.NativeFileIo,
            NativeFileStaging = source.NativeFileStaging,
            NativeModelLoader = source.NativeModelLoader,
            NativeModelCpuResources = source.NativeModelCpuResources,
            NativeModelTextureCache = source.NativeModelTextureCache,
            NativeStage = source.NativeStage,
            NativeGlobalSoundProcess = source.NativeGlobalSoundProcess,
            NativeMusicProcess = source.NativeMusicProcess,
            NativeVibrationProcess = source.NativeVibrationProcess,
            NativeActionPackets = source.nativeActionPackets
        });
        NativeGraphics = native.NativeGraphics;
        NativeCamera = native.NativeCamera;
        NativeEffectMemory = native.NativeEffectMemory;
        NativeFileIo = native.NativeFileIo;
        NativeFileStaging = native.NativeFileStaging;
        NativeModelLoader = native.NativeModelLoader;
        NativeModelCpuResources = native.NativeModelCpuResources;
        NativeModelTextureCache = native.NativeModelTextureCache;
        NativeStage = native.NativeStage;
        NativeGlobalSoundProcess = native.NativeGlobalSoundProcess;
        NativeMusicProcess = native.NativeMusicProcess;
        NativeVibrationProcess = native.NativeVibrationProcess;
        nativeActionPackets = native.NativeActionPackets ?? throw new InvalidDataException("The presentation probe lost its source packet arena.");
        NativeEffectIoResult = source.NativeEffectIoResult;
        nativeCrtRandom = source.nativeCrtRandom;
        nativeBattleUiRandom = source.nativeBattleUiRandom;
        nativeGlobalRandom = source.nativeGlobalRandom;
        nativeGlobalRandomCalls = source.nativeGlobalRandomCalls;
        nativeEffectResourceResolver = source.nativeEffectResourceResolver;
        presentationForeignActors = new(source.presentationForeignActors);
        presentationNativeTargets = source.presentationNativeTargets is null ? null : new(source.presentationNativeTargets);
        presentationArena = source.presentationArena?.Fork(preservePresentation: true);
        presentationSceneContext = source.presentationSceneContext;
        presentationRibbonProjectionPolicy = source.presentationRibbonProjectionPolicy;
        presentationModelTransformPolicy = source.presentationModelTransformPolicy;
        presentationSourceResource = source.presentationSourceResource;
        presentationSourceOwner = source.presentationSourceOwner;
        presentationSourceModelId = source.presentationSourceModelId;
        presentationSourceIsParty = source.presentationSourceIsParty;
        presentationCameraMode = source.presentationCameraMode;
        presentationFirstAdvance = source.presentationFirstAdvance;
        presentationInvocationHeader = source.presentationInvocationHeader;
        presentationHostCamera = NativeBattleStorageSnapshots.Fork(source.presentationHostCamera);
        presentationInitialStage = NativeBattleStorageSnapshots.Fork(source.presentationInitialStage);
        presentationInitialUi = NativeBattleStorageSnapshots.Fork(source.presentationInitialUi);
        presentationInitialModels = NativeBattleStorageSnapshots.Fork(source.presentationInitialModels);
        presentationPartyActorCount = source.presentationPartyActorCount;
        presentationInitialMusicGain = source.presentationInitialMusicGain;
        EffectPresentationInvoked = source.EffectPresentationInvoked;
        presentationSelectedModulePhase = source.presentationSelectedModulePhase;
        presentationSelectedModuleComplete = source.presentationSelectedModuleComplete;
        presentationInvocationCompleted = source.presentationInvocationCompleted;
        presentationContacts.AddRange(source.presentationContacts);
        presentationSounds.AddRange(source.presentationSounds);
        presentationSequences.AddRange(source.presentationSequences);
    }

    internal BattlePresentationProcess ForkEffectPresentation() => new(this);
}
