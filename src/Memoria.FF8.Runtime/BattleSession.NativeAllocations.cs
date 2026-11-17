namespace Memoria.FF8.Runtime;

/// <summary>A distinct native allocation of one immutable scene resource, including earlier released allocations.</summary>
public sealed record NativeBattleEnemyAllocation(string Id, int SceneSlot, int Generation)
{
    public BattleCombatantDefinition? ResolvedDefinition { get; init; }
    public string? EncounterBatchId { get; init; }
    public string? EncounterRole { get; init; }
    public string? InheritedFromActorId { get; init; }
    public EncounterAllocationRetirement? Retirement { get; init; }
}

public sealed partial class BattleSession
{

    private NativeBattleEnemyAllocation CloneNativeAllocation(NativeBattleEnemyAllocation allocation)
    {
        return allocation.ResolvedDefinition is null ? allocation : allocation with { ResolvedDefinition = CloneDefinition(definition with { Combatants = [allocation.ResolvedDefinition] }).Combatants[0] };
    }

    private NativeBattleEnemyAllocation[] CaptureNativeAllocations()
    {
        return nativeAllocations.Select(CloneNativeAllocation).ToArray();
    }

    private BattleCombatant AllocateNativeEnemy(BattleCombatant caller, int sceneSlot, int requestedIndex, bool activate)
    {
        if (!UsesOriginalEnemySlots)
        {
            throw new NotSupportedException("Expanded AI cannot allocate original enemy slots on this roster.");
        }
        RequireNoPendingEncounterAllocation();
        if (!producingExpandedAiCallback)
        {
            throw new InvalidOperationException("Native enemy allocation requires its typed AI callback transaction.");
        }
        if ((uint)sceneSlot >= NativeBattleSlots.FormationSlotCount)
        {
            throw new InvalidDataException("Native enemy allocation requires an original formation prototype.");
        }
        BattleCombatantDefinition prototype = definition.Combatants.SingleOrDefault(candidate => !candidate.IsPlayer && candidate.SceneSlot == sceneSlot) ?? throw new InvalidDataException($"Native enemy allocation has no scene resource {sceneSlot}.");
        NativeBattleEnemyTemplate template = prototype.NativeTemplate ?? throw new InvalidDataException("Native enemy allocation lacks its installed resource template.");
        int physicalIndex = requestedIndex == 0 ? Enumerable.Range(NativeBattleSlots.AllyCount, NativeBattleSlots.EnemyCount).FirstOrDefault(index => FindActiveNativeActor(index) is null && !IsEncounterRetirementPending(index), -1) : requestedIndex;
        if (physicalIndex is < 3 or > 6)
        {
            throw new InvalidDataException("Native enemy allocation requires one of physical slots3..6 and an available allocation.");
        }
        if (IsEncounterRetirementPending(physicalIndex))
        {
            throw new InvalidOperationException("The selected physical enemy slot is still owned by an accepted encounter retirement.");
        }
        StopTurnAdvance();
        BattleRosterSlot slot = NativeBattleSlots.Decode(physicalIndex);
        BattleCombatant? previous = FindAllocatedNativeActor(physicalIndex);
        int generation = checked(nativeAllocations.Count + 1);
        NativeBattleEnemyAllocation allocation = new($"native-allocation-{generation}", sceneSlot, generation);
        NativeBattleModelSlot original = NativeBattleModelSlot.Decode(checked((byte)physicalIndex));
        BattleRosterAllocation admitted = ApplyRosterMutation(new(TargetActorReference(caller), [new(allocation, slot, original, ReplaceOccupant: true)])).Allocations.Single();
        BattleCombatant spawned = GetActor(admitted.Actor.ActorId);
        spawned.NativeReturnDamage = 0;
        spawned.NativePendingReaction = previous?.NativePendingReaction ?? new(null, null);
        RecordLastNativeAllocation(spawned);
        Log("NativeSpawn", caller.Id, spawned.Id, sceneSlot, $"{spawned.Name} allocated from formation slot {sceneSlot} into physical slot {physicalIndex}.");
        spawned.NativeCombatantFlags = 0;
        spawned.NativeStatus0 = (ushort)((previous is null ? 0 : NativeSavedStatusBits(previous)) | 1);
        spawned.NativeAllocationPendingStatistics = true;
        nativeScriptState = nativeScriptState with { AllocationControl = new(checked((byte)sceneSlot), true, true) };
        SetNativeBattleClockActive(false);
        StageHostedNativeAllocation(admitted.Model, admitted.Slot, admitted.OriginalSlot, sceneSlot, spawned.ModelId, activate);
        return spawned;
    }

    private void InitializeNativeAllocatedStatistics(BattleCombatant spawned)
    {
        if (nativeAllocations.Single(allocation => allocation.Id == spawned.Id).ResolvedDefinition is not null)
        {
            InitializeEncounterAllocatedStatistics(spawned);
            return;
        }
        NativeBattleEnemyTemplate template = spawned.NativeTemplate ?? throw new InvalidDataException("Native allocation lacks its original level resource.");
        spawned.NativeCombatantFlags = template.NativeCombatantFlags | 0x11;
        spawned.NativePendingReaction = new(0, 0);
            spawned.LastNativeAction = new(0, null, null, null, null, null);
        int drawCount = NativeBattleEnemyLevelRules.RandomByteCount(template.SceneLevel);
        byte firstRoll = drawCount > 0 ? (byte)NextRandomByte() : (byte)0;
        byte? secondRoll = drawCount == 2 ? (byte)NextRandomByte() : null;
        byte[] partyLevels = allies.Where(candidate => candidate.IsActive).Select(candidate => checked((byte)candidate.Level)).ToArray();
        int level = NativeBattleEnemyLevelRules.ResolveExpanded(template.SceneLevel, partyLevels, firstRoll, secondRoll);
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
        if (allocation.Generation != nativeAllocations.Count + 1 || allocation.Id != $"native-allocation-{allocation.Generation}" || byId.ContainsKey(allocation.Id) || (uint)allocation.SceneSlot >= NativeBattleSlots.FormationSlotCount)
        {
            throw new InvalidDataException("Native allocation identity or generation is invalid.");
        }
        BattleCombatantDefinition prototype = definition.Combatants.SingleOrDefault(candidate => !candidate.IsPlayer && candidate.SceneSlot == allocation.SceneSlot) ?? throw new InvalidDataException("Native allocation references an absent scene prototype.");
        if (prototype.NativeTemplate is null)
        {
            throw new InvalidDataException("Native allocation references a scene without an original allocation template.");
        }
        if (allocation.ResolvedDefinition is { } resolved)
        {
            ValidateEncounterAllocationDefinition(allocation, resolved, prototype);
            prototype = CloneDefinition(definition with { Combatants = [resolved] }).Combatants[0];
        }
        BattleCombatant actor = CreateCombatant(prototype with { Id = allocation.Id, InitialActive = false, InitialAtb = 0 });
        combatants.Add(actor);
        enemies.Add(actor);
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
            if (allocation.Retirement is { } retirement && (allocation.EncounterBatchId is null || retirement.Slot.Side != BattleSide.Enemies || retirement.Slot.Position >= ExpandedBattleFormation.MaximumEnemies || retirement.OriginalIndex != NativeBattleSlots.TryEncode(retirement.Slot)))
            {
                throw new InvalidDataException("Encounter retirement requires its allocation generation and an enemy roster position.");
            }
            NativeBattleEnemyAllocation owned = CloneNativeAllocation(allocation);
            CreateNativeAllocation(owned);
            nativeAllocations.Add(owned);
        }
    }

    private void ReleaseNativeParticipant(BattleCombatant actor)
    {
        StopTurnAdvance();
        //105/502ED0 -> kind5/48AE20 owns only untargetable, savedKO and active bits.
        actor.Untargetable = true;
        actor.NativeStatus0 |= 1;
        actor.IsActive = false;
        Log("NativeModelRemoved", actor.Id, actor.Id, 0, $"{actor.Name} allocation released.");
    }

    private readonly List<NativeBattleEnemyAllocation> nativeAllocations = [];

    public BattleCombatantDefinition GetCombatantDefinition(string id)
    {
        return CloneDefinition(definition with { Combatants = [GetActor(id).SourceDefinition] }).Combatants[0];
    }
}
