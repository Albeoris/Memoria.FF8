using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal sealed record HostedModelLoadAdmission(HostedDispatchControl.LoadEnemy Load, NativeBattleGraphicsBinding Binding, ImmutableHashSet<BattleModelOwner> LaterOwners);
    internal sealed record HostedModelRegistry(BattleModelRoster Roster, ImmutableDictionary<BattleEnemyActorModel, HostedModelLoadAdmission> Loads);
    internal sealed record PreparedHostedModelRegistry(HostedModelRegistry Original, HostedModelRegistry Replacement, NativeBattleModelCpuResources Cpu, NativeBattleModelTextureCache Cache, NativeBattleEffectMemory Memory);

    private HostedModelRegistry? hostedModelRegistry;

    internal BattleModelRoster HostedPlannedModelRoster => hostedModelRegistry?.Roster ?? nativeActionPackets.Roster;

    private void StageHostedNativeAllocation(BattleEnemyActorModel owner, NativeBattleModelSlot slot, int sceneSlot, int modelId, bool activate)
    {
        StageExpandedHostedControl(new HostedDispatchControl.LoadEnemy(owner, slot, sceneSlot, modelId));
        StageExpandedHostedControl(new HostedDispatchControl.AllocationStatistics());
        if (activate)
        {
            StageExpandedHostedControl(new HostedDispatchControl.ActivateOriginal(slot));
        }
        StageExpandedHostedControl(new HostedDispatchControl.AllocationGate());
    }

    private void StageHostedNativeActivation(BattlePreviousTarget? target)
    {
        switch (target)
        {
            case NativeBattleTargetRequestAdapter.OriginalPreviousOperand original when NativeBattleSlots.Contains(original.Operand):
                StageExpandedHostedControl(new HostedDispatchControl.ActivateOriginal(NativeBattleModelSlot.Decode(checked((byte)original.Operand))));
                break;
            case BattlePreviousTarget.Actor captured when FindTargetActor(captured.Reference) is { } actor:
                StageExpandedHostedControl(new HostedDispatchControl.Model(113, CreatePresentationModelOwner(actor)));
                break;
            default:
                throw new NotSupportedException("Hosted native activation requires its original slot or captured actor.");
        }
    }

    private ImmutableArray<HostedModelLoadAdmission> PrepareExpandedModelLoadAdmissions(IExpandedBattlePresentationHost? host)
    {
        var unloadChecks = new List<(HostedDispatchControl.LoadEnemy Load, ImmutableHashSet<BattleModelOwner> LaterOwners)>();
        HashSet<BattleModelOwner> laterOwners = [];
        for (int i = expandedHostedOperations.Length - 1; i >= 0; i--)
        {
            switch (expandedHostedOperations[i])
            {
                case ExpandedHostedInvocation invocation:
                    laterOwners.Add(invocation.Header.Source);
                    foreach (ExpandedBattleContact contact in invocation.Contacts)
                    {
                        laterOwners.Add(contact.Packet.Primary.Recipient);
                        AddOptional(contact.Packet.Primary.CoverSource);
                        AddOptional(contact.Packet.Secondary?.Recipient);
                        AddOptional(contact.Packet.Secondary?.CoverSource);
                    }
                    break;
                case ExpandedHostedDescriptor { Control: HostedDispatchControl.Model model }:
                    laterOwners.Add(model.Owner);
                    break;
                case ExpandedHostedDescriptor { Control: HostedDispatchControl.LoadEnemy load }:
                    unloadChecks.Add((load, laterOwners.ToImmutableHashSet()));
                    break;
            }
        }
        unloadChecks.Reverse();
        return unloadChecks.Select(admission =>
        {
            (HostedDispatchControl.LoadEnemy load, ImmutableHashSet<BattleModelOwner> protectedOwners) = admission;
            BattleCombatant actor = GetActor(load.Owner.Actor.ActorId);
            NativeBattleEnemyAllocation allocation = nativeAllocations.Single(candidate => candidate.Id == actor.Id);
            if (load.Owner != CreatePresentationModelOwner(actor) || load.Slot.Side != BattleSide.Enemies ||
                actor.SceneSlot != allocation.SceneSlot || actor.SourceDefinition.SceneSlot != allocation.SceneSlot ||
                load.SceneSlot != allocation.SceneSlot || load.ModelId != actor.ModelId)
            {
                throw new InvalidDataException("A staged model load requires its exact native allocation and original scene prototype.");
            }
            NativeBattleGraphicsBinding binding = (host ?? throw new InvalidOperationException("Hosted model allocation requires its attached resource provider.")).PrepareModelBinding(this, load.Owner, NativeBattleSlots.Decode(load.Slot.Offset));
            if (binding.ActorId != actor.Id || binding.InitialPosition is null || binding.ModelId != checked((byte)(actor.ModelId + 16)) ||
                binding.Resource.AnimationFrames.IsDefaultOrEmpty || binding.Resource.ModelLoadSources is not { IsDefaultOrEmpty: false })
            {
                throw new InvalidDataException("The installed model binding does not match its staged allocation.");
            }
            binding.Resource.Validate();
            return new HostedModelLoadAdmission(load, binding, protectedOwners);
        }).ToImmutableArray();

        void AddOptional(BattleModelOwner? owner)
        {
            if (owner is not null)
            {
                laterOwners.Add(owner);
            }
        }
    }

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
            if (original.Roster.Contains(admission.Load.Owner) || !loads.TryAdd(admission.Load.Owner, admission))
            {
                throw new InvalidDataException("Hosted allocation cannot publish the same planned generation twice.");
            }
        }
        BattleModelRoster roster = new(original.Roster.Owners.Concat(additions.Select(plan => plan.Load.Owner)));
        NativeBattleModelAliases aliases = graphics.NativeBindings.CaptureAliases(graphics.ActorModels, roster);
        NativeBattleModelCpuResources cpu = PrepareHostedAllocationCpu(original.Roster, roster, [], aliases);
        NativeBattleModelTextureCache cache = (NativeModelTextureCache ?? throw new InvalidOperationException("Hosted allocation requires its model texture cache.")).RebindPresentationEnemies(original.Roster, roster, []);
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidOperationException("Hosted allocation requires its native effect memory.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new InvalidOperationException("Hosted allocation requires its per-model texture banks.");
        NativeBattlePcTextureWindowCache template = memory.PrimitiveProcess?.TextureWindowCache ?? throw new InvalidOperationException("Hosted allocation requires its initialized texture cache.");
        NativeBattleEffectTextureState leaf = textures with { PresentationTextures = null, Count = 0, Slots = NativeBattleEffectTextureState.FromColdProcess().Slots, Animation = null };
        foreach (HostedModelLoadAdmission admission in additions)
        {
            textures = textures.WithPresentationTexture(ReadGraphicsPresentationId(admission.Load.Owner), new(leaf, NativeBattlePcTextureWindowCache.Reset4647A0(template)));
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

    private NativeBattleModelCpuResources PrepareHostedAllocationCpu(BattleModelRoster expected, BattleModelRoster roster, BattleEnemyModelOwner[] retired, NativeBattleModelAliases aliases)
    {
        NativeBattleModelCpuResources owner = NativeModelCpuResources ?? throw new InvalidOperationException("Hosted allocation requires its model CPU banks.");
        NativeBattleModelCpuBanks cpu = owner.Cpu.RebindPresentationEnemies(expected, roster, retired, aliases);
        int capacity = NativeBattleModelResourceRowAddress.CapacityForRoster(roster);
        ImmutableArray<NativeBattleModelCpuResourceRow> rows = owner.Rows;
        if (rows.Length < capacity)
        {
            rows = rows.AddRange(Enumerable.Range(rows.Length, capacity - rows.Length).Select(_ => NativeBattleModelCpuResourceRow.Unknown()));
        }
        return owner with { Cpu = cpu, Rows = rows };
    }

    private bool TryRebindHostedAllocationModel(NativeBattleGraphicsState graphics, NativeBattleDescriptorModelSubject.PlannedLoad plan, int descriptorSlot, out NativeBattleGraphicsState rebound)
    {
        HostedModelRegistry registry = hostedModelRegistry ?? throw new InvalidOperationException("Hosted model load requires its planned roster.");
        if (plan.Owner is not BattleEnemyActorModel owner || !registry.Loads.TryGetValue(owner, out HostedModelLoadAdmission? admission) ||
            plan.OriginalSlot != admission.Load.Slot || !ReferenceEquals(plan.Binding, admission.Binding))
        {
            throw new InvalidDataException("Hosted native 102 does not belong to the admitted generation.");
        }
        NativeBattleModelSlot slot = admission.Load.Slot;
        BattleEnemyModelOwner? retired = graphics.NativeBindings.FindOriginalOwner(slot, graphics.ActorModels) as BattleEnemyModelOwner;
        if (retired is not null)
        {
            if (retired == owner || admission.LaterOwners.Contains(retired) || HasFutureHostedModelWork(graphics, descriptorSlot, retired))
            {
                throw new InvalidDataException("Hosted native 102 would retire an original model before its later captured work.");
            }
            if (HasRunningHostedModelWork(graphics, retired))
            {
                rebound = graphics;
                return false;
            }
        }

        (NativeBattleModelBoundary boundary, BattleActorModels models) = graphics.NativeBindings.BindOriginalSlot(slot, owner, graphics.ActorModels);
        BattleEnemyModelOwner[] retiredOwners = retired is null ? [] : [retired];
        BattleModelRoster roster = retired is null ? registry.Roster : new(registry.Roster.Owners.Where(owned => owned != retired));
        NativeBattleModelAliases aliases = boundary.CaptureAliases(models, roster);
        NativeBattleActionPacketsState packets = nativeActionPackets.WithRoster(roster, aliases);
        NativeBattleModelCpuResources cpu = PrepareHostedAllocationCpu(registry.Roster, roster, retiredOwners, aliases);
        NativeBattleModelTextureCache cache = NativeModelTextureCache!.RebindPresentationEnemies(registry.Roster, roster, retiredOwners);
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        if (retired is not null)
        {
            NativeBattleEffectTextureState textures = memory.Textures!;
            memory = memory with { Textures = textures with { PresentationTextures = textures.PresentationTextures!.Remove(ReadGraphicsPresentationId(retired)) } };
        }
        ImmutableDictionary<BattleEnemyActorModel, HostedModelLoadAdmission> loads = retired is BattleEnemyActorModel retiredActor ? registry.Loads.Remove(retiredActor) : registry.Loads;
        hostedModelRegistry = new(roster, loads);
        nativeActionPackets = packets;
        NativeModelCpuResources = cpu;
        NativeModelTextureCache = cache;
        NativeEffectMemory = memory;
        rebound = graphics with { NativeBindings = boundary, ActorModels = models };
        return true;
    }

    private bool HasFutureHostedModelWork(NativeBattleGraphicsState graphics, int currentSlot, BattleModelOwner owner)
    {
        return graphics.Descriptors.Queue.Where(slot => slot != currentSlot).Select(slot => graphics.Descriptors.Packets[slot])
            .Where(packet => (packet.PriorityState & 15) == 0).Any(packet =>
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
        NativeBattleModelSlot slot = plan.OriginalSlot!.Value;
        (NativeBattleDescriptorState descriptors, int packetSlot) = graphics.Descriptors.EnqueuePlannedLoad(103, 128, NativeParticipantAddress(slot.Offset), plan, (ushort)(1 | (facts.Hidden ? 0x100 : 0)));
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
        return new(owner, admission.Load.Slot, admission.Binding);
    }
}
