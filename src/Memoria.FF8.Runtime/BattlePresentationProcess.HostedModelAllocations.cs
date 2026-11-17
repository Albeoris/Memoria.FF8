using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    internal sealed record HostedModelLoadAdmission(HostedDispatchControl.LoadModel Load, NativeBattleGraphicsBinding Binding, ImmutableHashSet<BattleModelOwner> LaterOwners, bool UsesOriginalSceneResource)
    {
        internal BattleSourcePlacementBinding? Source { get; init; }
    }
    internal sealed record HostedModelRegistry(BattleModelRoster Roster, ImmutableDictionary<BattleModelOwner, HostedModelLoadAdmission> Loads);
    internal sealed record PreparedHostedModelRegistry(HostedModelRegistry Original, HostedModelRegistry Replacement, NativeBattleModelCpuResources Cpu, NativeBattleModelTextureCache Cache, NativeBattleEffectMemory Memory);

    private HostedModelRegistry? hostedModelRegistry;

    internal BattleModelRoster HostedPlannedModelRoster => hostedModelRegistry?.Roster ?? nativeActionPackets.Roster;



    private PreparedHostedModelRegistry? PrepareHostedModelRegistry(NativeBattleGraphicsState graphics, ImmutableArray<HostedModelLoadAdmission> additions)
    {
        if (additions.IsDefaultOrEmpty)
        {
            return null;
        }
        HostedModelRegistry original = hostedModelRegistry ?? throw new InvalidOperationException("Hosted native allocation requires its entered planned-model roster.");
        var loads = original.Loads.ToBuilder();
        foreach (HostedModelLoadAdmission admission in additions)
        {
            if (original.Roster.Contains(admission.Load.ModelOwner) || !loads.TryAdd(admission.Load.ModelOwner, admission))
            {
                throw new InvalidDataException("Hosted allocation cannot publish the same planned generation twice.");
            }
        }
        BattleModelRoster roster = new(original.Roster.Owners.Concat(additions.Select(plan => plan.Load.ModelOwner)));
        NativeBattleModelAliases aliases = graphics.NativeBindings.CaptureAliases(graphics.ActorModels, roster);
        NativeBattleModelCpuResources cpu = PrepareHostedAllocationCpu(original.Roster, roster, [], aliases);
        NativeBattleModelTextureCache cache = (NativeModelTextureCache ?? throw new InvalidOperationException("Hosted allocation requires its model texture cache.")).RebindPresentationRoster(original.Roster, roster, []);
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidOperationException("Hosted allocation requires its native effect memory.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new InvalidOperationException("Hosted allocation requires its per-model texture banks.");
        NativeBattlePcTextureWindowCache template = memory.PrimitiveProcess?.TextureWindowCache ?? throw new InvalidOperationException("Hosted allocation requires its initialized texture cache.");
        NativeBattleEffectTextureState leaf = textures with { PresentationTextures = null, Count = 0, Slots = NativeBattleEffectTextureState.FromColdProcess().Slots, Animation = null };
        foreach (HostedModelLoadAdmission admission in additions)
        {
            textures = textures.WithPresentationTexture(ReadGraphicsPresentationId(admission.Load.ModelOwner), new(leaf, NativeBattlePcTextureWindowCache.Reset4647A0(template)));
        }
        return new(original, new(roster, loads.ToImmutable()), cpu, cache, memory with { Textures = textures });
    }

    private void PublishHostedModelRegistry(PreparedHostedModelRegistry? prepared)
    {
        if (prepared is not null)
        {
            hostedModelRegistry = prepared.Replacement;
            NativeModelCpuResources = prepared.Cpu;
            NativeModelTextureCache = prepared.Cache;
            NativeEffectMemory = prepared.Memory;
        }
    }

    private NativeBattleModelCpuResources PrepareHostedAllocationCpu(BattleModelRoster expected, BattleModelRoster roster, BattleModelOwner[] retired, NativeBattleModelAliases aliases)
    {
        NativeBattleModelCpuResources owner = NativeModelCpuResources ?? throw new InvalidOperationException("Hosted allocation requires its model CPU banks.");
        NativeBattleModelCpuBanks cpu = owner.Cpu.RebindPresentationRoster(expected, roster, retired, aliases);
        int capacity = NativeBattleModelResourceRowAddress.CapacityForRoster(roster);
        ImmutableArray<NativeBattleModelCpuResourceRow> rows = owner.Rows;
        if (rows.Length < capacity)
        {
            rows = rows.AddRange(Enumerable.Range(rows.Length, capacity - rows.Length).Select(_ => NativeBattleModelCpuResourceRow.Unknown()));
        }
        return owner with { Cpu = cpu, Rows = rows };
    }

    internal bool TryCompleteEncounterModelRetirement(BattleEnemyActorModel owner, NativeBattleModelSlot? originalSlot)
    {
        RequireAdmissionWindow();
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (HasPendingModelLoad || graphics.ModelLoadCount != 0 ||
            graphics.ActorModels.TryGet(owner, out NativeBattleGraphicsModel? model) && (model.Flags0 & 3) != 0 ||
            FindFutureHostedModelWork(graphics, -1, owner) is not null || HasRunningHostedModelWork(graphics, owner) ||
            graphics.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && OwnsModel(task.ModelSubject)))
        {
            return false;
        }
        if (originalSlot.HasValue)
        {
            // Keep the dead native cell available as retained memory until its next 102 binding.
            return true;
        }
        if (hostedModelRegistry is not { } registry || !registry.Loads.TryGetValue(owner, out HostedModelLoadAdmission? load) ||
            load.Load is not HostedDispatchControl.LoadEnemy { OriginalSlot: null } || graphics.NativeBindings.CaptureAliases(graphics.ActorModels).TryEncodeExact(owner, out _))
        {
            throw new InvalidDataException("Encounter retirement lost its unaliased admitted model generation.");
        }
        BattleActorModels models = graphics.ActorModels.Contains(owner) ? graphics.ActorModels.Remove(owner) : graphics.ActorModels;
        BattleModelRoster roster = new(registry.Roster.Owners.Where(current => current != owner));
        NativeBattleModelAliases aliases = graphics.NativeBindings.CaptureAliases(models, roster);
        NativeBattleActionPacketsState packets = nativeActionPackets.WithRoster(roster, aliases);
        NativeBattleModelCpuResources cpu = PrepareHostedAllocationCpu(registry.Roster, roster, [owner], aliases);
        NativeBattleModelTextureCache cache = NativeModelTextureCache!.RebindPresentationRoster(registry.Roster, roster, [owner]);
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleEffectTextureState textures = memory.Textures!;
        memory = memory with { Textures = textures with { PresentationTextures = textures.PresentationTextures!.Remove(ReadGraphicsPresentationId(owner)) } };
        PreparedHostedModelRegistry prepared = new(registry, new(roster, registry.Loads.Remove(owner)), cpu, cache, memory);
        long epoch = checked(hostedPreparationEpoch + 1);

        PublishHostedModelRegistry(prepared);
        nativeActionPackets = packets;
        NativeGraphics = graphics with { ActorModels = models };
        hostedPreparationEpoch = epoch;
        return true;

        bool OwnsModel(NativeBattleDescriptorModelSubject? subject) => subject is NativeBattleDescriptorModelSubject.ExplicitModel model && model.Owner == owner ||
            subject is NativeBattleDescriptorModelSubject.PlannedLoad plan && plan.Owner == owner ||
            subject is NativeBattleDescriptorModelSubject.AuthoredOriginalSlot authored && authored.Slot == originalSlot;
    }

    private bool TryRebindHostedAllocationModel(NativeBattleGraphicsState graphics, NativeBattleDescriptorModelSubject.PlannedLoad plan, int descriptorSlot, out NativeBattleGraphicsState rebound)
    {
        HostedModelRegistry registry = hostedModelRegistry ?? throw new InvalidOperationException("Hosted model load requires its planned roster.");
        BattleModelOwner owner = plan.Owner;
        if (!registry.Loads.TryGetValue(owner, out HostedModelLoadAdmission? admission) ||
            plan.OriginalSlot != admission.Load.OriginalSlot || !ReferenceEquals(plan.Binding, admission.Binding))
        {
            throw new InvalidDataException("Hosted native 102 does not belong to the admitted generation.");
        }
        NativeBattleModelSlot? slot = admission.Load.OriginalSlot;
        BattleModelOwner? retired = slot is { } original ? graphics.NativeBindings.FindOriginalOwner(original, graphics.ActorModels) : null;
        if (admission.Load is HostedDispatchControl.LoadParty party)
        {
            if (slot is not null && retired != party.Outgoing || !graphics.ActorModels.Contains(party.Outgoing))
            {
                throw new InvalidDataException("Party replacement lost its outgoing model owner.");
            }
            retired = party.Outgoing;
        }
        if (retired is not null)
        {
            if (retired == owner)
            {
                throw new InvalidDataException($"Hosted native 102 already bound its incoming generation {owner}.");
            }
            if (admission.LaterOwners.Contains(retired))
            {
                throw new InvalidDataException($"Hosted native 102 would retire {retired} before its later captured work in staged operations.");
            }
            if (FindFutureHostedModelWork(graphics, descriptorSlot, retired) is { } pending)
            {
                throw new InvalidDataException($"Hosted native 102 would retire {retired} before its later captured work in queued descriptor {pending.Command} serial {pending.Serial}.");
            }
            if (HasRunningHostedModelWork(graphics, retired))
            {
                rebound = graphics;
                return false;
            }
        }

        (NativeBattleModelBoundary boundary, BattleActorModels models) = slot is { } bindingSlot
            ? graphics.NativeBindings.BindOriginalSlot(bindingSlot, owner, graphics.ActorModels)
            : retired is { } outgoing
                ? (graphics.NativeBindings, graphics.ActorModels.Remove(outgoing).Add(owner, graphics.ActorModels.Get(outgoing)))
                : throw new InvalidDataException("A model rebind requires an outgoing owner or original slot.");
        BattleModelOwner[] retiredOwners = retired is null ? [] : [retired];
        BattleModelRoster roster = retired is null ? registry.Roster : new(registry.Roster.Owners.Where(owned => owned != retired));
        NativeBattleModelAliases aliases = boundary.CaptureAliases(models, roster);
        NativeBattleActionPacketsState packets = nativeActionPackets.WithRoster(roster, aliases);
        NativeBattleModelCpuResources cpu = PrepareHostedAllocationCpu(registry.Roster, roster, retiredOwners, aliases);
        NativeBattleModelTextureCache cache = NativeModelTextureCache!.RebindPresentationRoster(registry.Roster, roster, retiredOwners);
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        if (retired is not null)
        {
            NativeBattleEffectTextureState textures = memory.Textures!;
            memory = memory with { Textures = textures with { PresentationTextures = textures.PresentationTextures!.Remove(ReadGraphicsPresentationId(retired)) } };
        }
        ImmutableDictionary<BattleModelOwner, HostedModelLoadAdmission> loads = retired is { } retiredActor ? registry.Loads.Remove(retiredActor) : registry.Loads;
        hostedModelRegistry = new(roster, loads);
        nativeActionPackets = packets;
        NativeModelCpuResources = cpu;
        NativeModelTextureCache = cache;
        NativeEffectMemory = memory;
        rebound = graphics with { NativeBindings = boundary, ActorModels = models };
        return true;
    }

    private NativeBattleDescriptorPacket? FindFutureHostedModelWork(NativeBattleGraphicsState graphics, int currentSlot, BattleModelOwner owner)
    {
        return graphics.Descriptors.Queue.Where(slot => slot != currentSlot).Select(slot => graphics.Descriptors.Packets[slot])
            .Where(packet => (packet.PriorityState & 15) == 0).FirstOrDefault(packet =>
                packet.Header is { } header && HostedHeaderUsesOwner(header, owner) ||
                packet.ModelSubject is NativeBattleDescriptorModelSubject.ExplicitModel model && model.Owner == owner ||
                packet.ModelSubject is NativeBattleDescriptorModelSubject.PlannedLoad load && load.Owner == owner);
    }

    private bool HasRunningHostedModelWork(NativeBattleGraphicsState graphics, BattleModelOwner owner)
    {
        if (hostedPresentationInvocation is { } active && HostedHeaderUsesOwner(active.Header, owner) ||
            graphics.Effects.Slots.Any(task => task.ModelOwner == owner && (task.Flags0 & 1) != 0) ||
            graphics.PendingContacts.Any(packet => HostedContactUsesOwner(packet, owner)))
        {
            return true;
        }
        for (int? slot = graphics.Auxiliary.Head; slot is int index; slot = graphics.Auxiliary.Slots[index].Next)
        {
            NativeBattleAuxiliaryTask task = graphics.Auxiliary.Slots[index];
            if (task.ModelOwner == owner || task.TargetOwner == owner)
            {
                return true;
            }
        }
        for (int? slot = graphics.ModuleEffects.Head; slot is int index; slot = graphics.ModuleEffects.Slots[index].Next)
        {
            if (graphics.ModuleEffects.Slots[index].ModelOwner == owner)
            {
                return true;
            }
        }
        if (NativeEffectMemory is { ReturnedTaskPointer1D96AAC: > 0 } memory)
        {
            NativeBattleOriginalEffectState effect = memory.DetachedReturnedTask ?? graphics.OriginalEffect
                ?? throw new InvalidDataException("Hosted native 102 found a returned model task without its live owner.");
            if (effect.Parameters is not { } parameters)
            {
                throw new InvalidDataException("Hosted native 102 found a live returned task with unknown model subjects.");
            }
            if (parameters.Source == owner || parameters.TargetActors.Contains(owner) || effect.EntryFirstRecipient == owner)
            {
                return true;
            }
        }
        return false;
    }

    private bool HostedHeaderUsesOwner(BattleActionHeaderReference reference, BattleModelOwner owner)
    {
        return nativeActionPackets.ReadHeader(reference).Source == owner || nativeActionPackets.ReadHeaderContacts(reference).Any(packet => HostedContactUsesOwner(packet, owner));
    }

    private static bool HostedContactUsesOwner(NativeBattleContactPacket packet, BattleModelOwner owner)
    {
        return packet.Primary.Recipient == owner || packet.Primary.CoverSource == owner || packet.Secondary?.Recipient == owner || packet.Secondary?.CoverSource == owner;
    }

    private NativeBattleGraphicsState ReachHostedNativeAllocationStatistics(NativeBattleGraphicsState graphics)
    {
        NativeBattleModelLifecycleServices services = hostedFrameServices ?? throw new InvalidOperationException("Hosted native 48AD10 requires its ordered receipt owner.");
        HostedNativeAllocationFrame facts = services.Allocation ?? throw new InvalidDataException("Hosted native 48AD10 has no current last allocation and scene.");
        NativeBattleDescriptorModelSubject.PlannedLoad plan = RequireHostedAllocationLoad(facts.Owner);
        uint originalParticipant = plan.OriginalSlot is { } slot ? NativeParticipantAddress(slot.Offset) : 0;
        (NativeBattleDescriptorState descriptors, int packetSlot) = graphics.Descriptors.EnqueuePlannedLoad(103, 128, originalParticipant, plan, (ushort)(1 | (facts.Hidden ? 0x100 : 0)));
        NativeBattleDescriptorPacket packet = descriptors.Packets[packetSlot];
        hostedControlPackets = hostedControlPackets.Add(new(packetSlot, packet.Serial, packet.Command, null));
        services.Emit(new NativeBattleAllocationStatisticsRequested(facts.Owner, facts.SceneSlot));
        return graphics with { Descriptors = descriptors };
    }

    private NativeBattleDescriptorModelSubject.PlannedLoad RequireHostedAllocationLoad(BattleEnemyActorModel owner)
    {
        if (hostedModelRegistry is not { } registry || !registry.Loads.TryGetValue(owner, out HostedModelLoadAdmission? admission))
        {
            throw new InvalidDataException("Hosted allocation lost its generation's model load binding.");
        }
        return new(owner, admission.Load.OriginalSlot, admission.Binding);
    }
}
