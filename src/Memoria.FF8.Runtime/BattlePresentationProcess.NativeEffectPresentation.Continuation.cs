using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record BattleExternalEffectActorState(NativeBattleModelTransform Transform, NativeBattleModuleAnchor Anchor, short Facing, int BoneCount,
    NativeBattleForeignModelGeometry? Geometry, BattleExternalEffectPoseState? Pose, NativeBattleGraphicsModel? NativeModel, NativeBattleGraphicsResource? NativeResource,
    bool CanReact, bool? AtStandingPose, uint? StatusFlags)
{
    internal static BattleExternalEffectActorState Capture(NativeBattleForeignActor actor)
    {
        BattleExternalEffectPoseState? pose = actor.Pose is { } currentPose ? new(currentPose.World, currentPose.Endpoints, currentPose.BoneParents, currentPose.BoneMatrices) : null;
        return new(actor.Transform, actor.Anchor, actor.Facing, actor.BoneCount, actor.Geometry, pose, actor.NativeModel, actor.NativeResource, actor.CanReact, actor.AtStandingPose, actor.StatusFlags);
    }

    internal NativeBattleForeignActor Bind(BattleModelOwner owner, BattleExternalEffectActorReaders readers)
    {
        NativeBattlePosedActor? pose = Pose is { } capturedPose
            ? new(capturedPose.World, capturedPose.Endpoints, readers.ReadLocalBone ?? throw new InvalidDataException("The external actor lost its semantic bone reader."))
            {
                BoneParents = capturedPose.BoneParents,
                BoneMatrices = capturedPose.BoneMatrices
            }
            : null;
        return new(owner, Transform, Anchor, readers.ReadBone)
        {
            Facing = Facing,
            BoneCount = BoneCount,
            Geometry = Geometry,
            ReadGeometry = readers.ReadGeometry,
            Pose = pose,
            NativeModel = NativeModel,
            NativeResource = NativeResource,
            CanReact = CanReact,
            AtStandingPose = AtStandingPose,
            StatusFlags = StatusFlags
        };
    }
}

internal sealed record BattleExternalEffectPoseState(NativeBattleActionMatrix World, ImmutableArray<NativeBattleVector> Endpoints,
    ImmutableArray<int> BoneParents, ImmutableArray<NativeBattleActionMatrix> BoneMatrices);

internal sealed record BattleExternalEffectActorReaders(Func<byte, short, NativeBattleVector> ReadBone, Func<NativeBattleEffectActorGeometry>? ReadGeometry,
    Func<byte, short, NativeBattleVector>? ReadLocalBone);

internal sealed record BattleExternalEffectHostBindings(IReadOnlyDictionary<BattleModelOwner, BattleExternalEffectActorReaders> Actors,
    Func<ImmutableArray<NativeBattleModelTransform>>? ReadOpposingTransforms);

internal sealed record BattleExternalEffectContinuation(
    ImmutableDictionary<BattleModelOwner, BattleExternalEffectActorState> Actors,
    ImmutableDictionary<BattleModelOwner, NativeBattleEffectModelBinding>? NativeTargets,
    NativeBattleArenaHost.Snapshot? Arena,
    NativeBattleEffectSceneContext? Scene,
    NativeBattleRibbonProjectionPolicy Ribbons,
    NativeBattleEffectModelTransformPolicy Transforms,
    NativeBattleEffectCameraMode CameraMode,
    bool FirstAdvance,
    BattleActionHeaderReference? Header,
    NativeBattleCameraState? HostCamera,
    NativeBattleStageState? InitialStage,
    NativeBattleUiServices? InitialUi,
    BattleActorModels? InitialModels,
    int? InitialMusicGain,
    bool Invoked,
    byte? ModulePhase,
    bool ModuleComplete,
    bool InvocationCompleted,
    ImmutableArray<NativeBattleContactPacket> Contacts,
    ImmutableArray<NativeBattleModuleSound> Sounds,
    ImmutableArray<NativeBattleForeignSequenceRequest> Sequences);

public sealed partial class BattlePresentationProcess
{
    private BattleExternalEffectContinuation CaptureExternalEffectContinuation()
    {
        if (presentationForeignActors is null || NativeGraphics is not { } graphics ||
            graphics.ActorModels.Owners.Any(owner => !graphics.NativeBindings.TryEncodeExactActor(owner, graphics.ActorModels, out _)))
        {
            throw new InvalidOperationException("An external effect continuation requires its retained host actors and model bindings.");
        }
        if (hostedGuardianFormMode is not null || presentationNativeTargets?.Values.Any(binding => binding.CanActivate) == true || presentationActivations.Count != 0 || presentationActivationSequences.Count != 0)
        {
            throw new InvalidOperationException("An external effect continuation cannot transfer model activation or guardian form ownership.");
        }
        return new(presentationForeignActors.ToImmutableDictionary(pair => pair.Key, pair => BattleExternalEffectActorState.Capture(pair.Value)),
            presentationNativeTargets?.ToImmutableDictionary(), presentationArena?.CaptureState(), presentationSceneContext is { } scene ? scene with { ReadOpposingTransforms = null } : null,
            presentationRibbonProjectionPolicy, presentationModelTransformPolicy, presentationCameraMode, presentationFirstAdvance, presentationInvocationHeader, presentationHostCamera,
            presentationInitialStage, presentationInitialUi, presentationInitialModels, presentationInitialMusicGain,
            EffectPresentationInvoked, presentationSelectedModulePhase, presentationSelectedModuleComplete, presentationInvocationCompleted,
            presentationContacts.ToImmutableArray(), presentationSounds.ToImmutableArray(), presentationSequences.ToImmutableArray());
    }

    private void RestoreExternalEffectContinuation(BattleExternalEffectContinuation external, BattleExternalEffectHostBindings host)
    {
        if (host.Actors.Count != external.Actors.Count || external.Actors.Keys.Any(owner => !host.Actors.ContainsKey(owner)))
        {
            throw new InvalidDataException("The continued external effect has different host actor bindings.");
        }
        presentationForeignActors = external.Actors.ToDictionary(pair => pair.Key, pair => pair.Value.Bind(pair.Key, host.Actors[pair.Key]));
        presentationNativeTargets = external.NativeTargets?.ToDictionary();
        presentationArena = external.Arena is { } arena ? NativeBattleArenaHost.Restore(arena) : null;
        presentationSceneContext = external.Scene is { } scene ? scene with { ReadOpposingTransforms = host.ReadOpposingTransforms } : null;
        presentationRibbonProjectionPolicy = external.Ribbons;
        presentationModelTransformPolicy = external.Transforms;
        presentationCameraMode = external.CameraMode;
        presentationFirstAdvance = external.FirstAdvance;
        presentationInvocationHeader = external.Header;
        presentationHostCamera = external.HostCamera;
        presentationInitialStage = external.InitialStage;
        presentationInitialUi = external.InitialUi;
        presentationInitialModels = external.InitialModels;
        presentationInitialMusicGain = external.InitialMusicGain;
        EffectPresentationInvoked = external.Invoked;
        presentationSelectedModulePhase = external.ModulePhase;
        presentationSelectedModuleComplete = external.ModuleComplete;
        presentationInvocationCompleted = external.InvocationCompleted;
        presentationContacts.AddRange(external.Contacts);
        presentationSounds.AddRange(external.Sounds);
        presentationSequences.AddRange(external.Sequences);
    }

    internal BattlePresentationProcess ForkEffectPresentation()
    {
        BattlePresentationSnapshot snapshot = CaptureSnapshot();
        BattleExternalEffectHostBindings host = new(presentationForeignActors!.ToDictionary(pair => pair.Key, pair => new BattleExternalEffectActorReaders(pair.Value.ReadBone, pair.Value.ReadGeometry, pair.Value.Pose?.ReadLocalBone)),
            presentationSceneContext?.ReadOpposingTransforms);
        return Restore(resources, snapshot, null, nativeEffectResourceResolver, host);
    }
}
