namespace Memoria.FF8.Runtime;

public sealed record EncounterAllocationRetirement(int PhysicalIndex, bool RemovalQueued = false, bool ResourcesReleased = false)
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
            activeAction?.NativeHeader is { } activeHeader && IsAcceptedHeader(activeHeader) ||
            activeAction is not null && NativeBattleSlots.Contains(PhysicalIndex) && NativeBattleTargetRequestAdapter.MaySelectOriginalSlot(activeAction.TargetRequest, NativeBattleSlots.Decode(PhysicalIndex)) ||
            graphics.ActionActorId == allocationId || graphics.ActionActorId is not null && graphics.ActionContext is { } context && (context.Source == owner || context.TargetActors?.Contains(owner) == true || context.SourceTargets.Contains(owner)) ||
            graphics.PendingContacts.Any(IsContactOwner) || IsEffectOwner(graphics.OriginalEffect) || IsEffectOwner(detachedEffect);
    }
}

public sealed partial class BattleSession
{
    public bool HasPendingEncounterRetirements => nativeAllocations.Any(allocation => allocation.Retirement is { ResourcesReleased: false });

    private bool IsEncounterRetirementPending(int physicalIndex)
    {
        return nativeAllocations.Any(allocation => allocation.Retirement is { ResourcesReleased: false } retirement && retirement.PhysicalIndex == physicalIndex);
    }

    private void CleanupDefeatedEncounterAllocations()
    {
        foreach (NativeBattleEnemyAllocation allocation in nativeAllocations.Where(allocation => allocation.EncounterBatchId is not null && allocation.Retirement is null).ToArray())
        {
            BattleCombatant actor = GetActor(allocation.Id);
            if (!actor.IsActive || actor.IsAlive || actor.EncodeNativeSlot() is < 3 or > 6)
            {
                continue;
            }
            // A copy retains the source's body sequences, but not its scripted boss death callback.
            // Finish every accepted contact and effect before releasing this summoned allocation through105.
            if (NativeGraphics is { } graphics && new EncounterAllocationRetirement(actor.EncodeNativeSlot()).HasAcceptedConsumers(actor.Id, nativeActionPackets, graphics, ActiveAction, NativeEffectMemory?.DetachedReturnedTask))
            {
                continue;
            }
            RemoveNativeQueuedActorCommands(actor);
            RemoveEncounterQueuedActions([actor.Id]);
            nativeCommandBanks = nativeCommandBanks.RemoveActorCallbacks(checked((byte)actor.EncodeNativeSlot()), actor.Id).RemoveAllocationCallbacks(actor.Id);
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
        int physical = actor.EncodeNativeSlot();
        if (NativeGraphics is { } graphics)
        {
            BattleModelOwner owner = CreatePresentationModelOwner(actor);
            if (!graphics.NativeBindings.TryEncodeExactActor(owner, graphics.ActorModels, out byte original) || (graphics.ActorModels.Get(owner).Flags0 & 3) == 0)
            {
                return;
            }
            physical = original;
        }
        if (physical is < 3 or > 6)
        {
            return;
        }
        nativeAllocations[index] = allocation with { Retirement = new(physical, ResourcesReleased: NativeGraphics is null) };
    }

    private NativeBattleGraphicsState AdvanceEncounterAllocationRetirements(NativeBattleGraphicsState graphics)
    {
        for (int index = 0; index < nativeAllocations.Count; index++)
        {
            NativeBattleEnemyAllocation allocation = nativeAllocations[index];
            if (allocation.Retirement is not { ResourcesReleased: false } retirement)
            {
                continue;
            }
            NativeBattleModelSlot original = NativeBattleModelSlot.Decode(checked((byte)retirement.PhysicalIndex));
            BattleModelOwner owner = new BattleEnemyActorModel(new(allocation.Id));
            BattleModelOwner? current = graphics.NativeBindings.FindOriginalOwner(original, graphics.ActorModels);
            bool IsRemovalSubject(NativeBattleDescriptorModelSubject? subject) => subject is NativeBattleDescriptorModelSubject.ExplicitModel model && model.Owner == owner ||
                subject is NativeBattleDescriptorModelSubject.AuthoredOriginalSlot authored && authored.Slot == original;
            bool pendingRemoval = graphics.Descriptors.Queue.Any(slot => graphics.Descriptors.Packets[slot] is { Command: 105 } packet && IsRemovalSubject(packet.ModelSubject)) ||
                graphics.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Remove502ED0 && IsRemovalSubject(task.ModelSubject));
            if (current != owner)
            {
                if (!retirement.RemovalQueued)
                {
                    throw new InvalidDataException("Encounter retirement lost its physical allocation generation before native resource release.");
                }
                if (!pendingRemoval)
                {
                    // Native102 can bind another generation only after the previous loaded bit has cleared.
                    nativeAllocations[index] = allocation with { Retirement = retirement with { ResourcesReleased = true } };
                }
                continue;
            }
            NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
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
            if (retirement.HasAcceptedConsumers(allocation.Id, nativeActionPackets, graphics, ActiveAction, NativeEffectMemory?.DetachedReturnedTask) || graphics.ModelLoadCount != 0 || NativeModelLoader?.Task is not null)
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
            graphics = QueueNativeModelDescriptor(graphics, 105, 0, owner);
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
            NativeBattleGraphicsState? graphics = NativeGraphics;
            BattleModelOwner owner = CreatePresentationModelOwner(actor);
            bool slotValid = retirement.PhysicalIndex is >= 3 and <= 6;
            bool retainsOwner = slotValid && graphics is not null &&
                graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode((byte)retirement.PhysicalIndex), graphics.ActorModels) == owner;
            if (actor.IsActive || !slotValid || !retirement.ResourcesReleased && (graphics is null || !retainsOwner && !retirement.RemovalQueued) ||
                retirement.ResourcesReleased && retainsOwner && (graphics!.ActorModels.Get(owner).Flags0 & 3) != 0)
            {
                throw new InvalidDataException("Encounter retirement snapshot disagrees with its inactive allocation and resource generation.");
            }
        }
    }
}
