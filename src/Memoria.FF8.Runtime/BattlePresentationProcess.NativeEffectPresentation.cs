using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private Dictionary<BattleModelOwner, NativeBattleForeignActor>? presentationForeignActors;
    internal NativeBattleArenaHost? presentationArena;
    private NativeBattleEffectSceneContext? presentationSceneContext;
    private NativeBattleRibbonProjectionPolicy presentationRibbonProjectionPolicy;
    private NativeBattleEffectModelTransformPolicy presentationModelTransformPolicy;
    private NativeBattleGraphicsResource? presentationSourceResource;
    private BattleModelOwner? presentationSourceOwner;
    private BattleModelOwner RequirePresentationSourceOwner() => presentationSourceOwner ?? throw new InvalidDataException("The presentation lost its source model owner.");
    private byte? presentationSourceModelId;
    private bool? presentationSourceIsParty;
    private Dictionary<BattleModelOwner, NativeBattleEffectModelBinding>? presentationNativeTargets;
    private bool presentationFirstAdvance = true;
    private BattleActionHeaderReference? presentationInvocationHeader;
    private NativeBattleCameraState? presentationHostCamera;
    private NativeBattleStageState? presentationInitialStage;
    private NativeBattleUiServices? presentationInitialUi;
    private BattleActorModels? presentationInitialModels;
    private int? presentationPartyActorCount;
    private int? presentationInitialMusicGain;
    private NativeBattleEffectCameraMode presentationCameraMode;
    private GuardianFormInvocationMode? hostedGuardianFormMode;
    private bool hostedGuardianFormContinued;
    private readonly List<NativeBattleContactPacket> presentationContacts = [];
    private readonly List<NativeBattleModuleSound> presentationSounds = [];
    private readonly List<NativeBattleForeignSequenceRequest> presentationSequences = [];
    internal bool EffectPresentationInvoked { get; private set; }
    private byte? presentationSelectedModulePhase;
    private bool presentationSelectedModuleComplete;
    internal bool EffectPresentationExecutionComplete => presentationSelectedModulePhase is null || presentationSelectedModuleComplete;

    /// <summary>The host retains its camera projection. Native control resources may register without acquiring a camera lease; their control programs are not executed here.</summary>
    private BattlePresentationProcess(NativeBattleEffectPresentationInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs.Resources);
        _ = inputs.Resources.RequirePrimitiveExecution();
        resources = inputs.Resources;
        presentationArena = inputs.Arena;
        presentationSceneContext = inputs.SceneContext;
        presentationSceneContext?.Validate();
        if (!Enum.IsDefined(inputs.RibbonProjectionPolicy))
        {
            throw new InvalidDataException("The presentation ribbon projection policy is unsupported.");
        }
        presentationRibbonProjectionPolicy = inputs.RibbonProjectionPolicy;
        if (!Enum.IsDefined(inputs.ModelTransformPolicy))
        {
            throw new InvalidDataException("The presentation model transform policy is unsupported.");
        }
        presentationModelTransformPolicy = inputs.ModelTransformPolicy;
        presentationPartyActorCount = inputs.PartyActorCount;
        presentationCameraMode = inputs.CameraMode;
        hostedGuardianFormMode = inputs.GuardianFormMode;
        IsHostedGuardianSummon = inputs.GuardianSummon;
        nativeCrtRandom = inputs.Random;
        nativeGlobalRandom = this.resources.InitialGlobalRandom;
        presentationSourceResource = inputs.SourceResource;
        bool positioning = hostedGuardianFormMode == GuardianFormInvocationMode.EntryOnly;
        (BattleModelOwner sourceOwner, BattleModelRoster roster, NativeBattleModelAliases aliases) = inputs.Invocation switch
        {
            NativeBattleEffectInvocation.Combat combat when !positioning => (combat.Parameters.Source, combat.Packets.Roster, combat.Packets.ModelAliases),
            NativeBattleEffectInvocation.Positioning entrance when positioning && inputs.Effect == 201 && inputs.CameraMode == NativeBattleEffectCameraMode.NativeCinematic && !inputs.GuardianSummon && entrance.Source is { Side: BattleSide.Allies }
                => (entrance.Source, entrance.Roster, entrance.Aliases),
            _ => throw new InvalidDataException("Native effect presentation requires the invocation binding for its selected presentation mode.")
        };
        presentationSourceOwner = sourceOwner;
        presentationSourceModelId = inputs.Source.ModelId4;
        presentationSourceIsParty = sourceOwner.Side == BattleSide.Allies;
        ValidatePresentationBindings(inputs, roster, sourceOwner);
        presentationForeignActors = inputs.ForeignActors.ToDictionary(actor => actor.Owner);
        presentationNativeTargets = inputs.NativeTargets.ToDictionary(target => target.Owner);
        bool hasExtendedOwners = roster.Owners.Any(owner => !aliases.TryEncodeExact(owner, out _));
        if (inputs.Invocation is NativeBattleEffectInvocation.Combat combatInvocation)
        {
            nativeActionPackets = combatInvocation.Packets.ActivateHeader(combatInvocation.Parameters.Header);
            presentationInvocationHeader = combatInvocation.Parameters.Header;
        }
        else
        {
            nativeActionPackets = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
        }
        NativeEffectMemory = NativeBattleStorageSnapshots.Fork(this.resources.InitialState?.EffectMemory ?? NativeBattleEffectMemory.FromColdProcess()).ReleaseReturnedTask() with { Textures = inputs.Textures };
        NativeEffectMemory = NativeBattleGraphicsInitialization.Initialize500520(NativeEffectMemory);
        NativeCamera = BindEffectPresentationHostProjection(NativeBattleCameraState.FromColdProcess(), inputs.Projection);
        if (presentationCameraMode == NativeBattleEffectCameraMode.NativeCinematic)
        {
            NativeCamera = NativeCamera.WriteEffectGlobals(inputs.HostCameraPose ?? throw new InvalidDataException("The cinematic presentation requires its initial host camera pose.")) with { BlendProgress = 0 };
        }
        presentationHostCamera = NativeCamera;
        NativeFileStaging = NativeBattleTemporaryMemory.Reset5082F0(hasExtendedOwners ? NativeBattleFileStaging.FromColdProcess(HostedPresentationExecutableHash) : NativeBattleFileStaging.FromColdProcess());
        NativeFileIo = NativeBattleFileIoState.FromColdProcess();
        NativeGlobalSoundProcess = this.resources.InitialSound ?? NativeGlobalSoundProcess.FromBufferlessColdProcess();
        presentationInitialMusicGain = inputs.InitialMusicGain;
        if (inputs.InitialMusicGain is { } musicGain)
        {
            if (musicGain is < 0 or > 127)
            {
                throw new InvalidDataException("The presentation music gain must be within its native relative range0..127.");
            }
            NativeMusicPlaybackState playback = NativeGlobalSoundProcess.MusicPlayback ?? throw new NotSupportedException("The presentation music gain requires its current playback owner.");
            NativeGlobalSoundProcess = NativeGlobalSoundProcess with { MusicPlayback = playback.SetGain46BB40(0, musicGain) };
        }
        NativeVibrationProcess = this.resources.InitialVibration ?? this.resources.InitialState?.VibrationProcess ?? NativeVibrationProcessState.FromColdProcess();
        NativeStage = inputs.Arena?.CreatePresentationStage() ?? NativeBattleStageState.FromColdProcess();
        if (IsHostedGuardianFormCinematic || IsHostedGuardianSummon)
        {
            // Opcode 0x6D controls visibility of the host stage, whose mesh data remains with the renderer.
            NativeStage = NativeStage with { Groups = NativeStage.Groups.Select(group => group with { Flags = 2 }).ToImmutableArray() };
        }
        presentationInitialStage = NativeStage;
        NativeBattleModelBoundary boundary = NativeBattleModelBoundary.FromRetainedMemory(this.resources.InitialState);
        BattleActorModels models = new([]);
        foreach (BattleModelOwner owner in roster.Owners)
        {
            NativeBattleGraphicsModel? supplied = owner == sourceOwner
                ? inputs.Source
                : presentationNativeTargets.TryGetValue(owner, out NativeBattleEffectModelBinding? target)
                    ? target.Model
                    : presentationForeignActors[owner].NativeModel;
            if (aliases.TryEncodeExact(owner, out byte originalSlot))
            {
                (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(originalSlot), owner, models);
                if (supplied is not null)
                {
                    models = models.Set(owner, supplied);
                }
            }
            else
            {
                models = models.Add(owner, supplied ?? NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null));
            }
        }
        models = BindPresentationForeignModels(models);
        presentationInitialUi = NativeBattleUiServices.Initialize();
        BattleContactTaskStorageContract contactStorage = hasExtendedOwners ? BattleContactTaskStorageContract.ExpandableArena : BattleContactTaskStorageContract.OriginalArena;
        NativeGraphics = InitializeNativeGraphicsFrame500400(new(models, boundary, 0, 0, NativeBattleEffectsState.Clear(contactStorage)) { UiServices = presentationInitialUi });
        byte sourceModel = inputs.Source.ModelId4 ?? throw new NotSupportedException("Native effect presentation requires its source model identity.");
        if (hasExtendedOwners)
        {
            NativeGraphics = InitializeHostedPresentationModelResources(NativeGraphics, sourceModel >= 16 && presentationNativeTargets.Values.Any(binding => binding.CanActivate));
        }
        else if (sourceModel < 16)
        {
            NativeGraphics = InitializeEffectPresentationSourceResources(NativeGraphics, inputs.SourceResource, preserveSelectedProgram: true);
        }
        else if (presentationNativeTargets.Values.Any(binding => binding.CanActivate))
        {
            NativeGraphics = InitializeEffectPresentationEnemyResources(NativeGraphics);
        }
        if (inputs.SequenceGlobals is { } sequenceGlobals)
        {
            NativeEffectMemory = NativeEffectMemory with { SequenceSine = sequenceGlobals.Sine, SequenceCosine = sequenceGlobals.Cosine };
        }
        InitializeEffectPresentationServices(inputs.Invocation);
        if (hasExtendedOwners)
        {
            NativeGraphics!.Validate();
            BindHostedCinematicActors();
        }
        presentationInitialModels = NativeGraphics!.ActorModels;
        InitializeNativePrimitiveDepth47CE10();
        if (presentationCameraMode == NativeBattleEffectCameraMode.HostPoseWithEffectTranslation && inputs.Effect == NativeBattleSonic139Profile.Effect)
        {
            NativeBattlePrimitiveProcess process = NativeEffectMemory.PrimitiveProcess ?? throw new NotSupportedException("Hosted Sonic Wave requires its original primitive process.");
            byte buffer = process.Buffer80 ?? throw new NotSupportedException("Hosted Sonic Wave requires its selected draw buffer.");
            NativeBattleSoftwarePrimitiveState software = process.Software ?? throw new NotSupportedException("Hosted Sonic Wave requires its original software drawing owner.");
            NativeBattlePcTextureWindowCache cache = process.TextureWindowCache ?? throw new NotSupportedException("Hosted Sonic Wave requires its retained PC texture cache.");
            NativeBattleGraphicsDefinition graphics = this.resources.NativeGraphics ?? throw new NotSupportedException("Hosted Sonic Wave requires its original primitive resources.");
            NativeSoftwarePrimitiveTable softwareTable = graphics.SoftwarePrimitiveTable ?? throw new NotSupportedException("Hosted Sonic Wave requires its original software dispatch table.");
            bool hardware = graphics.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Hosted Sonic Wave requires its selected hardware execution capability.");
            NativeBattleSoftwarePrimitiveExecution execution = new(software, hasExtendedOwners ? NativeEffectMemory.Textures! : inputs.Textures, softwareTable, hardware, cache);
            execution.ApplyEnvironment45B660(buffer ^ 1);
            NativeEffectMemory = NativeEffectMemory with { PrimitiveProcess = process with { Software = execution.State, TextureWindowCache = execution.Cache }, Textures = execution.Textures };
        }
        if (NativeGraphics.ActionGraphics is { Callback: 0x50BC20 } context)
        {
            (NativeBattleGraphicsState selectedGraphics, byte phase, _) = PollNativeUnsequencedGraphics(NativeGraphics, context, 0);
            NativeGraphics = selectedGraphics;
            presentationSelectedModulePhase = phase;
        }
        else
        {
            NativeGraphics = BeginNativeOriginalEffect(NativeGraphics!, inputs.Effect);
        }
        if (IsHostedGuardianSummon)
        {
            NativeEffectMemory = NativeEffectMemory! with { GuardianCasterBank = 0 };
        }
        if (inputs.Invocation is NativeBattleEffectInvocation.Combat entered)
        {
            NativeGraphics = NativeGraphics with { OriginalEffect = NativeGraphics.OriginalEffect! with { Parameters = entered.Parameters.CaptureOriginalWindow(NativeGraphics.NativeBindings.CaptureAliases(NativeGraphics.ActorModels)) } };
        }
        if (!inputs.DeferInvocation)
        {
            InvokeEffectPresentation();
        }
        ValidateEffectPresentationServices();
    }

    internal static BattlePresentationProcess CreateEffectPresentation(NativeBattleEffectPresentationInputs inputs) => new(inputs);

    internal void ReplaceHostedEffectContacts(IEnumerable<NativeBattleContactPacket> packets)
    {
        if (presentationForeignActors is null || presentationInvocationHeader is not { } header)
        {
            throw new InvalidDataException("Hosted recomputation requires the presentation's invocation header.");
        }
        nativeActionPackets = nativeActionPackets.ReplaceHeaderContacts(header, packets.ToImmutableArray());
    }

    private NativeBattleEffectSceneContext ReadRequiredEffectSceneContext()
    {
        return presentationSceneContext ?? NativeBattleEffectSceneContext.FromNativeEncounter(checked((ushort)(this.resources.EncounterId ?? throw new NotSupportedException("The native effect requires its scene placement context."))));
    }

    internal void ReleaseEffectPresentation()
    {
        presentationArena?.ClearPresentation();
        NativeStage = presentationInitialStage;
        if (presentationInitialMusicGain is { } musicGain && NativeGlobalSoundProcess is { MusicPlayback: { } playback } sound)
        {
            NativeGlobalSoundProcess = sound with { MusicPlayback = playback.SetGain46BB40(0, musicGain) };
        }
        if (NativeGraphics is { } graphics)
        {
            BattleActorModels models = graphics.ActorModels;
            foreach (BattleModelEntry<BattleModelOwner> entry in graphics.ActorModels.Entries)
            {
                NativeBattleGraphicsModel original = presentationInitialModels!.Get(entry.Owner);
                NativeBattleGraphicsModel model = entry.State;
                models = models.Set(entry.Owner, model with { Flags0 = (ushort)((model.Flags0 & ~0x804) | (original.Flags0 & 0x804)), Color40 = original.Color40, Color44 = original.Color44, Alpha7 = original.Alpha7 });
            }
            NativeGraphics = graphics with { UiServices = presentationInitialUi, ActorModels = models };
        }
    }

    internal void InvokeEffectPresentation()
    {
        if (EffectPresentationInvoked)
        {
            throw new InvalidOperationException("The native presentation descriptor was already invoked.");
        }
        if (presentationSelectedModulePhase is null)
        {
            NativeGraphics = InvokeNativeOriginalEffect(NativeGraphics!);
        }
        EffectPresentationInvoked = true;
    }

    internal void InvokeEffectPresentation(NativeBattleGraphicsModel source, NativeCrtRandomState randomState, NativeBattleEffectTextureState textures)
    {
        if (EffectPresentationInvoked)
        {
            throw new InvalidOperationException("The native presentation descriptor was already invoked.");
        }
        NativeBattleGraphicsModel loaded = NativeGraphics!.ActorModels.Get(RequirePresentationSourceOwner());
        source = source with { ActiveResourceRow100 = loaded.ActiveResourceRow100, OriginalResourceRow104 = loaded.OriginalResourceRow104, TextureResourceIndex = loaded.TextureResourceIndex, LinkedTextureResourceIndex = loaded.LinkedTextureResourceIndex, BodyTextureSlots = loaded.BodyTextureSlots, LinkedTextureSlots = loaded.LinkedTextureSlots, TextureAllocationMask = loaded.TextureAllocationMask };
        NativeGraphics = NativeGraphics with { ActorModels = NativeGraphics.ActorModels.Set(RequirePresentationSourceOwner(), source) };
        if (NativeModelTextureCache?.PresentationActors is not null)
        {
            NativeGraphics.Validate();
        }
        BindHostedCinematicActors();
        nativeCrtRandom = randomState;
        NativeEffectMemory = NativeEffectMemory! with { Textures = AcceptHostedPresentationTextures(textures) };
        InvokeEffectPresentation();
    }

    internal void ReloadEffectPresentationSourceResources()
    {
        if (!EffectPresentationInvoked || NativeEffectMemory!.ReadReturnedTask5044B0() != 0 || NativeGraphics!.ModuleEffects.Head is not null || NativeGraphics.Auxiliary.Head is not null || HasPendingEffectPresentationEvents)
        {
            throw new InvalidOperationException("Native presentation source reload waits for effect completion and consumed output.");
        }
        NativeGraphics = InitializeEffectPresentationSourceResources(NativeGraphics, presentationSourceResource!);
    }

    private void ValidateEffectPresentationServices()
    {
        _ = ReadEffectPresentationStageColor();
        NativeCamera = NativeCamera!;
    }

    /// <summary>The mixed host acknowledges camera requests without moving its view or running native camera tracks.</summary>
    internal static NativeBattleCameraState HoldEffectPresentationCamera(NativeBattleCameraState camera, NativeBattleCameraState host, NativeBattleEffectCameraMode mode = NativeBattleEffectCameraMode.Hold)
    {
        NativeBattleCameraViewTranslationScratch translation = mode == NativeBattleEffectCameraMode.HostPoseWithEffectTranslation && camera.ViewTranslationScratch is { } effectTranslation ? effectTranslation : new(0, 0, 0);
        return host with { ControlResource = camera.ControlResource, ControlStagingSource = camera.ControlStagingSource, ControlCursor = null, ViewTranslationScratch = translation };
    }

    internal static NativeBattleCameraState BindEffectPresentationHostProjection(NativeBattleCameraState camera, NativeBattleCameraProjection projection)
    {
        return camera with { Projection = projection, FocalLength = projection.FocalLength is ushort focal ? unchecked((short)focal) : null };
    }

    internal NativeBattleStageColor ReadEffectPresentationStageColor()
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("The host has no native arena color owner.");
        return NativeBattleStageColor.ReadUniform(stage.Groups, presentationArena?.PresentationGroupMask ?? 15);
    }

    internal NativeBattleSummonColors ReadEffectPresentationStageColorRules() => this.resources.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native stage shading requires its interpolation constants.");

    internal bool HasPendingEffectPresentationEvents => presentationContacts.Count != 0 || presentationSounds.Count != 0 || presentationSequences.Count != 0;
    internal NativeGlobalRandomState? ReadEffectPresentationGlobalRandom() => nativeGlobalRandom;
    internal int? ReadEffectPresentationMusicGain() => presentationInitialMusicGain is null ? null : (NativeGlobalSoundProcess?.MusicPlayback ?? throw new InvalidDataException("Native presentation lost its music gain owner.")).ReadGain(0);
    internal NativeCrtRandomState ReadEffectPresentationRandom() => nativeCrtRandom ?? throw new InvalidDataException("Native presentation lost its random owner.");
    internal BattleModelOwner ReadEffectPresentationSourceOwner() => RequirePresentationSourceOwner();
    internal NativeBattleGraphicsModel ReadEffectPresentationSource() => NativeGraphics!.ActorModels.Get(RequirePresentationSourceOwner());
    internal NativeBattleGraphicsResource ReadEffectPresentationSourceResource() => NativeGraphicsPoseResource(NativeGraphics!, RequirePresentationSourceOwner());
    internal NativeBattleCameraPose? ReadHostedCinematicCamera() => presentationCameraMode == NativeBattleEffectCameraMode.NativeCinematic ? NativeCamera?.PublishedPresentationPose : null;
    internal bool IsHostedGuardianFormCinematic => hostedGuardianFormMode is GuardianFormInvocationMode.EntryOnly or GuardianFormInvocationMode.FinisherWithoutEntry;
    internal bool IsHostedGuardianSummon { get; }
    internal bool ReachedHostedGuardianFormLanding() => hostedGuardianFormMode == GuardianFormInvocationMode.EntryOnly && ReachedIfritLandingBoundary();
    internal bool CanSkipHostedGuardianFormFinisher => hostedGuardianFormContinued && !NativeBattleIfritContinuation.HasReachedFinale(RequireWaterMemory().ScriptedEffects!) ||
        IsHostedGuardianSummon && NativeGraphics?.OriginalEffect is { ReturnedDescriptor: > 0 } effect &&
        NativeBattleGuardianAnimationFinale.IsSupported(effect.Binding.Effect) && !NativeBattleGuardianAnimationFinale.HasReachedFinale(effect, NativeEffectMemory!);

    internal NativeBattleEffectPresentationStep AdvanceEffectPresentation(NativeBattleGraphicsModel source, NativeCrtRandomState randomState, NativeBattleCameraProjection projection, ImmutableArray<NativeBattleForeignActor> foreignActors, NativeBattleEffectTextureState textures)
    {
        if (presentationForeignActors is null)
        {
            throw new InvalidOperationException("This session is not a presentation component.");
        }
        Dictionary<BattleModelOwner, NativeBattleForeignActor> previousForeignActors = presentationForeignActors;
        presentationForeignActors = foreignActors.ToDictionary(actor => actor.Owner);
        nativeCrtRandom = randomState;
        presentationHostCamera = BindEffectPresentationHostProjection(presentationHostCamera!, projection);
        if (presentationCameraMode != NativeBattleEffectCameraMode.NativeCinematic)
        {
            NativeCamera = NativeCamera! with { Projection = projection, ViewTranslationScratch = new(0, 0, 0) };
        }
        NativeBattleGraphicsState graphics = NativeGraphics! with { ActorModels = BindPresentationForeignModels(NativeGraphics!.ActorModels.Set(RequirePresentationSourceOwner(), source), previousForeignActors), ModuleDraws = presentationFirstAdvance ? NativeGraphics.ModuleDraws : [], PrimitiveFrame = presentationFirstAdvance ? NativeGraphics.PrimitiveFrame : NativeBattlePrimitiveFrame.Clear() };
        NativeEffectMemory = NativeEffectMemory! with { PrimitiveProcess = presentationFirstAdvance ? NativeEffectMemory.PrimitiveProcess : NativeEffectMemory.PrimitiveProcess!.Reset500900(graphics.Flags), Textures = AcceptHostedPresentationTextures(textures) };
        presentationFirstAdvance = false;
        NativeGraphics = graphics;
        PumpNativeFileIo482590();
        graphics = NativeGraphics!;
        if (HasPendingEffectPresentationActivations)
        {
            graphics = AdvanceNativeDescriptors(graphics);
            NativeGraphics = graphics;
        }
        NativeBattleGraphicsModel currentSource = graphics.ActorModels.Get(RequirePresentationSourceOwner());
        if ((currentSource.Flags0 & 6) == 2 && ((currentSource.Flags2 ?? throw new InvalidDataException("Native presentation lost its source model WORD2.")) & 0x84) == 0)
        {
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, RequirePresentationSourceOwner()).PoseSource ?? throw new NotSupportedException("Native5086F0 requires its current source skeleton.");
            currentSource = NativeBattlePoseMatrices.PublishShadowBounds5086F0(currentSource, pose);
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(RequirePresentationSourceOwner(), currentSource) };
            NativeGraphics = graphics;
        }
        graphics = AdvanceNativeAuxiliary(graphics);
        NativeGraphics = graphics;
        graphics = AdvanceNativeModuleEffects(graphics);
        NativeGraphics = graphics;
        if (EffectPresentationInvoked && presentationSelectedModulePhase is byte selectedPhase && !presentationSelectedModuleComplete)
        {
            NativeBattleActionGraphicsState context = graphics.ActionGraphics ?? throw new InvalidDataException("The selected module lost its original graphics service.");
            (NativeBattleGraphicsState selectedGraphics, byte phase, bool complete) = PollNativeUnsequencedGraphics(graphics, context, selectedPhase);
            graphics = selectedGraphics;
            presentationSelectedModulePhase = phase;
            presentationSelectedModuleComplete = complete;
            if (complete)
            {
                nativeActionPackets = nativeActionPackets.CompleteHeader(context.Header);
            }
        }
        if (EffectPresentationInvoked && (presentationSelectedModulePhase is null || presentationSelectedModulePhase >= 3 && !presentationSelectedModuleComplete))
        {
            graphics = AdvanceNativeOriginalEffect(graphics);
        }
        NativeGraphics = graphics;
        if (presentationCameraMode == NativeBattleEffectCameraMode.NativeCinematic)
        {
            const uint projectionFlag = 4;
            AdvanceNativeCamera(graphics.Flags | projectionFlag);
            NativeCamera = NativeCamera!.PublishProjection(graphics.Flags | projectionFlag);
        }
        graphics = ConsumeNativeGraphicsContacts(graphics);
        graphics = AdvanceEffectPresentationActorSequences(graphics);

        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeEffectMemory = DrainNativeTextureUploads(memory, memory.Textures!);
        NativeEffectMemory = NativeEffectMemory.PublishSound();
        ValidateEffectPresentationServices();
        NativeGraphics = graphics with { Callback = checked(graphics.Callback + 1) };
        NativeBattleEffectPresentationStep result = new(presentationContacts.ToImmutableArray(), presentationSounds.ToImmutableArray()) { Sequences = presentationSequences.ToImmutableArray(), Activations = presentationActivations.ToImmutableArray() };
        presentationContacts.Clear();
        presentationSounds.Clear();
        presentationSequences.Clear();
        presentationActivations.Clear();
        return result;
    }

    internal NativeBattlePreparedPrimitiveFrame PrepareEffectOverlay()
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (graphics.PrimitiveFrame is null || NativeEffectMemory?.PrimitiveProcess is not { } process)
        {
            throw new InvalidDataException("Native effect overlay requires its initialized primitive frame and process.");
        }
        NativeBattleLogicalOrdering pending = SubmitNativeModulePackets(graphics).PrimitiveFrame!.Pending;
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null) with { ScaleU = 0, ScaleV = 0, SpriteTileCacheControl1CCFD90 = 0 };
        var executed = NativeBattleLogicalPasses.ExecuteAdmitted(pending, resources.NativeGraphics!.PrimitiveTables!, new(0) { TextureWindowBits = 0 }, 1, cache, null, ReadNativePhysicalPrimitive);
        return new(executed.Commands, [])
        {
            LastDescriptorPerspective = true,
            LastDepthState = process.Depth,
            LastColorState = process.Color
        };
    }

    private BattleActorModels BindPresentationForeignModels(BattleActorModels models, Dictionary<BattleModelOwner, NativeBattleForeignActor>? previous = null)
    {
        foreach (NativeBattleForeignActor actor in presentationForeignActors!.Values)
        {
            if (actor.Owner == presentationSourceOwner || !models.Contains(actor.Owner))
            {
                throw new InvalidDataException("Foreign presentation requires a bound model owner outside its source.");
            }
            if ((actor.NativeModel is null) != (actor.NativeResource is null))
            {
                throw new InvalidDataException("A native foreign participant requires both its actual model state and resource owner.");
            }
            NativeBattleGraphicsModel model = previous is null && actor.NativeModel is { } native ? native with { ActiveResourceRow100 = null, OriginalResourceRow104 = null, ActiveStagedResourceRow100 = null, LinkedStagedResourceRow = null, TextureResourceIndex = null, LinkedTextureResourceIndex = null } : models.Get(actor.Owner);
            NativeBattleModelTransform transform = actor.Transform;
            short rotationX = 0;
            short rotationY = actor.Facing;
            short rotationZ = 0;
            if (previous?.TryGetValue(actor.Owner, out NativeBattleForeignActor? old) == true && model.Transform is { } current)
            {
                NativeBattleVector position = new(unchecked((short)(transform.Position28.X + current.Position28.X - old.Transform.Position28.X)), unchecked((short)(transform.Position28.Y + current.Position28.Y - old.Transform.Position28.Y)), unchecked((short)(transform.Position28.Z + current.Position28.Z - old.Transform.Position28.Z)));
                transform = transform with { Position28 = position, Vertical34 = unchecked((short)(transform.Vertical34 + current.Vertical34 - old.Transform.Vertical34)), Scale148 = current.Scale148, Scale154 = current.Scale154 };
                rotationX = model.Rotation12 ?? 0;
                rotationY = unchecked((short)(actor.Facing + (model.Rotation14 ?? old.Facing) - old.Facing));
                rotationZ = model.Rotation16 ?? 0;
            }
            uint normal = this.resources.NativeGraphics?.Rules.SummonColors?.Normal ?? throw new NotSupportedException("Foreign model presentation requires its selected normal color.");
            NativeBattleGraphicsModel bound = model with { Flags0 = (ushort)(model.Flags0 | 2), Flags2 = model.Flags2 ?? 0, Flags8 = actor.NativeModel?.Flags8 ?? actor.StatusFlags, Transform = transform, Rotation12 = rotationX, Rotation14 = rotationY, Rotation16 = rotationZ, GeometryBounds52 = actor.Geometry?.Anchors.LocalBounds, Color40 = model.Color40 ?? 0x808080, Color44 = model.Color44 ?? (normal & 0xFFFFFFu) | 0x32000000u, HasLinkedModelRing140 = false, LinkedModel = null };
            models = models.Set(actor.Owner, bound with { GeometryWorldMatrix = actor.Geometry is { } geometry ? NativeBattleGfMatrices.Compose56C2F0(NativeBattleForeignPlacement.Delta(actor, bound), geometry.World) : null });
        }
        return models;
    }
}
