using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleModelEntryFacts(string ActorId, bool IsParty, int? SceneSlot, bool Active, bool Hidden, byte? Information246, ushort SavedStatus, uint BattleStatus);

public sealed partial class BattleSession
{
    private readonly bool hostedBattlePresentation;
    private readonly ImmutableDictionary<BattleModelOwner, NativeBattleModelEntryFacts> hostedEntryFacts = ImmutableDictionary<BattleModelOwner, NativeBattleModelEntryFacts>.Empty;
    private readonly BattleModelRoster? hostedEntryRoster;
    private readonly NativeBattleModelAliases? hostedEntryAliases;
    private readonly Queue<HostedEntryDescriptor>? hostedEntryDescriptors;
    private readonly NativeBattleHostedModelPackets? hostedModelPackets;

    private sealed record HostedEntryDescriptor(ushort Command, uint Argument = 0, ushort? Index = null, ushort? Value = null, BattleEvent? Message = null, NativeBattleDescriptorModelSubject.PlannedLoad? ModelLoad = null);

    private BattleSession(BattleSession logicalOwner, BattleDefinition presentationResources)
    {
        if (!logicalOwner.UsesExpandedRoster)
        {
            throw new InvalidDataException("Hosted battle presentation requires an expanded logical roster.");
        }
        definition = NativeBattleStorageSnapshots.Capture(CloneDefinition(presentationResources));
        var (roster, aliases) = logicalOwner.ReadBattlePresentationPacketBindings();
        NativeBattleGraphicsDefinition source = definition.NativeGraphics ?? throw new InvalidDataException("Hosted entry requires its installed graphics definition.");
        NativeBattleRetainedMemory retained = definition.NativeRetainedMemory ?? throw new InvalidDataException("Hosted entry requires its retained native process memory.");
        if (definition.StageId is null || definition.NativeStageSource?.PhysicalSource is null || definition.NativeStageCameraBank is null ||
            definition.NativeEntryWaveFile is null || definition.NativeMusicProcess is null || definition.NativeGlobalSoundProcess is null ||
            definition.NativeGlobalRandom is null || retained.Stage is null || retained.EffectMemory?.Textures is null)
        {
            throw new InvalidDataException("Hosted entry lacks its actual stage, camera, wave, sound, music, random or retained graphics owner.");
        }
        hostedEntryRoster = roster;
        hostedEntryAliases = aliases;
        hostedEntryFacts = logicalOwner.CaptureHostedEntryFacts(roster);
        Dictionary<string, NativeBattleGraphicsBinding> bindings = ReadHostedEntryBindings(roster, source);
        foreach (NativeBattleModelEntryFacts facts in hostedEntryFacts.Values)
        {
            BattleCombatant actor = logicalOwner.GetActor(facts.ActorId);
            NativeBattleGraphicsBinding binding = bindings[facts.ActorId];
            int modelId = actor.IsPlayer ? actor.ModelId : actor.ModelId + 16;
            if (binding.ModelId != modelId || !facts.IsParty && facts.Active && facts.Information246 is null)
            {
                throw new InvalidDataException("Hosted entry model identity or information differs from its allocated actor.");
            }
        }

        hostedBattlePresentation = true;
        presentationCameraMode = NativeBattleEffectCameraMode.NativeCinematic;
        hostedEntryDescriptors = new();
        hostedModelPackets = new();
        combatants = [];
        byId = new(StringComparer.Ordinal);
        handlers = [];
        Combatants = combatants.AsReadOnly();
        random = NativeBattleRandomState.Initialize(0);
        nativeCrtRandom = definition.NativeCrtRandom;
        nativeBattleUiRandom = logicalOwner.nativeBattleUiRandom;
        nativeGlobalRandom = definition.NativeGlobalRandom;
        nativeEffectResourceResolver = logicalOwner.nativeEffectResourceResolver;
        NativeEntryAdvantage = logicalOwner.NativeEntryAdvantage;
        NativeCamera = NativeBattleCameraState.Initialize();
        NativeGlobalSoundProcess = definition.NativeGlobalSoundProcess;
        NativeMusicProcess = definition.NativeMusicProcess;
        InitializeNativeRetainedMemory();
        nativeActionPackets = nativeActionPackets.WithRoster(roster, aliases);
        InitializeNativePrimitiveDepth47CE10();
        InitializeNativeGraphicsProcessOwners();
        InitializeNativeGraphicsModelOwners();

        presentationForeignActors = [];
        presentationNativeTargets = [];
        NativeBattleGraphicsState graphics = new(new BattleActorModels([]), NativeBattleModelBoundary.FromRetainedMemory(retained), 0, 0, NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.ExpandableArena));
        graphics = InitializeNativeGraphicsFrame500400(graphics);
        InitializeHostedPresentationModelOwners(roster, useRetainedModelOwners: true);
        NativeGraphics = QueueHostedNativeEntryLoads(graphics);
    }

    internal static BattleSession CreateHostedBattlePresentation(BattleSession logicalOwner, BattleDefinition presentationResources)
    {
        return new(logicalOwner, presentationResources);
    }

    private ImmutableDictionary<BattleModelOwner, NativeBattleModelEntryFacts> CaptureHostedEntryFacts(BattleModelRoster roster)
    {
        var facts = ImmutableDictionary.CreateBuilder<BattleModelOwner, NativeBattleModelEntryFacts>();
        foreach (BattleModelOwner owner in roster.Owners)
        {
            BattleCombatant actor = GetActor(RequireCanonicalGraphicsActorId(owner));
            if (owner.Side != actor.Side || actor.Slot is null)
            {
                throw new InvalidDataException("Hosted entry actor has a conflicting roster binding.");
            }
            facts.Add(owner, new(actor.Id, actor.IsPlayer, actor.SceneSlot, actor.IsActive, actor.Hidden, actor.NativeInformation246, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor)));
        }
        return facts.ToImmutable();
    }

    private static Dictionary<string, NativeBattleGraphicsBinding> ReadHostedEntryBindings(BattleModelRoster roster, NativeBattleGraphicsDefinition source)
    {
        if (source.Rules is null || source.Bindings.IsDefault)
        {
            throw new InvalidDataException("Hosted entry requires its installed graphics rules and bindings.");
        }
        source.Rules.Validate();
        HashSet<string> expected = roster.Owners.Select(RequireCanonicalGraphicsActorId).ToHashSet(StringComparer.Ordinal);
        Dictionary<string, NativeBattleGraphicsBinding> bindings = new(StringComparer.Ordinal);
        foreach (NativeBattleGraphicsBinding binding in source.Bindings)
        {
            if (binding is null || binding.Resource is null || binding.InitialPosition is null || !expected.Remove(binding.ActorId) || !bindings.TryAdd(binding.ActorId, binding))
            {
                throw new InvalidDataException("Hosted entry requires one original resource and position for each allocated actor.");
            }
            binding.Resource.Validate();
            if (binding.Resource.AnimationFrames.IsDefaultOrEmpty || binding.Resource.ModelLoadSources is not ImmutableArray<NativeBattleModelDatSource> loadSources || loadSources.IsDefaultOrEmpty)
            {
                throw new InvalidDataException("Hosted entry requires the original model load and animation data for its bound actors.");
            }
        }
        if (expected.Count != 0)
        {
            throw new InvalidDataException("Hosted entry is missing an allocated actor's original resource binding.");
        }
        return bindings;
    }

    private NativeBattleModelEntryFacts RequireHostedEntryFacts(BattleModelOwner owner)
    {
        if (!hostedEntryFacts.TryGetValue(owner, out NativeBattleModelEntryFacts? facts) || !facts.Active)
        {
            throw new InvalidDataException("Hosted entry descriptor requires its active allocation identity.");
        }
        return facts;
    }

    private IEnumerable<BattleModelOwner> HostedEntryOwners(bool party)
    {
        IEnumerable<BattleModelOwner> owners = hostedEntryRoster!.Owners.Where(owner => hostedEntryFacts[owner] is { Active: true } facts && facts.IsParty == party);
        return party ? owners : owners.OrderBy(owner => hostedEntryFacts[owner].SceneSlot);
    }

    private BattleActorModels OrderHostedEntryModels(BattleActorModels models)
    {
        return new(hostedEntryRoster!.Owners.Where(models.Contains).Select(owner => new BattleModelEntry<BattleModelOwner>(owner, models.Get(owner))));
    }

    private NativeBattleGraphicsState QueueHostedNativeEntryLoads(NativeBattleGraphicsState graphics)
    {
        Queue<HostedEntryDescriptor> pending = hostedEntryDescriptors!;
        pending.Enqueue(new(1002, Index: checked((ushort)definition.StageId!.Value)));
        pending.Enqueue(new(1));
        pending.Enqueue(new(1003));
        pending.Enqueue(new(9));
        foreach (BattleModelOwner owner in HostedEntryOwners(party: false))
        {
            QueueHostedEntryModelDescriptor(102, owner);
        }
        pending.Enqueue(new(10, 0x47DD80));
        return FeedHostedEntryDescriptors(graphics);
    }

    private NativeBattleGraphicsState QueueHostedNativeEntryModels(NativeBattleGraphicsState graphics)
    {
        Queue<HostedEntryDescriptor> pending = hostedEntryDescriptors!;
        if (pending.Count != 0)
        {
            throw new InvalidDataException("Hosted entry reached its second phase before the initial descriptor list drained.");
        }
        foreach (BattleModelOwner owner in HostedEntryOwners(party: false))
        {
            QueueHostedEntryModelDescriptor(103, owner, hostedEntryFacts[owner].Hidden ? (ushort)0x101 : (ushort)1);
        }
        foreach (BattleModelOwner owner in HostedEntryOwners(party: true))
        {
            QueueHostedEntryModelDescriptor(102, owner);
        }
        pending.Enqueue(new(13));
        foreach (BattleModelOwner owner in HostedEntryOwners(party: true))
        {
            QueueHostedEntryModelDescriptor(103, owner, 0);
        }
        pending.Enqueue(new(10, 0x4A94B0));
        if (CreateNativeEntryAdvantageMessage() is { } message)
        {
            pending.Enqueue(new(8, Message: message));
        }
        pending.Enqueue(new(112));
        pending.Enqueue(new(10, 0x47DD70));
        return graphics with { EntryLifecycle = 2 };
    }

    private void QueueHostedEntryModelDescriptor(ushort command, BattleModelOwner owner, ushort? value = null)
    {
        NativeBattleModelEntryFacts facts = RequireHostedEntryFacts(owner);
        NativeBattleGraphicsBinding binding = RequireHostedEntryBinding(facts);
        NativeBattleModelSlot? slot = hostedEntryAliases!.TryEncodeExact(owner, out byte operand) ? NativeBattleModelSlot.Decode(operand) : null;
        var plan = new NativeBattleDescriptorModelSubject.PlannedLoad(owner, slot, binding);
        hostedEntryDescriptors!.Enqueue(new(command, Value: value ?? binding.ModelId, ModelLoad: plan));
    }

    private NativeBattleGraphicsBinding RequireHostedEntryBinding(NativeBattleModelEntryFacts facts)
    {
        return definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == facts.ActorId);
    }

    private void ValidateHostedEntryModelCompletion(NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleGraphicsBinding binding, bool isParty)
    {
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
        if (binding.ModelId == 143)
        {
            throw new NotSupportedException("Hosted entry model143 requires its original model142 linked-ring ownership.");
        }
        if (model.EffectCounter97 != 2 || model.ModelId4 != binding.ModelId || model.ActiveResourceRow100 is not int body || model.TextureResourceIndex != body || model.BodyTextureSlots is null)
        {
            throw new InvalidDataException("Hosted entry model completion requires its finished body resource publication.");
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new InvalidDataException("Hosted entry model completion lacks its CPU resource owner.");
        NativeBattleModelTextureCache cache = NativeModelTextureCache ?? throw new InvalidDataException("Hosted entry model completion lacks its texture alias owner.");
        RequireHostedModelLoadAlias(cpu, cache, body, owner, isParty ? (byte)1 : (byte)3, binding.ModelId);
        if (isParty && binding.ModelId != 7)
        {
            if (model.LinkedTextureSlots is null)
            {
                throw new InvalidDataException("Hosted entry party model completion requires its finished linked resource publication.");
            }
            if (binding.ModelId is not (1 or 9))
            {
                int linked = model.LinkedTextureResourceIndex ?? throw new InvalidDataException("Hosted linked model completion lacks its allocated alias.");
                RequireHostedModelLoadAlias(cpu, cache, linked, owner, 2, binding.ModelId);
            }
        }
    }

    private NativeBattleGraphicsState FeedHostedEntryDescriptors(NativeBattleGraphicsState graphics)
    {
        Queue<HostedEntryDescriptor> pending = hostedEntryDescriptors!;
        // The native pump rotates a blocked head. Append only between priority scopes to retain FIFO dispatch order.
        if (graphics.Descriptors.Depth != 0)
        {
            return graphics;
        }
        while (pending.TryPeek(out HostedEntryDescriptor? request))
        {
            NativeBattleDescriptorState descriptors = graphics.Descriptors;
            // Appearance may advance while any model completion still outside the native queue is invisible to it.
            int requiredPackets = request.Command == 13 ? pending.Count : 1;
            if (requiredPackets > descriptors.Packets.Length || requiredPackets >= descriptors.Nodes.Length)
            {
                throw new InvalidDataException("Hosted entry descriptor group exceeds the native packet and sentinel-node capacity.");
            }
            int requiredNodes = requiredPackets + (descriptors.Root is null ? 1 : 0);
            if (descriptors.Packets.Count(packet => packet.PriorityState == 0) < requiredPackets || descriptors.Nodes.Count(node => node.Data == 0) < requiredNodes)
            {
                break;
            }
            if (request.Message is { } message)
            {
                graphics = QueueNativeDescriptorMessage(graphics, message, 86);
            }
            else
            {
                (NativeBattleDescriptorState queued, int slot) = request.ModelLoad is { } model
                    ? descriptors.EnqueuePlannedLoad(request.Command, 128, request.Argument, model, request.Value!.Value)
                    : descriptors.Enqueue(request.Command, 128, request.Argument, request.Index, request.Value);
                graphics = graphics with { Descriptors = queued, EntryBarrierPacket = request.Command == 112 ? slot : graphics.EntryBarrierPacket };
            }
            pending.Dequeue();
        }
        return graphics;
    }
}
