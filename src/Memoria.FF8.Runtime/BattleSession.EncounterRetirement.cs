namespace Memoria.FF8.Runtime;

public sealed record EncounterAllocationRetirement(int PhysicalIndex, bool RemovalQueued = false, bool ResourcesReleased = false)
{
    public bool HasAcceptedConsumers(string allocationId, NativeBattleActionPacketsState packets, NativeBattleGraphicsState graphics, BattleActionPresentation? activeAction = null, NativeBattleOriginalEffectState? detachedEffect = null)
    {
        bool IsContactOwner(NativeBattleContactPacket packet)
        {
            return packet.Primary.ActorIndex == PhysicalIndex || packet.Secondary.ActorIndex == PhysicalIndex;
        }
        bool IsEffectOwner(NativeBattleOriginalEffectState? effect)
        {
            return effect is { ReturnedDescriptor: > 0 } && (effect.Parameters is not { } parameters || parameters.Actor78 == PhysicalIndex || (parameters.TargetMask7A & (1 << PhysicalIndex)) != 0);
        }
        for (int header = 0; header < packets.ActionCount; header++)
        {
            if (packets.Headers[header].ReadByte(0) == PhysicalIndex || packets.ReadHeaderContacts506BA0(header).Any(IsContactOwner))
            {
                return true;
            }
        }
        return activeAction?.ActorId == allocationId || activeAction?.TargetId == allocationId || activeAction is not null && (activeAction.NativeTargetMask & (1 << PhysicalIndex)) != 0 || graphics.ActionActorId == allocationId || graphics.ActionActorId is not null && graphics.ActionContext is { } context && (context.ActorIndex == PhysicalIndex || (context.PhysicalTargetMask.GetValueOrDefault() & (1 << PhysicalIndex)) != 0) || graphics.PendingContacts.Any(IsContactOwner) || IsEffectOwner(graphics.OriginalEffect) || IsEffectOwner(detachedEffect);
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
            if (!actor.IsActive || actor.IsAlive || actor.NativeIndex is < 3 or > 6)
            {
                continue;
            }
            // A copy retains the source's body sequences, but not its scripted boss death callback.
            // Finish every accepted contact and effect before releasing this summoned allocation through105.
            if (NativeGraphics is { } graphics && new EncounterAllocationRetirement(actor.NativeIndex).HasAcceptedConsumers(actor.Id, nativeActionPackets, graphics, ActiveAction, NativeEffectMemory?.DetachedReturnedTask))
            {
                continue;
            }
            RemoveNativeQueuedActorCommands(actor);
            RemoveEncounterQueuedActions([actor.Id]);
            nativeCommandBanks = nativeCommandBanks.RemoveActorCallbacks(checked((byte)actor.NativeIndex), actor.Id).RemoveAllocationCallbacks(actor.Id);
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
        int physical = actor.NativeIndex is >= 3 and <= 6 ? actor.NativeIndex : NativeGraphics?.ActorIds.IndexOf(actor.Id) ?? -1;
        if (physical is < 3 or > 6 || NativeGraphics is { } graphics && (graphics.Models[physical].Flags0 & 3) == 0)
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
            int physical = retirement.PhysicalIndex;
            NativeBattleGraphicsModel model = graphics.Models[physical];
            bool pendingRemoval = graphics.Descriptors.Queue.Any(slot => graphics.Descriptors.Packets[slot].Command == 105 && graphics.Descriptors.Packets[slot].Index == physical) || graphics.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Remove502ED0 && graphics.Descriptors.Packets[task.PacketSlot].Index == physical);
            if (graphics.ActorIds[physical] != allocation.Id)
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
            graphics = QueueNativeDescriptor(graphics, 105, 0, checked((ushort)physical));
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
            if (actor.IsActive || retirement.PhysicalIndex is < 3 or > 6 || !retirement.ResourcesReleased && (NativeGraphics is null || NativeGraphics.ActorIds[retirement.PhysicalIndex] != allocation.Id && !retirement.RemovalQueued) || retirement.ResourcesReleased && NativeGraphics is { } graphics && graphics.ActorIds[retirement.PhysicalIndex] == allocation.Id && (graphics.Models[retirement.PhysicalIndex].Flags0 & 3) != 0)
            {
                throw new InvalidDataException("Encounter retirement snapshot disagrees with its inactive allocation and resource generation.");
            }
        }
    }
}
