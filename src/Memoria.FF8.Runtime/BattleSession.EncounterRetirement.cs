using static Memoria.FF8.Runtime.BattlePresentationProcess;
namespace Memoria.FF8.Runtime;

public sealed record EncounterAllocationRetirement(BattleRosterSlot Slot, int? OriginalIndex, bool RemovalQueued = false, bool ResourcesReleased = false)
{
    public bool HasAcceptedConsumers(string allocationId, NativeBattleActionPacketsState packets, NativeBattleGraphicsState graphics, BattleActionPresentation? activeAction = null, NativeBattleOriginalEffectState? detachedEffect = null)
    {
        BattleModelOwner owner = new BattleEnemyActorModel(new(allocationId));
        bool IsContactOwner(NativeBattleContactPacket packet)
        {
            return packet.Primary.Recipient == owner || packet.Primary.CoverSource == owner ||
                packet.Secondary?.Recipient == owner || packet.Secondary?.CoverSource == owner;
        }
        bool IsEffectOwner(NativeBattleOriginalEffectState? effect)
        {
            return effect is { ReturnedDescriptor: > 0 } && (effect.Parameters is not { } parameters || parameters.Source == owner || parameters.TargetActors.Contains(owner));
        }
        foreach ((BattleActionHeaderReference reference, NativeBattleActionHeaderSlot header) in packets.PublishedHeaders())
        {
            if (header.Source == owner || packets.ReadHeaderContacts(reference).Any(IsContactOwner))
            {
                return true;
            }
        }
        bool IsAcceptedHeader(BattleActionHeaderReference reference)
        {
            NativeBattleActionHeaderSlot header = packets.ReadHeader(reference);
            return header.Source == owner || packets.ReadHeaderContacts(reference).Any(IsContactOwner);
        }
        return activeAction?.ActorId == allocationId || activeAction?.TargetId == allocationId ||
            activeAction?.TargetRequest.Selection is BattleTargetSelection.Explicit selected && selected.Actors.Contains(new BattleEnemyActorReference(allocationId)) ||
            activeAction?.NativeHeader is { } activeHeader && activeHeader.Ordinal < packets.ActionCount && IsAcceptedHeader(activeHeader) ||
            activeAction is not null && OriginalIndex is int original && NativeBattleTargetRequestAdapter.MaySelectOriginalSlot(activeAction.TargetRequest, NativeBattleSlots.Decode(original)) ||
            graphics.ActionActorId == allocationId || graphics.ActionActorId is not null && graphics.ActionContext is { } context &&
                (context.ResolveSource(graphics.ActorModels, graphics.NativeBindings) == owner || context.TargetActors?.Resolve(graphics.ActorModels, graphics.NativeBindings).Contains(owner) == true || context.SourceTargets.Resolve(graphics.ActorModels, graphics.NativeBindings).Contains(owner)) ||
            graphics.PendingContacts.Any(IsContactOwner) || IsEffectOwner(graphics.OriginalEffect) || IsEffectOwner(detachedEffect);
    }
}

public sealed partial class BattleSession
{
    public bool HasPendingEncounterRetirements => nativeAllocations.Any(allocation => allocation.Retirement is { ResourcesReleased: false });

    private bool IsEncounterRetirementPending(int physicalIndex)
    {
        return NativeBattleSlots.Contains(physicalIndex) && IsEncounterRetirementPending(NativeBattleSlots.Decode(physicalIndex));
    }

    private bool IsEncounterRetirementPending(BattleRosterSlot slot)
    {
        return nativeAllocations.Any(allocation => allocation.Retirement is { ResourcesReleased: false } retirement && retirement.Slot == slot);
    }

    private void CleanupDefeatedEncounterAllocations()
    {
        if (preparingExpandedHostedAction)
        {
            return;
        }
        BattlePresentationProcess? process = Presentation;
        foreach (NativeBattleEnemyAllocation allocation in nativeAllocations.Where(allocation => allocation.EncounterBatchId is not null && allocation.Retirement is null).ToArray())
        {
            BattleCombatant actor = GetActor(allocation.Id);
            if (!actor.IsActive || actor.IsAlive || actor.Slot is not { Side: BattleSide.Enemies } slot)
            {
                continue;
            }
            // A copy retains the source's body sequences, but not its scripted boss death callback.
            // Finish every accepted contact and effect before releasing this summoned allocation through105.
            if (process is not null && new EncounterAllocationRetirement(slot, actor.NativeActorIndex).HasAcceptedConsumers(actor.Id, process.ActionPackets, process.Graphics, ActiveAction, process.Memory.DetachedReturnedTask))
            {
                continue;
            }
            if (actor.NativeActorIndex is int alias)
            {
                RemoveNativeQueuedActorCommands(actor);
                nativeCommandBanks = nativeCommandBanks.RemoveActorCallbacks(checked((byte)alias), actor.Id);
            }
            RemoveEncounterQueuedActions([actor.Id]);
            nativeCommandBanks = nativeCommandBanks.RemoveAllocationCallbacks(actor.Id);
            ReleaseNativeParticipant(actor);
            RequestEncounterAllocationRetirement(actor);
            actor.IsBusy = false;
            actor.Atb = 0;
        }
    }

    private void RemoveEncounterQueuedActions(IReadOnlyCollection<string> actorIds)
    {
        BattleAttackCommand[] retained = actions.Where(command => !actorIds.Contains(command.ActorId)).ToArray();
        nativeCallbackTail?.RemoveAll(command => actorIds.Contains(command.ActorId));
        actions.Clear();
        foreach (BattleAttackCommand command in retained)
        {
            actions.Enqueue(command);
        }
    }

    private void RequestEncounterAllocationRetirement(BattleCombatant actor)
    {
        int index = nativeAllocations.FindIndex(allocation => allocation.Id == actor.Id && allocation.EncounterBatchId is not null);
        if (index < 0 || actor.IsActive)
        {
            throw new InvalidOperationException("Encounter retirement requires an inactive retained reinforcement allocation.");
        }
        NativeBattleEnemyAllocation allocation = nativeAllocations[index];
        if (allocation.Retirement is not null)
        {
            return;
        }
        if (actor.Slot is not { Side: BattleSide.Enemies } slot)
        {
            throw new InvalidDataException("Encounter retirement requires its reserved logical position.");
        }
        bool resourcesReleased = Presentation is { Graphics: { } graphics } &&
            (!graphics.ActorModels.TryGet(CreatePresentationModelOwner(actor), out NativeBattleGraphicsModel? model) || (model.Flags0 & 3) == 0);
        nativeAllocations[index] = allocation with { Retirement = new(slot, actor.NativeActorIndex, ResourcesReleased: resourcesReleased) };
    }

    private NativeBattleGraphicsState AdvanceEncounterAllocationRetirements(NativeBattleGraphicsState graphics, BattlePresentationProcess process)
    {
        for (int index = 0; index < nativeAllocations.Count; index++)
        {
            NativeBattleEnemyAllocation allocation = nativeAllocations[index];
            if (allocation.Retirement is not { ResourcesReleased: false } retirement)
            {
                continue;
            }

            NativeBattleModelSlot? original = retirement.OriginalIndex is int originalIndex ? NativeBattleModelSlot.Decode(checked((byte)originalIndex)) : null;
            BattleModelOwner owner = new BattleEnemyActorModel(new(allocation.Id));
            bool IsRemovalSubject(NativeBattleDescriptorModelSubject? subject) => subject is NativeBattleDescriptorModelSubject.ExplicitModel model && model.Owner == owner ||
                subject is NativeBattleDescriptorModelSubject.AuthoredOriginalSlot authored && authored.Slot == original;
            bool pendingRemoval = graphics.Descriptors.Queue.Any(slot => graphics.Descriptors.Packets[slot] is { Command: 105 } packet && IsRemovalSubject(packet.ModelSubject)) ||
                graphics.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Remove502ED0 && IsRemovalSubject(task.ModelSubject));
            if (!graphics.ActorModels.TryGet(owner, out NativeBattleGraphicsModel? model))
            {
                if (!retirement.RemovalQueued)
                {
                    throw new InvalidDataException("Encounter retirement lost its allocation generation before native resource release.");
                }
                if (!pendingRemoval)
                {
                    // Native102 can bind another generation only after the previous loaded bit has cleared.
                    nativeAllocations[index] = allocation with { Retirement = retirement with { ResourcesReleased = true } };
                }
                continue;
            }
            if ((model.Flags0 & 3) == 0 && !pendingRemoval)
            {
                nativeAllocations[index] = allocation with { Retirement = retirement with { ResourcesReleased = true } };
                continue;
            }
            if (retirement.RemovalQueued || pendingRemoval)
            {
                continue;
            }
            // Physical resource release follows accepted contacts and native effect completion, never their preparation.
            if (retirement.HasAcceptedConsumers(allocation.Id, process.ActionPackets, graphics, ActiveAction, process.Memory.DetachedReturnedTask) ||
                graphics.ModelLoadCount != 0 || process.HasPendingModelLoad)
            {
                continue;
            }
            if (model.Kind5 is 5 or 6 or 7)
            {
                nativeAllocations[index] = allocation with { Retirement = retirement with { RemovalQueued = true } };
                continue;
            }
            if (model.Kind5 != 0)
            {
                continue;
            }
            if (process.HasActiveAction || !process.IsControlPrefixRetired || process.PendingCompletion is not null)
            {
                continue;
            }
            PreparedHostedDispatch retirementDispatch = process.PrepareDispatch([], controls: new([new HostedDispatchControl.Model(105, owner)], []));
            process.ValidatePreparedDispatch(retirementDispatch);
            process.CommitDispatch(retirementDispatch);
            graphics = process.Graphics;
            nativeAllocations[index] = allocation with { Retirement = retirement with { RemovalQueued = true } };
        }
        return graphics;
    }

    private void ValidateEncounterRetirements()
    {
        foreach (NativeBattleEnemyAllocation allocation in nativeAllocations.Where(allocation => allocation.Retirement is not null))
        {
            EncounterAllocationRetirement retirement = allocation.Retirement!;
            BattleCombatant actor = GetActor(allocation.Id);
            bool slotValid = retirement.Slot.Side == BattleSide.Enemies && retirement.Slot.Position < ExpandedBattleFormation.MaximumEnemies &&
                retirement.OriginalIndex == NativeBattleSlots.TryEncode(retirement.Slot);
            if (actor.IsActive || !slotValid || !retirement.ResourcesReleased &&
                (actor.Slot != retirement.Slot || actor.NativeActorIndex != retirement.OriginalIndex))
            {
                throw new InvalidDataException("Encounter retirement snapshot disagrees with its inactive allocation and resource generation.");
            }
        }
    }
}
