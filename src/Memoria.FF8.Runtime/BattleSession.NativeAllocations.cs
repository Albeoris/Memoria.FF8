namespace Memoria.FF8.Runtime;

/// <summary>A distinct native allocation of one immutable scene resource, including earlier released allocations.</summary>
public sealed record NativeBattleEnemyAllocation(string Id, int SceneSlot, int Generation)
{
    public BattleCombatantDefinition? ResolvedDefinition { get; init; }
    public string? EncounterBatchId { get; init; }
    public string? EncounterRole { get; init; }
    public string? InheritedFromActorId { get; init; }
    public BattleEnemyRosterSource? DonorSource { get; init; }
    public BattleSourcePlacementBinding? TemplateSource { get; init; }
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

    private byte ReadNativeAllocationSourceScene(BattleCombatant actor)
    {
        NativeBattleEnemyAllocation allocation = nativeAllocations.Single(candidate => candidate.Id == actor.Id);
        int source = allocation.TemplateSource?.SceneSlot ?? allocation.DonorSource?.SceneSlot ?? allocation.SceneSlot;
        if ((uint)source >= NativeBattleSlots.FormationSlotCount)
        {
            throw new InvalidDataException("Native allocation lost its original source scene operand.");
        }
        return checked((byte)source);
    }

    private BattleCombatant AllocateNativeEnemy(BattleCombatant caller, int sceneSlot, int requestedIndex, bool activate)
    {
        BattleEnemyRosterSource? callerSource = ReadDonorSource(caller);
        RequireNoPendingEncounterAllocation();
        if (!producingExpandedAiCallback)
        {
            throw new InvalidOperationException("Native enemy allocation requires its typed AI callback transaction.");
        }
        if ((uint)sceneSlot >= NativeBattleSlots.FormationSlotCount)
        {
            throw new InvalidDataException("Native enemy allocation requires an original formation prototype.");
        }
        int prototypeSlot = sceneSlot;
        BattleEnemyRosterSource? donorSource = null;
        if (callerSource is { } source)
        {
            (prototypeSlot, donorSource) = FindDonorPrototype(source, sceneSlot);
        }
        BattleCombatantDefinition prototype = definition.Combatants.SingleOrDefault(candidate => !candidate.IsPlayer && candidate.SceneSlot == prototypeSlot) ?? throw new InvalidDataException($"Native enemy allocation has no scene resource {sceneSlot}.");
        _ = prototype.NativeTemplate ?? throw new InvalidDataException("Native enemy allocation lacks its installed resource template.");
        int physicalIndex = requestedIndex;
        if (physicalIndex == 0)
        {
            physicalIndex = Enumerable.Range(NativeBattleSlots.AllyCount, NativeBattleSlots.EnemyCount).FirstOrDefault(index =>
                FindSourceOriginalActor(caller, index, activeOnly: true) is null && (callerSource is not null || !IsEncounterRetirementPending(index)), -1);
        }
        if (physicalIndex is < 3 or > 6)
        {
            throw new InvalidDataException("Native enemy allocation requires one of physical slots3..6 and an available allocation.");
        }
        BattleCombatant? previous = FindSourceOriginalActor(caller, physicalIndex);
        BattleRosterSlot slot = donorSource is null
            ? NativeBattleSlots.Decode(physicalIndex)
            : previous?.Slot ?? ReserveDonorAllocationSlot(prototypeSlot);
        if (IsEncounterRetirementPending(slot))
        {
            throw new InvalidOperationException("The selected physical enemy slot is still owned by an accepted encounter retirement.");
        }
        StopTurnAdvance();
        int generation = checked(nativeAllocations.Count + 1);
        NativeBattleEnemyAllocation allocation = new($"native-allocation-{generation}", prototypeSlot, generation)
        {
            DonorSource = donorSource is null ? null : donorSource with { OriginalEnemyOrdinal = physicalIndex - NativeBattleSlots.AllyCount }
        };
        NativeBattleModelSlot? original = donorSource is null ? NativeBattleModelSlot.Decode(checked((byte)physicalIndex)) : null;
        BattleRosterAllocation admitted = ApplyRosterMutation(new(TargetActorReference(caller), [new(allocation, slot, original, ReplaceOccupant: true)])).Allocations.Single();
        BattleCombatant spawned = GetActor(admitted.Actor.ActorId);
        spawned.NativeReturnDamage = 0;
        spawned.NativePendingReaction = previous?.NativePendingReaction ?? new(null, null);
        RecordLastNativeAllocation(spawned);
        Log("NativeSpawn", caller.Id, spawned.Id, sceneSlot, $"{spawned.Name} allocated from formation slot {sceneSlot} into enemy roster position {slot.Position}.");
        spawned.NativeCombatantFlags = 0;
        spawned.NativeStatus0 = (ushort)((previous is null ? 0 : NativeSavedStatusBits(previous)) | 1);
        spawned.NativeAllocationPendingStatistics = true;
        nativeScriptState = nativeScriptState with { AllocationControl = new(checked((byte)sceneSlot), true, true) };
        SetNativeBattleClockActive(false);
        StageHostedNativeAllocation(admitted.Model, admitted.Slot, admitted.OriginalSlot, prototypeSlot, spawned.ModelId, activate);
        return spawned;
    }

    private BattleEnemyRosterSource? ReadDonorSource(BattleCombatant actor)
    {
        if (actor.IsPlayer || actor.SceneSlot is not int slot || definition.AddedEnemySources is not { } sources || !sources.TryGetValue(slot, out BattleEnemyRosterSource? source))
        {
            return null;
        }
        NativeBattleEnemyAllocation? allocation = nativeAllocations.FirstOrDefault(candidate => candidate.Id == actor.Id);
        return allocation is null ? source.Group.HasValue ? source : source with { Group = source.GroupForSlot(slot) } : allocation.DonorSource;
    }

    private (BattleSourceGroupId Group, int SceneSlot, int? OriginalEnemyOrdinal)? ReadScopedEnemySource(BattleCombatant actor)
    {
        if (actor.IsPlayer)
        {
            return null;
        }
        NativeBattleEnemyAllocation? allocation = nativeAllocations.Find(item => item.Id == actor.Id);
        if (allocation?.TemplateSource is { } copy)
        {
            return (copy.Group, copy.SceneSlot, copy.OriginalEnemyOrdinal);
        }
        BattleEnemyRosterSource? source = allocation is null ? ReadDonorSource(actor) : allocation.DonorSource;
        return source is { Group: { } group } donor ? (group, donor.SceneSlot, donor.OriginalEnemyOrdinal) : null;
    }

    private (BattleSourceGroupId? Namespace, int SceneSlot, int? OriginalEnemyOrdinal) ReadTemplateSourceFacts(BattleCombatant source)
    {
        if (ReadScopedEnemySource(source) is { } binding)
        {
            return (binding.Group, binding.SceneSlot, binding.OriginalEnemyOrdinal);
        }
        int sceneSlot = source.SceneSlot ?? throw new InvalidDataException("An encounter template requires its inherited scene placement.");
        return (null, sceneSlot, source.NativeActorIndex is int operand ? operand - NativeBattleSlots.AllyCount : null);
    }

    private (int Slot, BattleEnemyRosterSource Source) FindDonorPrototype(BattleEnemyRosterSource source, int sceneSlot)
    {
        foreach ((int slot, BattleEnemyRosterSource candidate) in definition.AddedEnemySources!)
        {
            if (candidate.SceneSlot == sceneSlot && candidate.GroupForSlot(slot) == source.Group)
            {
                return (slot, candidate with { Group = source.Group });
            }
        }
        throw new InvalidDataException($"The admitted donor group has no reserve at source scene slot {sceneSlot}.");
    }

    private BattleRosterSlot ReserveDonorAllocationSlot(int prototypeSlot)
    {
        int preferred = NativeBattleSlots.EnemyCount + prototypeSlot - NativeBattleSlots.FormationSlotCount;
        BattleRosterSlot slot = BattleRosterSlot.Enemy(preferred);
        if (enemies.All(actor => actor.Slot != slot) && !IsEncounterRetirementPending(slot))
        {
            return slot;
        }
        for (int position = NativeBattleSlots.EnemyCount; position < ExpandedBattleFormation.MaximumEnemies; position++)
        {
            slot = BattleRosterSlot.Enemy(position);
            if (enemies.All(actor => actor.Slot != slot) && !IsEncounterRetirementPending(slot))
            {
                return slot;
            }
        }
        throw new InvalidOperationException("The donor group has no available enemy allocation position.");
    }

    private BattleCombatant? FindSourceOriginalActor(BattleCombatant caller, int operand, bool activeOnly = false)
    {
        if (operand < NativeBattleSlots.AllyCount || ReadScopedEnemySource(caller) is not { } source)
        {
            return activeOnly ? FindActiveNativeActor(operand) : FindAllocatedNativeActor(operand);
        }
        int ordinal = operand - NativeBattleSlots.AllyCount;
        return enemies.SingleOrDefault(actor => actor.Slot is not null && (!activeOnly || actor.IsActive) &&
            ReadScopedEnemySource(actor) is { OriginalEnemyOrdinal: int original } binding && binding.Group == source.Group && original == ordinal);
    }

    private BattleCombatant? FindSourceEnemyModelActor(BattleCombatant caller, int nativeModel)
    {
        if (nativeModel < 16 || ReadScopedEnemySource(caller) is not { } source)
        {
            return ExpandedAiModelActor(nativeModel);
        }
        return enemies.Where(actor => actor.Slot is not null && NativeModelId(actor) == nativeModel)
            .Select(actor => (Actor: actor, Source: ReadScopedEnemySource(actor)))
            .Where(candidate => candidate.Source is { OriginalEnemyOrdinal: not null } binding && binding.Group == source.Group)
            .OrderBy(candidate => candidate.Source?.OriginalEnemyOrdinal)
            .Select(candidate => candidate.Actor)
            .FirstOrDefault();
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
        spawned.LastNativeAction = new(0, null, null, null, null, 0);
        int drawCount = NativeBattleEnemyLevelRules.RandomByteCount(template.SceneLevel);
        byte firstRoll = drawCount > 0 ? (byte)NextRandomByte() : (byte)0;
        byte? secondRoll = drawCount == 2 ? (byte)NextRandomByte() : null;
        int[] partyLevels = allies.Where(candidate => candidate.IsActive).Select(candidate => candidate.Level).ToArray();
        int level = NativeBattleEnemyLevelRules.ResolveExpanded(template.SceneLevel, partyLevels, firstRoll, secondRoll, definition.LevelRules);
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
        bool sceneMatchesSource = allocation.DonorSource is { } donor
            ? definition.AddedEnemySources?.TryGetValue(allocation.SceneSlot, out BattleEnemyRosterSource? source) == true &&
                donor.Group == source.GroupForSlot(allocation.SceneSlot) && donor.Formation == source.Formation && donor.SceneSlot == source.SceneSlot &&
                donor.FormationHash.Equals(source.FormationHash, StringComparison.OrdinalIgnoreCase) && donor.OriginalEnemyOrdinal is >= 0 and < NativeBattleSlots.EnemyCount
            : (uint)allocation.SceneSlot < NativeBattleSlots.FormationSlotCount;
        if (allocation.Generation != nativeAllocations.Count + 1 || allocation.Id != $"native-allocation-{allocation.Generation}" || byId.ContainsKey(allocation.Id) || !sceneMatchesSource)
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
            if (allocation.Retirement is { } retirement && (allocation.EncounterBatchId is null || retirement.Slot.Side != BattleSide.Enemies || retirement.Slot.Position >= ExpandedBattleFormation.MaximumEnemies || retirement.OriginalIndex is int original && original != NativeBattleSlots.TryEncode(retirement.Slot)))
            {
                throw new InvalidDataException("Encounter retirement requires its allocation generation and an enemy roster position.");
            }
            NativeBattleEnemyAllocation owned = CloneNativeAllocation(allocation);
            if (owned is { ResolvedDefinition: not null, InheritedFromActorId: { } source })
            {
                BattleCombatant ancestor = GetActor(source);
                var inherited = ReadTemplateSourceFacts(ancestor);
                if (owned.TemplateSource is not { Group.Kind: BattleSourceGroupKind.EncounterCopy } binding ||
                    binding.Group.Slot < 1 || binding.Group.Slot > owned.Generation || binding.SceneSlot != inherited.SceneSlot ||
                    binding.OriginalEnemyOrdinal is < 0 or >= NativeBattleSlots.EnemyCount || inherited.OriginalEnemyOrdinal.HasValue && binding.OriginalEnemyOrdinal != inherited.OriginalEnemyOrdinal ||
                    owned.DonorSource != ReadDonorSource(ancestor))
                {
                    throw new InvalidDataException("An encounter copy lost its admitted source placement or inherited model operand.");
                }
                if (binding.Group.Slot != owned.Generation)
                {
                    NativeBattleEnemyAllocation? root = nativeAllocations.FirstOrDefault(candidate => candidate.Generation == binding.Group.Slot);
                    string? templateGroup = definition.EncounterMod?.Templates.GetValueOrDefault(owned.EncounterRole!)?.Group;
                    if (root is null || root.TemplateSource?.Group != binding.Group || root.EncounterBatchId != owned.EncounterBatchId || templateGroup is null ||
                        definition.EncounterMod!.Templates.GetValueOrDefault(root.EncounterRole!)?.Group != templateGroup ||
                        ReadTemplateSourceFacts(GetActor(root.InheritedFromActorId!)).Namespace != inherited.Namespace || binding.OriginalEnemyOrdinal is null ||
                        nativeAllocations.Any(candidate => candidate.TemplateSource is { } previous && previous.Group == binding.Group && previous.OriginalEnemyOrdinal == binding.OriginalEnemyOrdinal))
                    {
                        throw new InvalidDataException("Encounter copies share a source group only within their admitted batch and distinct inherited operands.");
                    }
                }
            }
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
