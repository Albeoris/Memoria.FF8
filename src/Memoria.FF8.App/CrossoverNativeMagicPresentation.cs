using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeMagicParticipant(int HostSlot, int ActorIndex);

internal sealed record CrossoverNativeMagicTarget(int HostSlot, NativeBattleContactHalf Contact)
{
    public static ImmutableArray<CrossoverNativeMagicTarget> Bind(int caster, IReadOnlyList<int> slots, Func<int, byte, NativeBattleContactHalf> createContact)
    {
        int foreignCount = slots.Count(slot => slot != caster);
        int capacity = NativeBattleGraphicsModels.ClearBank().Count();
        if (foreignCount >= capacity)
        {
            throw new NotSupportedException($"The native presentation model bank can represent at most {capacity - 1} foreign targets in one action.");
        }
        if (slots.Count == 0 || slots.Distinct().Count() != slots.Count)
        {
            throw new InvalidDataException("The native action requires a nonempty set of distinct target occurrences.");
        }
        int foreignIndex = foreignCount <= capacity - 3 ? 3 : 1;
        return slots.Select(slot => new CrossoverNativeMagicTarget(slot, createContact(slot, slot == caster ? (byte)0 : checked((byte)foreignIndex++)))).ToImmutableArray();
    }
}

internal sealed record CrossoverNativeMagicPresentationState(CrossoverNativeCastState Cast, NativeBattleAuxiliaryState Auxiliary, NativeBattleModuleState Modules, NativeBattleEffectTextureState Textures, NativeBattleModelTextureBinding BodyTextures, ImmutableArray<byte> TextureAnimations, NativeCrtRandomState Random, int HostTarget, NativeBattleContactHalf Contact)
{
    public ImmutableArray<CrossoverNativeMagicTarget> Targets { get; init; } = [];
    public ImmutableArray<CrossoverNativeMagicTarget> Contacts { get; init; } = [];
    public ImmutableArray<int> Participants { get; init; } = [];
    public ushort PublishedTargetMask { get; init; }
    public ImmutableArray<CrossoverNativeMagicTarget> BoundTargets => Targets.IsEmpty ? [new(HostTarget, Contact)] : Targets;
    public ImmutableArray<CrossoverNativeMagicTarget> BoundContacts => Contacts.IsEmpty ? BoundTargets : Contacts;
    public int PublishedContacts { get; init; }
    public ImmutableArray<int> PublishedContactOrdinals { get; init; } = [];
    public ushort Ability { get; init; }
    public byte Command { get; init; }
    public BattleNativeAction? Action { get; init; }
    public NativeGlobalRandomState? GlobalRandom { get; init; }
    public NativeBattleSequenceProcessGlobals? InheritedSequenceGlobals { get; init; }
    [JsonIgnore]
    public NativeBattleEffectPresentation? Effect { get; init; }
    [JsonIgnore]
    public NativeBattleArenaHost? Arena { get; init; }
    public ImmutableArray<NativeBattleModuleDraw> Draws { get; init; } = [];
    public bool ContinueBackground { get; init; }
    public bool ReinvokePending { get; init; }
    public bool Complete => !ContinueBackground && Cast.Complete && (Cast.RetainsProcess || Auxiliary.Head is null && Modules.Head is null) && EffectComplete;
    public bool EffectComplete => !ReinvokePending && (Cast.Phase >= 7 || Effect is { Invoked: true } effect && (Cast.RetainsProcess || Cast.ImmediateInvocation ? effect.ServiceComplete : effect.Complete));
    [JsonIgnore]
    public NativeBattleLogicalOrdering? Ordering => Effect?.Graphics.PrimitiveFrame?.Pending;
    [JsonIgnore]
    public NativeBattleEffectMemory? Memory => Effect?.Memory;
    public NativeBattleStageColor StageColor => Effect?.StageColor ?? default;
    public short StageDepth => StageColor.Depth;
    public short ViewTranslationY => Effect?.ViewTranslationY ?? 0;
    public ImmutableArray<CrossoverNativeMagicParticipant> ParticipantBindings { get; init; } = [];
    public ImmutableArray<CrossoverNativeMagicParticipant> BoundParticipants
    {
        get
        {
            if (!ParticipantBindings.IsEmpty)
            {
                return ParticipantBindings;
            }
            List<CrossoverNativeMagicParticipant> bound = BoundTargets.Where(target => target.Contact.ActorIndex != 0).Select(target => new CrossoverNativeMagicParticipant(target.HostSlot, target.Contact.ActorIndex)).ToList();
            foreach (int participant in Participants.Where(participant => bound.All(target => target.HostSlot != participant)))
            {
                int index = Enumerable.Range(1, 6).FirstOrDefault(index => bound.All(target => target.ActorIndex != index));
                if (index == 0)
                {
                    break;
                }
                bound.Add(new(participant, index));
            }
            return bound.ToImmutableArray();
        }
    }
    public ImmutableArray<int> SuppressedForeignSlots => Effect is { } effect ? BoundParticipants.Where(participant => (effect.Graphics.Models[participant.ActorIndex].Flags0 & 4) != 0).Select(participant => participant.HostSlot).ToImmutableArray() : [];
}

internal sealed record CrossoverNativeMagicPresentationStep(CrossoverNativeMagicPresentationState State, bool Contact, ImmutableArray<NativeBattleModuleDraw> Sounds)
{
    public ImmutableArray<int> ContactTargets { get; init; } = [];
    public ImmutableArray<int> ContactOrdinals { get; init; } = [];
    public ImmutableArray<NativeBattleModuleSound> EffectSounds { get; init; } = [];
    public ImmutableArray<NativeBattleForeignSequenceRequest> Sequences { get; init; } = [];
}

internal sealed class CrossoverNativePreflightParticipants
{
    private readonly Dictionary<(int Slot, int Index), NativeBattleForeignActor> actors = [];

    internal static NativeBattleForeignActor Read(CrossoverNativePreflightParticipants? snapshot, int slot, int index, Func<NativeBattleForeignActor> read)
    {
        if (snapshot is null)
        {
            return read();
        }
        if (!snapshot.actors.TryGetValue((slot, index), out NativeBattleForeignActor? actor))
        {
            actor = read();
            snapshot.actors.Add((slot, index), actor);
        }
        return actor;
    }
}

/// <summary>Original command casting and the shared FFVIII effect dispatcher on the host presentation clock.</summary>
internal sealed class CrossoverNativeMagicPresentation(GameAssets game, NativeBattleGraphicsResource resource, BattleAssets battle, Func<ImmutableArray<int>>? readParticipants = null, Func<int, int, NativeBattleForeignActor>? readParticipantGeometry = null, Func<NativeBattleEffectSceneContext>? readSceneContext = null, Func<int>? readPartyActorCount = null, ImmutableArray<BattleNativeRenzokukenResource> sourcePrograms = default)
{
    private const int MaximumProbeCallbacks = 4096;
    private readonly ImmutableArray<NativeBattleModuleResource> sprites = game.ReadNativeBattleModuleResources();

    public CrossoverNativeMagicPresentationState Begin(NativeBattleGraphicsModel source, int hostTarget, NativeCrtRandomState random, NativeBattleContactHalf contact, BattleNativeAction action, NativeGlobalRandomState? globalRandom = null, NativeBattleArenaHost? arena = null, NativeBattleEffectPresentation? retained = null, ImmutableArray<CrossoverNativeMagicParticipant> participantBindings = default, CrossoverNativeMagicPresentationState? processState = null, NativeBattleEffectTextureState? sourceTextures = null, NativeBattleEffectMemory? sourceMemory = null)
    {
        if (hostTarget is < 0 or > 9 || contact.ActorIndex >= 7)
        {
            throw new InvalidDataException("The original presentation requires its bound source or host target occurrence.");
        }
        BattleNativePresentationInvocation invocation = action.ReadPresentationInvocation();
        byte command = invocation.Command;
        NativeBattleGraphicsResource currentResource = retained?.SourceResource ?? resource;
        if (retained is not null)
        {
            source = retained.Source;
        }
        byte sequence = invocation.SourceSequence ?? NativeBattleActionGraphics.ResolvePlayerSequence(command, 0, action.ActionId, game.ReadNativeBattleCommandSequences());
        NativeBattleActionPacketsState headerPackets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
        (headerPackets, int header) = headerPackets.Prepare(new(0, command, sequence, -1, invocation.Argument, action.MagicId, null));
        uint service = NativeBattleActionGraphics.SelectService(headerPackets.Headers[header]).Callback;
        NativeBattleEffectTextureState textures;
        if (sourceTextures is not null)
        {
            textures = sourceTextures;
        }
        else if (retained is null)
        {
            (source, textures) = CrossoverNativeActorTextures.Allocate(source, currentResource);
        }
        else
        {
            textures = retained.Memory.Textures ?? throw new InvalidDataException("The native Limit process lost its texture owner.");
        }
        source = NativeBattlePoseMatrices.PublishShadowBounds5086F0(source, currentResource.PoseSource ?? throw new NotSupportedException("Native source presentation requires its posed skeleton."));
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        source = source with { Color40 = source.Color40 ?? colors.Transition, Color44 = source.Color44 ?? colors.Normal };
        if (retained is null && sourceTextures is null)
        {
            NativeBattleEffectInitializerAsset common = game.ReadNativeCommonEffectTexture();
            (textures, _) = textures.EnqueueTim505E30(new(common.Name, common.Data));
            textures = textures.Drain505D20().State;
        }
        CrossoverNativeCastState cast = CrossoverNativeCastPresentation.Begin(source, currentResource, game.ReadNativeBattleCommandSequences(), command, action.ActionId, service);
        return new(cast, processState?.Auxiliary ?? NativeBattleAuxiliaryState.Clear(), processState?.Modules ?? NativeBattleModuleState.Clear(), textures, source.BodyTextureSlots!, currentResource.TextureAnimations ?? throw new InvalidDataException("Native casting requires the source texture animation table."), random, hostTarget, contact) { Ability = action.ActionId, Command = command, Action = action, GlobalRandom = globalRandom, Arena = retained?.Arena ?? arena, Participants = readParticipants?.Invoke() ?? [], Effect = retained, ReinvokePending = retained is not null, ParticipantBindings = participantBindings.IsDefault ? [] : participantBindings, InheritedSequenceGlobals = sourceMemory is null ? null : new(sourceMemory.SequenceSine, sourceMemory.SequenceCosine) };
    }

    internal static ImmutableArray<int> ReadContactOrdinals(IReadOnlyList<CrossoverNativeMagicTarget> contacts, IReadOnlyList<int> published, ImmutableArray<NativeBattleContactPacket> packets)
    {
        HashSet<int> settled = published.ToHashSet();
        if (settled.Count != published.Count || published.Any(ordinal => ordinal < 0 || ordinal >= contacts.Count))
        {
            throw new InvalidDataException("The native presentation lost its published occurrence ownership.");
        }
        ImmutableArray<int>.Builder ordinals = ImmutableArray.CreateBuilder<int>();
        foreach (NativeBattleContactPacket packet in packets)
        {
            if (packet.Secondary.ActorIndex != 255)
            {
                throw new NotSupportedException("The native presentation requires a secondary contact operation that this host action cannot represent.");
            }
            int ordinal = packet.PhysicalContactIndex ?? throw new InvalidDataException("The native process lost its physical contact identity.");
            if (ordinal < 0 || ordinal >= contacts.Count || contacts[ordinal].Contact.ActorIndex != packet.Primary.ActorIndex)
            {
                throw new InvalidDataException("The native process published an unowned physical contact occurrence.");
            }
            if (!settled.Add(ordinal))
            {
                throw new InvalidDataException("The native process repeated a physical contact occurrence.");
            }
            ordinals.Add(ordinal);
        }
        return ordinals.ToImmutable();
    }

    public bool TryPreflight(CrossoverNativeMagicPresentationState source, Func<NativeBattleModuleAnchor> readTargetAnchor, Func<NativeBattleModelTransform> readTargetTransform, NativeBattleCameraProjection projection, Func<CrossoverNativeForeignTarget>? readForeign, out string? reason, Func<int, CrossoverNativeForeignTarget>? readGroupForeign = null)
    {
        NativeBattleEffectPresentation? retained = source.Effect?.Fork();
        CrossoverNativeMagicPresentationState probe = source with { Cast = retained is null ? source.Cast : source.Cast with { Model = retained.Source }, Auxiliary = retained is null ? source.Auxiliary : NativeBattleEffectComponentStorage.Restore(NativeBattleEffectComponentStorage.Capture(source.Auxiliary)), Modules = retained is null ? source.Modules : NativeBattleEffectComponentStorage.Restore(NativeBattleEffectComponentStorage.Capture(source.Modules)), Effect = retained, Arena = retained?.Arena ?? source.Arena?.Fork(), Textures = retained?.Memory.Textures ?? NativeBattleEffectComponentStorage.Restore(NativeBattleEffectComponentStorage.Capture(source.Textures)) };
        CrossoverNativePreflightParticipants participants = new();
        try
        {
            for (int callback = 0; callback < MaximumProbeCallbacks; callback++)
            {
                probe = Advance(probe, readTargetAnchor, readTargetTransform, projection, readForeign, readGroupForeign, participants).State;
                if (probe.Complete)
                {
                    reason = probe.Action?.PresentationOnly == true || probe.PublishedContacts == probe.BoundContacts.Length ? null : "The native presentation did not publish every planned contact occurrence.";
                    return reason is null;
                }
            }
            reason = $"The native presentation did not complete within {MaximumProbeCallbacks} callbacks using the available host services.";
            return false;
        }
        catch (Exception error) when (error is NotSupportedException or InvalidDataException or InvalidOperationException or OverflowException or ArgumentException)
        {
            reason = error.InnerException is null ? error.Message : $"{error.Message} {error.GetBaseException().Message}";
            return false;
        }
        finally
        {
            probe.Effect?.Dispose();
            probe.Arena?.ClearPresentation();
        }
    }

    public CrossoverNativeMagicPresentationStep Advance(CrossoverNativeMagicPresentationState source, Func<NativeBattleModuleAnchor> readTargetAnchor, Func<NativeBattleModelTransform> readTargetTransform, NativeBattleCameraProjection? projection = null, Func<CrossoverNativeForeignTarget>? readForeign = null, Func<int, CrossoverNativeForeignTarget>? readGroupForeign = null)
    {
        return Advance(source, readTargetAnchor, readTargetTransform, projection, readForeign, readGroupForeign, null);
    }

    private CrossoverNativeMagicPresentationStep Advance(CrossoverNativeMagicPresentationState source, Func<NativeBattleModuleAnchor> readTargetAnchor, Func<NativeBattleModelTransform> readTargetTransform, NativeBattleCameraProjection? projection, Func<CrossoverNativeForeignTarget>? readForeign, Func<int, CrossoverNativeForeignTarget>? readGroupForeign, CrossoverNativePreflightParticipants? participants)
    {
        if (source.Complete)
        {
            return new(source, false, []);
        }
        BattleNativeAction action = source.Action ?? throw new NotSupportedException("The selected presentation has no original kernel action record.");
        CrossoverNativeCastState cast = source.Cast;
        NativeBattleAuxiliaryState auxiliary = source.Auxiliary;
        NativeBattleModuleState modules = source.Modules;
        NativeBattleEffectTextureState textures = source.Textures;
        NativeCrtRandomState random = source.Random;
        ImmutableArray<byte> motion = source.TextureAnimations;
        NativeBattleEffectPresentation? effect = source.Effect;
        if ((cast.GuardianInvocation || cast.ServiceCallback == 0x50A9A0) && effect is null)
        {
            PrepareEffect(true);
        }
        cast = CrossoverNativeCastPresentation.SelectPendingSequence(cast, effect?.SourceResource ?? resource);
        CrossoverNativeCastPoll poll = CrossoverNativeCastPresentation.Poll(cast, modules.PublishedCount != 0, source.EffectComplete);
        cast = poll.State;
        if (poll.InvokeEffect)
        {
            if (effect is null || source.ReinvokePending)
            {
                PrepareEffect(false);
            }
            else
            {
                effect.Invoke(cast.Model, random, textures);
                cast = cast with { Model = effect.Source };
                textures = effect.Memory.Textures!;
                random = effect.Random;
            }
        }
        if (poll.ReloadSourceResources)
        {
            (effect ?? throw new InvalidDataException("Native invocation lost its source resource owner.")).ReloadSourceResources();
            cast = cast with { Model = effect.Source };
            textures = effect.Memory.Textures!;
        }

        auxiliary = auxiliary.Advance(new()
        {
            ReadModel = ReadSource,
            ApplyTexture = (_, selector, variant) => ApplyTexture(selector, variant),
            RestoreTextureBlink = actor => cast = cast with { Model = ReadSource(actor) with { Flags0 = (ushort)(cast.Model.Flags0 | 0x400) } }
        }).State;
        NativeBattleModuleStep shared = modules.Advance(new(0)
        {
            Resources = sprites,
            CrtRandom = random,
            ReadModelAnchor = actor => NativeBattlePoseMatrices.ReadAnchor(ReadSource(actor), (effect?.SourceResource ?? resource).PoseSource!),
            ReadModelTransform = actor => ReadSource(actor).Transform!,
            ReadModelBone = (actor, bone, fraction) => NativeBattlePoseMatrices.ReadBone(ReadSource(actor), (effect?.SourceResource ?? resource).PoseSource!, bone, fraction),
            TransformModelPosition = (actor, local) => NativeBattlePoseMatrices.TransformModulePosition570CC0(ReadSource(actor), local)
        });
        modules = shared.State;
        random = shared.CrtRandomAfter!;
        ImmutableArray<NativeBattleModuleDraw> draws = shared.Draws.Where(draw => draw.Kind != NativeBattleModuleDrawKind.Sound).ToImmutableArray();
        ImmutableArray<NativeBattleModuleSound> effectSounds = [];
        ImmutableArray<NativeBattleForeignSequenceRequest> sequences = [];
        bool contact = false;
        ImmutableArray<int>.Builder contactTargets = ImmutableArray.CreateBuilder<int>();
        ImmutableArray<int>.Builder contactOrdinals = ImmutableArray.CreateBuilder<int>();
        ImmutableArray<int> previousOrdinals = source.PublishedContactOrdinals.IsEmpty && source.PublishedContacts != 0 ? Enumerable.Range(0, source.PublishedContacts).ToImmutableArray() : source.PublishedContactOrdinals;
        if (previousOrdinals.Length != source.PublishedContacts)
        {
            throw new InvalidDataException("The native presentation contact count conflicts with its settled occurrence identities.");
        }
        ushort published = source.PublishedTargetMask;
        if (effect is { Invoked: true, Complete: false })
        {
            CollectNativeStep(effect.Advance(cast.Model, random, projection ?? throw new NotSupportedException("Native effects require the current host camera."), ForeignActors(), textures));
        }
        if (cast.GuardianInvocation)
        {
            NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
            NativeBattleGraphicsModel model = NativeBattleGraphicsModels.AdvanceAppearance(cast.Model, false, colors);
            if (model.Kind5 == 3)
            {
                model = NativeBattleModelFade.Advance50C410(model, false, colors);
            }
            if (model.Kind5 == 4)
            {
                model = NativeBattleModelFade.Advance50C560(model, false, colors);
            }
            cast = cast with { Model = NativeBattleSummonModels.Advance(model, false, colors) };
        }
        NativeBattleGraphicsResource currentResource = effect?.SourceResource ?? resource;
        (cast, _) = CrossoverNativeCastPresentation.AdvanceModel(cast, currentResource, new(0, 0)
        {
            GlobalRandom = effect is null ? null : () => effect.NextGlobalRandom(),
            ReadModelExternal = effect is null ? null : (current, variable) => effect.ReadSourceVariable(current, variable),
            WriteExternal = effect is null ? null : (variable, value) => effect.WriteSourceVariable(variable, value),
            WriteModelExternal = effect is null ? null : (current, variable, value) => effect.WriteSourceTargetVariable(current, variable, value),
            ScheduleTextureAnimation = (program, sequence) => auxiliary = auxiliary.AppendTextureAnimation(0, sequence, program),
            ApplyTexture = ApplyTexture,
            ScheduleModelEffect = selected => modules = modules.AppendModelEffect(0, selected),
            AdvanceTexture = (_, selector) =>
            {
                NativeBattleTextureMotionStep step = NativeBattleTextureMotion.Advance50C950(motion, cast.Model.TextureAllocationMask, selector, true);
                motion = step.Source;
                foreach (NativeBattleTextureCopy copy in step.Copies)
                {
                    Copy(copy);
                }
            }
        }, resource);
        effect?.PublishSource(cast.Model);
        if (cast.GuardianInvocation && cast.Complete && effect is { Invoked: true, Complete: true })
        {
            CollectNativeStep(effect.CompleteInvocation(), false);
        }
        textures = textures.Drain505D20().State;
        return new(source with { Cast = cast, Auxiliary = auxiliary, Modules = modules, Textures = textures, TextureAnimations = motion, Random = random, GlobalRandom = effect?.GlobalRandom ?? source.GlobalRandom, Effect = effect, BodyTextures = cast.Model.BodyTextureSlots ?? source.BodyTextures, Draws = draws, PublishedContacts = source.PublishedContacts + contactOrdinals.Count, PublishedContactOrdinals = previousOrdinals.AddRange(contactOrdinals), PublishedTargetMask = published, ReinvokePending = false }, contact, shared.Draws.Where(draw => draw.Kind == NativeBattleModuleDrawKind.Sound).ToImmutableArray()) { EffectSounds = effectSounds, ContactTargets = contactTargets.ToImmutable(), ContactOrdinals = contactOrdinals.ToImmutable(), Sequences = sequences };

        void CollectNativeStep(NativeBattleEffectPresentationStep next, bool advanceSource = true)
        {
            NativeBattleEffectPresentation current = effect ?? throw new InvalidDataException("The native presentation lost its process owner.");
            if (advanceSource)
            {
                cast = cast with { Model = current.Source };
                textures = current.Memory.Textures!;
                random = current.Random;
                draws = draws.AddRange(current.Graphics.ModuleDraws.Where(draw => draw.Kind != NativeBattleModuleDrawKind.Sound));
            }
            contact |= !action.PresentationOnly && !next.Contacts.IsEmpty;
            foreach (int ordinal in action.PresentationOnly ? [] : ReadContactOrdinals(source.BoundContacts, previousOrdinals.AddRange(contactOrdinals), next.Contacts))
            {
                CrossoverNativeMagicTarget target = source.BoundContacts[ordinal];
                published |= (ushort)(1 << target.HostSlot);
                contactTargets.Add(target.HostSlot);
                contactOrdinals.Add(ordinal);
            }
            effectSounds = effectSounds.AddRange(next.Sounds);
            sequences = sequences.AddRange(next.Sequences);
        }

        void PrepareEffect(bool defer)
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
            BattleNativePresentationInvocation invocation = action.ReadPresentationInvocation();
            byte command = invocation.Command;
            byte sourceSequence = invocation.SourceSequence ?? NativeBattleActionGraphics.ResolvePlayerSequence(command, 0, action.ActionId, game.ReadNativeBattleCommandSequences());
            (packets, _) = packets.Prepare(new(0, command, sourceSequence, -1, invocation.Argument, action.MagicId, null));
            ushort targetMask = 0;
            foreach (CrossoverNativeMagicTarget target in source.BoundContacts)
            {
                (packets, _) = packets.AppendContact(new(target.Contact, action.PresentationSecondarySource ? new(0, 0, 1, 0, 0, 0, 0) : new(255, 0, 0, 0, 0, 0, 0)));
                targetMask |= (ushort)(1 << target.Contact.ActorIndex);
            }
            (packets, _) = packets.Publish(checked((byte)source.BoundContacts.Length));
            NativeBattleEffectParameters parameters = new(0, 0, targetMask, 0, 0, 0) { Parameter80 = command is 11 or 14 ? (byte)0 : null };
            if (effect is not null && source.ReinvokePending)
            {
                effect.ReinvokeDirect(packets, cast.Model, random, textures, parameters, ForeignActors());
                cast = cast with { Model = effect.Source };
                textures = effect.Memory.Textures!;
                random = effect.Random;
                return;
            }
            NativeBattleGraphicsDefinition graphics = new(game.ReadNativeBattleGraphicsRules(), [new("source", checked((byte)cast.Model.ModelId4!.Value), resource)], []) { PrimitiveTables = battle.ReadNativePrimitiveTables(), SoftwarePrimitiveTable = battle.ReadNativeSoftwarePrimitiveTable(), PrimitiveCapabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8() };
            BattleDefinition resources = new() { NativeGraphics = graphics, NativeModuleResources = sprites, NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([action.MagicId]), NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(), NativeCommandSequences = game.ReadNativeBattleCommandSequences(), NativeGlobalRandom = source.GlobalRandom, NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeRenzokukenResources = sourcePrograms.IsDefault ? [] : sourcePrograms };
            effect = new(new(resources, cast.Model, resource, packets, parameters, action.MagicId, textures, random, projection ?? throw new NotSupportedException("Native effects require the current host camera."), ForeignActors())
            {
                Arena = source.Arena,
                SceneContext = readSceneContext?.Invoke() ?? new(NativeBattleSummonFloorMode.BelowTargetPlane, NativeBattleTargetBoundsMode.AllContacts) { MirroredSurfaceMode = NativeBattleMirroredSurfaceMode.InitialCapture },
                RibbonProjectionPolicy = NativeBattleRibbonProjectionPolicy.IndependentGeometricRuns,
                ModelTransformPolicy = NativeBattleEffectModelTransformPolicy.HostActorRelative,
                DeferInvocation = defer,
                PartyActorCount = readPartyActorCount?.Invoke() ?? (readParticipants is null ? null : source.Participants.Length + 1),
                InitialMusicGain = 127,
                SequenceGlobals = source.InheritedSequenceGlobals
            });
            cast = cast with { Model = effect.Source };
            textures = effect.Memory.Textures!;
            random = effect.Random;
        }

        ImmutableArray<NativeBattleForeignActor> ForeignActors()
        {
            ImmutableArray<NativeBattleForeignActor>.Builder actors = ImmutableArray.CreateBuilder<NativeBattleForeignActor>();
            foreach (CrossoverNativeMagicTarget target in source.BoundTargets)
            {
                if (target.Contact.ActorIndex == 0)
                {
                    continue;
                }
                Func<CrossoverNativeForeignTarget>? geometry = readGroupForeign is not null ? () => readGroupForeign(target.HostSlot) : readForeign;
                if (geometry is not null)
                {
                    actors.Add(CrossoverNativePreflightParticipants.Read(participants, target.HostSlot, target.Contact.ActorIndex, () =>
                    {
                        CrossoverNativeForeignTarget foreign = geometry();
                        return foreign.ToActor(target.Contact.ActorIndex) with { ReadGeometry = () => foreign.Anchors };
                    }));
                    continue;
                }
                if (readParticipantGeometry is not null)
                {
                    actors.Add(CrossoverNativePreflightParticipants.Read(participants, target.HostSlot, target.Contact.ActorIndex, () => readParticipantGeometry(target.HostSlot, target.Contact.ActorIndex)));
                    continue;
                }
                NativeBattleModuleAnchor anchor = readTargetAnchor();
                NativeBattleModelTransform transform = readTargetTransform();
                actors.Add(new(target.Contact.ActorIndex, transform, anchor, (selector, fraction) => CrossoverNativeForeignTarget.ReadSemanticBone(selector, fraction, new(anchor.X, anchor.Y, anchor.Z), () => throw new NotSupportedException("The host target has no posed root geometry provider."))));
            }
            foreach (CrossoverNativeMagicParticipant participant in source.BoundParticipants.Where(participant => source.BoundTargets.All(target => target.HostSlot != participant.HostSlot)))
            {
                actors.Add(CrossoverNativePreflightParticipants.Read(participants, participant.HostSlot, participant.ActorIndex, () => (readParticipantGeometry ?? throw new NotSupportedException("The host has no geometry owner for its other presentation participants."))(participant.HostSlot, participant.ActorIndex)));
            }
            return actors.ToImmutable();
        }

        NativeBattleGraphicsModel ReadSource(int actor) => actor == 0 ? cast.Model : throw new NotSupportedException("The source casting program requested an unbound actor.");

        void ApplyTexture(byte selector, int variant)
        {
            if (NativeBattleTextureMotion.Plan50C780(motion, cast.Model.TextureAllocationMask, selector, variant) is { } copy)
            {
                Copy(copy);
            }
        }

        void Copy(NativeBattleTextureCopy copy)
        {
            textures = textures.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY);
        }
    }

    public static CrossoverNativeMagicPresentationState Capture(CrossoverNativeMagicPresentationState source)
    {
        if (source.Effect is not null)
        {
            throw new NotSupportedException("Live crossover battle presentation cannot be saved.");
        }
        return source with { Textures = NativeBattleEffectComponentStorage.Capture(source.Textures) };
    }

    public static CrossoverNativeMagicPresentationState Restore(CrossoverNativeMagicPresentationState source)
    {
        if (source.Effect is not null || source.Cast.Phase == 6)
        {
            throw new NotSupportedException("This live guest effect uses a previous execution continuation contract. Resume a field or world save instead.");
        }
        return source with { Textures = NativeBattleEffectComponentStorage.Restore(source.Textures) };
    }
}
