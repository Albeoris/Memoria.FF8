using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleModelEntryFacts(string ActorId, bool IsParty, int? SceneSlot, bool Active, bool Hidden, byte? Information246, ushort SavedStatus, uint BattleStatus);

public sealed partial class BattleSession
{
    private readonly bool hostedBattlePresentation;
    private readonly ImmutableArray<NativeBattleModelEntryFacts?> hostedEntryFacts;
    private readonly Queue<HostedEntryDescriptor>? hostedEntryDescriptors;
    private readonly NativeBattleHostedModelPackets? hostedModelPackets;

    private sealed record HostedEntryDescriptor(ushort Command, uint Argument = 0, ushort? Index = null, ushort? Value = null, BattleEvent? Message = null);

    private BattleSession(BattleSession logicalOwner, BattleDefinition presentationResources)
    {
        if (!logicalOwner.UsesExpandedRoster)
        {
            throw new InvalidDataException("Hosted battle presentation requires an expanded logical roster.");
        }
        definition = NativeBattleStorageSnapshots.Capture(CloneDefinition(presentationResources));
        NativeBattleGraphicsRoster roster = logicalOwner.ReadExpandedPresentationRoster();
        NativeBattleGraphicsDefinition source = definition.NativeGraphics ?? throw new InvalidDataException("Hosted entry requires its installed graphics definition.");
        NativeBattleRetainedMemory retained = definition.NativeRetainedMemory ?? throw new InvalidDataException("Hosted entry requires its retained native process memory.");
        if (definition.StageId is null || definition.NativeStageSource?.PhysicalSource is null || definition.NativeStageCameraBank is null ||
            definition.NativeEntryWaveFile is null || definition.NativeMusicProcess is null || definition.NativeGlobalSoundProcess is null ||
            definition.NativeGlobalRandom is null || retained.Stage is null || retained.EffectMemory?.Textures is null)
        {
            throw new InvalidDataException("Hosted entry lacks its actual stage, camera, wave, sound, music, random or retained graphics owner.");
        }
        hostedEntryFacts = logicalOwner.CaptureHostedEntryFacts(roster);
        Dictionary<string, NativeBattleGraphicsBinding> bindings = ReadHostedEntryBindings(roster, source);
        foreach (NativeBattleModelEntryFacts facts in hostedEntryFacts.OfType<NativeBattleModelEntryFacts>())
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
        nativeGlobalRandom = definition.NativeGlobalRandom;
        NativeEntryAdvantage = logicalOwner.NativeEntryAdvantage;
        NativeCamera = NativeBattleCameraState.Initialize();
        NativeGlobalSoundProcess = definition.NativeGlobalSoundProcess;
        NativeMusicProcess = definition.NativeMusicProcess;
        InitializeNativeRetainedMemory();
        InitializeNativePrimitiveDepth47CE10();
        InitializeNativeGraphicsProcessOwners();
        InitializeNativeGraphicsModelOwners();

        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearPresentationBank(roster, retained.ModelTransformRows, retained.ModelColors40, retained.ModelStatusTimers, retained.ModelWorldMatrices64, retained.ModelSequenceLocals).ToImmutableArray();
        presentationForeignActors = [];
        presentationNativeTargets = new();
        for (int row = 0; row < roster.ActorCount; row++)
        {
            if (roster.Rows[row] is { } actor)
            {
                presentationNativeTargets.Add(row, new(actor.ActorId, models[row], bindings[actor.ActorId].Resource) { IsParty = actor.IsParty });
            }
        }
        NativeBattleGraphicsState graphics = new(models, roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster };
        graphics = InitializeNativePrimitiveFrame500400(graphics);
        InitializeHostedPresentationModelOwners(roster, useRetainedModelOwners: true);
        NativeGraphics = QueueHostedNativeEntryLoads(graphics);
    }

    internal static BattleSession CreateHostedBattlePresentation(BattleSession logicalOwner, BattleDefinition presentationResources)
    {
        return new(logicalOwner, presentationResources);
    }

    internal NativeBattleModelLifecycleServices AdvanceHostedBattlePresentationFrame(BattleSession logicalOwner, NativeExamTimerState? timerObservation)
    {
        return AdvanceHostedBattlePresentation(logicalOwner, timerObservation);
    }

    private ImmutableArray<NativeBattleModelEntryFacts?> CaptureHostedEntryFacts(NativeBattleGraphicsRoster roster)
    {
        NativeBattleModelEntryFacts?[] facts = new NativeBattleModelEntryFacts?[roster.ActorCount];
        foreach (BattleCombatant actor in combatants.Where(actor => actor.Slot is not null))
        {
            int row = RequireExpandedPresentationRow(roster, actor);
            if (facts[row] is not null || roster[row].IsParty != actor.IsPlayer)
            {
                throw new InvalidDataException("Hosted entry actor has a conflicting roster binding.");
            }
            facts[row] = new(actor.Id, actor.IsPlayer, actor.SceneSlot, actor.IsActive, actor.Hidden, actor.NativeInformation246, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor));
        }
        return facts.ToImmutableArray();
    }

    private static Dictionary<string, NativeBattleGraphicsBinding> ReadHostedEntryBindings(NativeBattleGraphicsRoster roster, NativeBattleGraphicsDefinition source)
    {
        if (source.Rules is null || source.Bindings.IsDefault)
        {
            throw new InvalidDataException("Hosted entry requires its installed graphics rules and bindings.");
        }
        source.Rules.Validate();
        HashSet<string> expected = roster.Rows.OfType<NativeBattleGraphicsActor>().Select(actor => actor.ActorId).ToHashSet(StringComparer.Ordinal);
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

    private NativeBattleModelEntryFacts RequireHostedEntryFacts(NativeBattleGraphicsState graphics, int row)
    {
        NativeBattleGraphicsRoster roster = graphics.PresentationRoster ?? throw new InvalidDataException("Hosted entry descriptor lost its presentation roster.");
        if (!roster.ContainsRow(row) || hostedEntryFacts[row] is not { Active: true } facts || graphics.ActorIds[row] != facts.ActorId)
        {
            throw new InvalidDataException("Hosted entry descriptor requires its active allocation identity.");
        }
        return facts;
    }

    private IEnumerable<int> HostedEntryRows(bool party)
    {
        IEnumerable<int> rows = Enumerable.Range(0, hostedEntryFacts.Length).Where(row => hostedEntryFacts[row] is { Active: true } facts && facts.IsParty == party);
        return party ? rows : rows.OrderBy(row => hostedEntryFacts[row]!.SceneSlot).ThenBy(row => row);
    }

    private NativeBattleGraphicsState QueueHostedNativeEntryLoads(NativeBattleGraphicsState graphics)
    {
        Queue<HostedEntryDescriptor> pending = hostedEntryDescriptors!;
        pending.Enqueue(new(1002, Index: checked((ushort)definition.StageId!.Value)));
        pending.Enqueue(new(1));
        pending.Enqueue(new(1003));
        pending.Enqueue(new(9));
        foreach (int row in HostedEntryRows(party: false))
        {
            QueueHostedEntryModelLoad(row);
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
        foreach (int row in HostedEntryRows(party: false))
        {
            pending.Enqueue(new(103, Index: checked((ushort)row), Value: hostedEntryFacts[row]!.Hidden ? (ushort)0x101 : (ushort)1));
        }
        foreach (int row in HostedEntryRows(party: true))
        {
            QueueHostedEntryModelLoad(row);
        }
        pending.Enqueue(new(13));
        foreach (int row in HostedEntryRows(party: true))
        {
            pending.Enqueue(new(103, Index: checked((ushort)row), Value: 0));
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

    private void QueueHostedEntryModelLoad(int row)
    {
        NativeBattleModelEntryFacts facts = hostedEntryFacts[row]!;
        NativeBattleGraphicsBinding binding = RequireHostedEntryBinding(facts);
        hostedEntryDescriptors!.Enqueue(new(102, Index: checked((ushort)row), Value: binding.ModelId));
    }

    private NativeBattleGraphicsBinding RequireHostedEntryBinding(NativeBattleModelEntryFacts facts)
    {
        return definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == facts.ActorId);
    }

    private void ValidateHostedEntryModelCompletion(NativeBattleGraphicsState graphics, int row, NativeBattleGraphicsBinding binding, bool isParty)
    {
        NativeBattleGraphicsModel model = graphics.Models[row];
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
        RequireHostedModelLoadAlias(cpu, cache, body, row, isParty ? (byte)1 : (byte)3, binding.ModelId);
        if (isParty && binding.ModelId != 7)
        {
            if (model.LinkedTextureSlots is null)
            {
                throw new InvalidDataException("Hosted entry party model completion requires its finished linked resource publication.");
            }
            if (binding.ModelId is not (1 or 9))
            {
                int linked = model.LinkedTextureResourceIndex ?? throw new InvalidDataException("Hosted linked model completion lacks its allocated alias.");
                RequireHostedModelLoadAlias(cpu, cache, linked, row, 2, binding.ModelId);
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
                (NativeBattleDescriptorState queued, int slot) = descriptors.Enqueue(request.Command, 128, request.Argument, request.Index, request.Value);
                graphics = graphics with { Descriptors = queued, EntryBarrierPacket = request.Command == 112 ? slot : graphics.EntryBarrierPacket };
            }
            pending.Dequeue();
        }
        return graphics;
    }
}
