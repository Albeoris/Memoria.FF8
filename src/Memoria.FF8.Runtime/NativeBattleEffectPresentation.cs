using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A host actor exposes the native services it can actually represent. Missing services reject the selected presentation.</summary>
public sealed record NativeBattleForeignActor(BattleModelOwner Owner, NativeBattleModelTransform Transform, NativeBattleModuleAnchor Anchor, Func<byte, short, NativeBattleVector> ReadBone)
{
    public short Facing { get; init; }
    public int BoneCount { get; init; }
    public NativeBattleForeignModelGeometry? Geometry { get; init; }
    public Func<NativeBattleEffectActorGeometry>? ReadGeometry { get; init; }
    public NativeBattlePosedActor? Pose { get; init; }
    public NativeBattleGraphicsModel? NativeModel { get; init; }
    public NativeBattleGraphicsResource? NativeResource { get; init; }
    public bool CanReact { get; init; }
    public bool? AtStandingPose { get; init; }
    public uint? StatusFlags { get; init; }
    public bool IsParty => Owner.Side == BattleSide.Allies;
}

public abstract record NativeBattleEffectInvocation
{
    private NativeBattleEffectInvocation()
    {
    }

    public sealed record Combat(NativeBattleActionPacketsState Packets, NativeBattleEffectParameters Parameters) : NativeBattleEffectInvocation;
    public sealed record Positioning(BattleModelOwner Source, BattleModelRoster Roster, NativeBattleModelAliases Aliases) : NativeBattleEffectInvocation;
}

public sealed record NativeBattleEffectPresentationInputs(BattleDefinition Resources, NativeBattleGraphicsModel Source, NativeBattleGraphicsResource SourceResource, NativeBattleEffectInvocation Invocation, ushort Effect, NativeBattleEffectTextureState Textures, NativeCrtRandomState Random, NativeBattleCameraProjection Projection, ImmutableArray<NativeBattleForeignActor> ForeignActors)
{
    public NativeBattleArenaHost? Arena { get; init; }
    public NativeBattleEffectSceneContext? SceneContext { get; init; }
    public NativeBattleRibbonProjectionPolicy RibbonProjectionPolicy { get; init; } = NativeBattleRibbonProjectionPolicy.NativeCallerScratch;
    public NativeBattleEffectModelTransformPolicy ModelTransformPolicy { get; init; } = NativeBattleEffectModelTransformPolicy.NativeCallerScratch;
    public bool DeferInvocation { get; init; }
    public int? PartyActorCount { get; init; }
    public int? InitialMusicGain { get; init; }
    public NativeBattleSequenceProcessGlobals? SequenceGlobals { get; init; }
    public NativeBattleEffectCameraMode CameraMode { get; init; }
    public NativeBattleCameraPose? HostCameraPose { get; init; }
    public GuardianFormInvocationMode? GuardianFormMode { get; init; }
    public bool GuardianSummon { get; init; }
    public ImmutableArray<NativeBattleEffectModelBinding> NativeTargets { get; init; } = [];
}

public sealed record NativeBattleEffectModelBinding(BattleModelOwner Owner, NativeBattleGraphicsModel Model, NativeBattleGraphicsResource Resource)
{
    public bool CanActivate { get; init; }
    public bool IsParty => Owner.Side == BattleSide.Allies;
    public string ActorId => Owner switch
    {
        BattlePartyActorModel party => party.Actor.ActorId,
        BattleEnemyActorModel enemy => enemy.Actor.ActorId,
        _ => throw new InvalidDataException("A native presentation binding requires a canonical battle actor.")
    };
}

public enum NativeBattleEffectCameraMode
{
    Hold,
    HostPoseWithEffectTranslation,
    NativeCinematic
}

public enum NativeBattleForeignSequenceKind
{
    Hit,
    Standing
}

public sealed record NativeBattleForeignSequenceRequest(BattleModelOwner Owner, NativeBattleForeignSequenceKind Kind);

public sealed record NativeBattleEffectActivation(string ActorId, BattleModelOwner Owner);

public sealed record NativeBattleEffectPresentationStep(ImmutableArray<NativeBattleContactPacket> Contacts, ImmutableArray<NativeBattleModuleSound> Sounds)
{
    public ImmutableArray<NativeBattleForeignSequenceRequest> Sequences { get; init; } = [];
    public ImmutableArray<NativeBattleEffectActivation> Activations { get; init; } = [];
}

/// <summary>Runs the current native effect dispatcher without owning combat, inventory, rewards or host time.</summary>
public sealed class NativeBattleEffectPresentation : IDisposable
{
    private readonly BattlePresentationProcess owner;
    private readonly NativeBattleCameraProjection projection;
    private readonly ImmutableArray<NativeBattleForeignActor> actors;

    public NativeBattleEffectPresentation(NativeBattleEffectPresentationInputs inputs)
    {
        owner = BattlePresentationProcess.CreateEffectPresentation(inputs);
        projection = inputs.Projection;
        actors = inputs.ForeignActors;
        InitialHostCameraPose = inputs.HostCameraPose;
    }

    private NativeBattleEffectPresentation(NativeBattleEffectPresentation source)
    {
        owner = source.owner.ForkEffectPresentation();
        projection = source.projection;
        actors = source.actors;
        InitialHostCameraPose = source.InitialHostCameraPose;
    }

    public NativeBattleEffectPresentation Fork() => new(this);

    public NativeBattleGraphicsState Graphics => owner.NativeGraphics!;
    public NativeBattleEffectMemory Memory => owner.NativeEffectMemory!;
    public NativeCrtRandomState Random => owner.ReadEffectPresentationRandom();
    public NativeGlobalRandomState? GlobalRandom => owner.ReadEffectPresentationGlobalRandom();
    public NativeBattleGraphicsModel Source => owner.ReadEffectPresentationSource();
    internal BattleModelOwner SourceOwner => owner.ReadEffectPresentationSourceOwner();
    public NativeBattleGraphicsResource SourceResource => owner.ReadEffectPresentationSourceResource();
    public bool Invoked => owner.EffectPresentationInvoked;
    public bool ServiceComplete => owner.ReadEffectPresentationServiceComplete();
    public bool NativeBusy => owner.ReadEffectPresentationNativeBusy();
    public byte Parameter81 => Graphics.OriginalEffect?.Parameters?.Parameter81 ?? throw new InvalidOperationException("The presentation has no invoked parameter owner.");
    public bool Complete => owner.EffectPresentationInvoked && owner.EffectPresentationExecutionComplete && Memory.ReadReturnedTask5044B0() == 0 && Graphics.ModuleEffects.Head is null && Graphics.Auxiliary.Head is null && !owner.HasPendingEffectPresentationEvents && !owner.HasPendingEffectPresentationActivations;
    public NativeBattleStageColor StageColor => owner.ReadEffectPresentationStageColor();
    public NativeBattleSummonColors StageColorRules => owner.ReadEffectPresentationStageColorRules();
    public short StageColorDepth => StageColor.Depth;
    public short ViewTranslationY => owner.NativeCamera?.ViewTranslationScratch?.Y ?? 0;
    public NativeBattleCameraPose? CinematicCamera => owner.ReadHostedCinematicCamera();
    public NativeBattleCameraPose? InitialHostCameraPose { get; }
    public NativeBattleCameraProjection? Projection => owner.NativeCamera?.Projection;
    public bool IsGuardianFormCinematic => owner.IsHostedGuardianFormCinematic;
    internal bool IsGuardianSummon => owner.IsHostedGuardianSummon;
    public bool HidesHostStage => (IsGuardianFormCinematic || IsGuardianSummon) && Stage.Groups.All(group => (group.Flags & 2) == 0);
    public bool ReachedGuardianFormLanding => owner.ReachedHostedGuardianFormLanding();
    internal bool CanSkipGuardianFormFinisher => owner.CanSkipHostedGuardianFormFinisher;
    public NativeBattleActionMatrix ForeignPlacement(BattleModelOwner model) => owner.ReadEffectPresentationForeignPlacement(model);
    public bool ReplacesHostModel(BattleModelOwner model) => (Graphics.ActorModels.Get(model).Flags0 & 4) != 0;
    public ImmutableArray<NativeBattleArenaReplacement> ArenaReplacements => owner.presentationArena?.Replacements ?? [];
    public NativeBattleUiServices Ui => Graphics.UiServices ?? throw new InvalidDataException("Native presentation lost its UI owner.");
    public NativeBattleStageState Stage => owner.NativeStage ?? throw new InvalidDataException("Native presentation lost its arena owner.");
    public NativeBattleArenaHost? Arena => owner.presentationArena;
    public int? MusicGain => owner.ReadEffectPresentationMusicGain();
    public ImmutableArray<NativeBattleArenaPrimitiveId> SuppressedArenaPrimitives => owner.presentationArena?.SuppressedPrimitives(Stage) ?? [];

    public void Dispose() => owner.ReleaseEffectPresentation();

    public void Invoke(NativeBattleGraphicsModel source, NativeCrtRandomState random, NativeBattleEffectTextureState textures) => owner.InvokeEffectPresentation(source, random, textures);
    public void PublishSource(BattleModelOwner model, NativeBattleGraphicsModel source) => owner.PublishEffectPresentationSource(model, source);
    public void ReinvokeDirect(NativeBattleActionPacketsState packets, NativeBattleGraphicsModel source, NativeCrtRandomState random, NativeBattleEffectTextureState textures, NativeBattleEffectParameters parameters, ImmutableArray<NativeBattleForeignActor> foreignActors = default) => owner.ReinvokeEffectPresentationDirect(packets, source, random, textures, parameters, foreignActors);
    public void ReloadSourceResources() => owner.ReloadEffectPresentationSourceResources();
    internal void ReplaceHostedContacts(IEnumerable<NativeBattleContactPacket> packets) => owner.ReplaceHostedEffectContacts(packets);
    public NativeBattleEffectPresentationStep CompleteInvocation() => owner.CompleteEffectPresentationInvocation();
    public NativeBattleScriptValue ReadSourceVariable(NativeBattleGraphicsModel source, byte variable) => owner.ReadEffectPresentationSourceVariable(source, variable);
    public void WriteSourceVariable(byte variable, int value) => owner.WriteEffectPresentationSourceVariable(variable, value);
    public NativeBattleGraphicsModel WriteSourceTargetVariable(NativeBattleGraphicsModel source, byte variable, int value) => owner.WriteEffectPresentationSourceTargetVariable(source, variable, value);
    public ushort NextGlobalRandom() => owner.NextEffectPresentationGlobalRandom();
    public NativeBattlePreparedPrimitiveFrame? Overlay { get; private set; }

    public NativeBattleEffectPresentationStep AdvanceCurrent()
    {
        NativeBattleEffectPresentationStep step = Advance(Source, Random, projection, actors, Memory.Textures!);
        Overlay = owner.PrepareEffectOverlay();
        return step;
    }

    public NativeBattleEffectPresentationStep Advance(NativeBattleGraphicsModel source, NativeCrtRandomState random, NativeBattleCameraProjection projection, ImmutableArray<NativeBattleForeignActor> foreignActors, NativeBattleEffectTextureState textures)
    {
        try
        {
            return owner.AdvanceEffectPresentation(source, random, projection, foreignActors, textures);
        }
        catch
        {
            Dispose();
            throw;
        }
    }
}
