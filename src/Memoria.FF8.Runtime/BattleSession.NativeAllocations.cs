namespace Memoria.FF8.Runtime;

/// <summary>A distinct native allocation of one immutable scene resource, including earlier released allocations.</summary>
public sealed record NativeBattleEnemyAllocation(string Id, int SceneSlot, int Generation);

public sealed partial class BattleSession
{
    private readonly List<NativeBattleEnemyAllocation> nativeAllocations = [];

    public BattleCombatantDefinition GetCombatantDefinition(string id)
    {
        return CloneDefinition(definition with { Combatants = [GetActor(id).SourceDefinition] }).Combatants[0];
    }

    private BattleCombatant AllocateNativeEnemy(BattleCombatant caller, int sceneSlot, int requestedIndex, bool activate)
    {
        BattleCombatantDefinition prototype = definition.Combatants.SingleOrDefault(candidate => !candidate.IsPlayer && candidate.SceneSlot == sceneSlot) ?? throw new InvalidDataException($"Native enemy allocation has no scene resource {sceneSlot}.");
        NativeBattleEnemyTemplate template = prototype.NativeTemplate ?? throw new InvalidDataException("Native enemy allocation lacks its installed resource template.");
        int physicalIndex = requestedIndex == 0 ? Enumerable.Range(3, 4).FirstOrDefault(index => !combatants.Any(candidate => candidate.IsActive && candidate.NativeIndex == index), -1) : requestedIndex;
        if (physicalIndex is < 3 or > 6)
        {
            throw new InvalidDataException("Native enemy allocation requires one of physical slots3..6 and an available allocation.");
        }
        StopTurnAdvance();
        BattleCombatant? previous = combatants.SingleOrDefault(candidate => !candidate.IsPlayer && candidate.NativeIndex == physicalIndex);
        foreach (BattleCombatant replaced in combatants.Where(candidate => !candidate.IsPlayer && candidate.NativeIndex == physicalIndex).ToArray())
        {
            if (replaced.IsActive)
            {
                ReleaseNativeParticipant(replaced);
            }
            replaced.NativeIndex = -1;
        }
        int generation = checked(nativeAllocations.Count + 1);
        NativeBattleEnemyAllocation allocation = new($"native-allocation-{generation}", sceneSlot, generation);
        BattleCombatant spawned = CreateNativeAllocation(allocation);
        nativeAllocations.Add(allocation);
        spawned.NativeIndex = physicalIndex;
        spawned.NativeReturnDamage = 0;
        spawned.NativePresentationStatus = previous?.NativePresentationStatus ?? new(null, null);
        spawned.NativePendingReaction = previous?.NativePendingReaction ?? new(null, null);
        nativeLastAllocatedIndex = physicalIndex;
        Log("NativeSpawn", caller.Id, spawned.Id, sceneSlot, $"{spawned.Name} allocated from formation slot {sceneSlot} into physical slot {physicalIndex}.");
        if (NativeGraphics is not null)
        {
            spawned.NativeCombatantFlags = 0;
            spawned.NativeStatus0 = (ushort)((previous is null ? 0 : NativeSavedStatusBits(previous)) | 1);
            spawned.NativeAllocationPendingStatistics = true;
            nativeScriptState = nativeScriptState with { AllocationControl = new(checked((byte)sceneSlot), true, true) };
            SetNativeBattleClockActive(false);
            NativeBattleGraphicsState graphics = QueueNativeDescriptor(NativeGraphics, 102, NativeParticipantAddress(physicalIndex), checked((ushort)physicalIndex), checked((ushort)(spawned.ModelId + 16)));
            graphics = QueueNativeDescriptor(graphics, 113, 0x48AD10);
            if (activate)
            {
                graphics = QueueNativeDescriptor(graphics, 113, 0x487670, checked((ushort)physicalIndex));
            }
            NativeGraphics = QueueNativeDescriptor(graphics, 10, 0x4876B0);
            return spawned;
        }
        // TODO(TD-009): Components without a native graphics owner materialize102/113 at this explicit boundary.
        InitializeNativeAllocatedStatistics(spawned);
        if (activate)
        {
            ActivateNativeParticipant(spawned);
        }
        return spawned;
    }

    private void InitializeNativeAllocatedStatistics(BattleCombatant spawned)
    {
        NativeBattleEnemyTemplate template = spawned.NativeTemplate ?? throw new InvalidDataException("Native allocation lacks its original level resource.");
        spawned.NativeCombatantFlags = template.NativeCombatantFlags | 0x11;
        spawned.NativePendingReaction = new(0, 0);
        nativeActionMemory.State = nativeActionMemory.State.InitializeStatistics(spawned.NativeIndex);
        int drawCount = NativeBattleEnemyLevelRules.RandomByteCount(template.SceneLevel);
        byte firstRoll = drawCount > 0 ? (byte)NextRandomByte() : (byte)0;
        byte? secondRoll = drawCount == 2 ? (byte)NextRandomByte() : null;
        int level = NativeBattleEnemyLevelRules.Resolve(template.SceneLevel, combatants.Where(candidate => candidate.IsPlayer && candidate.IsActive).Select(candidate => checked((byte)candidate.Level)).ToArray(), firstRoll, secondRoll);
        spawned.BindNativeLevel(level);
        spawned.ResetNativeStatusAllocation();
        ResetNativeDrawAllocation(spawned);
        spawned.CurrentHp = spawned.MaximumHp;
        spawned.Hidden = spawned.SourceDefinition.InitialHidden;
        spawned.Untargetable = spawned.SourceDefinition.InitialUntargetable;
        spawned.Suspended = spawned.SourceDefinition.InitialSuspended;
        spawned.AiTurnCount = 0;
        Array.Fill(spawned.NativeStatMultipliers, (byte)10);
        Array.Clear(spawned.AiVariables);
        // Keep the allocation draw after its level rolls, using the shared current-speed policy.
        spawned.Atb = BattleInitialAtb.Calculate(spawned.Speed, checked((byte)NextRandomByte()));
        spawned.NativeAllocationPendingStatistics = false;
    }

    private BattleCombatant CreateNativeAllocation(NativeBattleEnemyAllocation allocation)
    {
        if (allocation.Generation != nativeAllocations.Count + 1 || allocation.Id != $"native-allocation-{allocation.Generation}" || byId.ContainsKey(allocation.Id))
        {
            throw new InvalidDataException("Native allocation identity or generation is invalid.");
        }
        BattleCombatantDefinition prototype = definition.Combatants.SingleOrDefault(candidate => !candidate.IsPlayer && candidate.SceneSlot == allocation.SceneSlot) ?? throw new InvalidDataException("Native allocation references an absent scene prototype.");
        if (prototype.NativeTemplate is null)
        {
            throw new InvalidDataException("Native allocation references a scene without an original allocation template.");
        }
        BattleCombatant actor = new(prototype with { Id = allocation.Id, InitialActive = false, InitialAtb = 0 });
        actor.NativeActionMemoryOwner = nativeActionMemory;
        InitializeNativeAtb(actor);
        combatants.Add(actor);
        byId.Add(actor.Id, actor);
        return actor;
    }

    private void RestoreNativeAllocations(NativeBattleEnemyAllocation[] allocations)
    {
        if (allocations is null)
        {
            throw new InvalidDataException("Native allocation list is missing.");
        }
        foreach (NativeBattleEnemyAllocation allocation in allocations)
        {
            if (allocation is null)
            {
                throw new InvalidDataException("Native allocation list contains an empty owner.");
            }
            CreateNativeAllocation(allocation);
            nativeAllocations.Add(allocation);
        }
    }

    private void ReleaseNativeParticipant(BattleCombatant actor)
    {
        StopTurnAdvance();
        //105/502ED0 -> kind5/48AE20 owns only untargetable, savedKO and active bits.
        actor.Untargetable = true;
        actor.NativeStatus0 |= 1;
        actor.IsActive = false;
        // TODO(TD-009): Graphics105 completes the release; scaffold presentation commits it at the same dispatch boundary.
        Log("NativeModelRemoved", actor.Id, actor.Id, 0, $"{actor.Name} allocation released.");
    }
}
