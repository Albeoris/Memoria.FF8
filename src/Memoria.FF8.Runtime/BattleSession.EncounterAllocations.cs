using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum BattleEncounterAllocationPhase
{
    Loading,
    Ready,
    Arriving,
    Complete
}

public sealed record BattleEncounterAllocationState(string BatchId, string SourceActorId, ImmutableArray<string> ActorIds, ImmutableArray<string> Roles, BattleEncounterAllocationPhase Phase, int LoadingIndex);

public sealed partial class BattleSession
{
    private BattleEncounterAllocationState? encounterAllocation;
    private BattleSnapshot? encounterAllocationRollback;

    public BattleEncounterAllocationState? EncounterAllocation => encounterAllocation;

    /// <summary>Reserves free physical slots and stages resolved copies without replacing an active participant.</summary>
    public BattleEncounterAllocationState BeginEncounterAllocation(string batchId, string sourceActorId, params string[] roles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
        ArgumentNullException.ThrowIfNull(roles);
        if (encounterAllocation is { } existing)
        {
            if (existing.BatchId == batchId && existing.SourceActorId == sourceActorId && existing.Roles.SequenceEqual(roles))
            {
                return existing;
            }
            throw new InvalidOperationException("This battle already owns an encounter allocation batch.");
        }
        BattleCombatant source = GetActor(sourceActorId);
        if (source.IsPlayer || !source.IsAlive || source.SceneSlot is not int sceneSlot || source.NativeTemplate is null || source.ResolvedNativeLevel is null || source.NativeAllocationPendingStatistics)
        {
            throw new InvalidOperationException("Encounter copies require a living enemy with fully resolved native statistics and a scene resource.");
        }
        if (roles.Length is < 1 or > 3 || roles.Any(string.IsNullOrWhiteSpace) || roles.Distinct(StringComparer.Ordinal).Count() != roles.Length)
        {
            throw new ArgumentException("Encounter allocation requires one to three distinct role identities.", nameof(roles));
        }
        if (NativeScriptAllocationPending || NativeScriptAllocationPaused || NativeModelLoader?.Task is not null || NativeGraphics?.ModelLoadCount > 0)
        {
            throw new InvalidOperationException("Encounter allocation cannot overlap another native model allocation.");
        }
        int[] slots = Enumerable.Range(3, 4).Where(index => !combatants.Any(actor => actor.NativeIndex == index && actor.IsActive) && !IsEncounterRetirementPending(index)).Take(roles.Length).ToArray();
        if (slots.Length != roles.Length)
        {
            throw new InvalidOperationException($"Encounter allocation requires {roles.Length} free enemy slots; only {slots.Length} are available within physical slots 3–6.");
        }
        BattleSnapshot rollback = Capture();
        List<NativeBattleEnemyAllocation> prepared = [];
        for (int index = 0; index < roles.Length; index++)
        {
            int generation = checked(nativeAllocations.Count + index + 1);
            string id = $"native-allocation-{generation}";
            BattleCombatantDefinition resolved = CreateEncounterCopyDefinition(source, id, roles[index]);
            prepared.Add(new(id, sceneSlot, generation) { ResolvedDefinition = resolved, EncounterBatchId = batchId, EncounterRole = roles[index] });
        }
        encounterAllocationRollback = rollback;
        encounterAllocation = new(batchId, sourceActorId, prepared.Select(allocation => allocation.Id).ToImmutableArray(), roles.ToImmutableArray(), BattleEncounterAllocationPhase.Loading, 0);
        try
        {
            for (int index = 0; index < prepared.Count; index++)
            {
                foreach (BattleCombatant previous in combatants.Where(actor => actor.NativeIndex == slots[index]).ToArray())
                {
                    previous.NativeIndex = -1;
                }
                BattleCombatant actor = CreateNativeAllocation(prepared[index]);
                nativeAllocations.Add(prepared[index]);
                actor.NativeIndex = slots[index];
                actor.NativeAllocationPendingStatistics = true;
                actor.NativeCombatantFlags = 0xC2;
            }
            StopTurnAdvance();
            nativeScriptState = nativeScriptState with { AllocationControl = new(checked((byte)sceneSlot), true, true) };
            if (nativeBattleClock is not null)
            {
                SetNativeBattleClockActive(false);
            }
            if (NativeGraphics is { } graphics)
            {
                NativeGraphics = QueueEncounterAllocationLoad(graphics);
            }
            else
            {
                foreach (string id in encounterAllocation.ActorIds)
                {
                    InitializeEncounterAllocatedStatistics(GetActor(id));
                }
                encounterAllocation = encounterAllocation with { Phase = BattleEncounterAllocationPhase.Ready, LoadingIndex = roles.Length };
                encounterAllocationRollback = null;
            }
            return encounterAllocation;
        }
        catch
        {
            AbortEncounterAllocation();
            throw;
        }
    }

    private BattleCombatantDefinition CreateEncounterCopyDefinition(BattleCombatant source, string id, string role)
    {
        BattleCombatantDefinition resolved = GetCombatantDefinition(source.Id);
        NativeBattleEnemyTemplate template = resolved.NativeTemplate!;
        NativeBattleStatusAllocation freshStatuses = template.NativeStatusAllocation ?? new(0, 0, resolved.NativeStatusResistances.ToImmutableArray());
        NativeBattleAiDefinition? combatAbilities = resolved.NativeAi is { } ai ? new([[0], [0], [0], [0], [0]], ai.Abilities.ToArray(), ai.LevelBand, []) : null;
        double maximumHpMultiplier = definition.EncounterMod is { } mod && mod.Templates.ContainsKey(role) ? mod.ActorProperties.GetValueOrDefault(role + "/stats.maximumHpMultiplier", 1) : 1;
        int maximumHp = EncounterActorProperties.ScaleMaximumHp(source.MaximumHp, maximumHpMultiplier);
        return resolved with
        {
            Id = id,
            Level = source.Level,
            MaximumHp = maximumHp,
            MaximumHpMultiplier = 1,
            CurrentHp = maximumHp,
            InitialAtb = 0,
            InitialActive = false,
            InitialHidden = true,
            InitialUntargetable = true,
            InitialSuspended = true,
            NativeAi = combatAbilities,
            NativeTemplate = template with { NativeMug = null, NativeSpecial = null, NativeStatusAllocation = freshStatuses, NativeCombatantFlags = template.NativeCombatantFlags & ~0xE3u },
            NativeMug = null,
            NativeSpecial = null,
            Rewards = null,
            RewardSlot = null,
            AbilityPoints = 0,
            NativeKillCount = 0,
            NativeDeathCount = 0,
            NativeCombatantFlags = resolved.NativeCombatantFlags & ~0xE3u,
            NativeStatus0 = freshStatuses.Saved,
            NativeStatus1 = freshStatuses.Battle,
            NativeRetainedStatusBits = 0,
            StatusEffects = []
        };
    }

    private static void ValidateEncounterAllocationDefinition(NativeBattleEnemyAllocation allocation, BattleCombatantDefinition resolved, BattleCombatantDefinition prototype)
    {
        bool hasLifecycleAi = resolved.NativeAi is { } ai && (ai.Scripts.Length != 5 || ai.Scripts.Any(script => script is not { Length: 1 } || script[0] != 0) || ai.Texts.Length != 0 || ai.NameBuffers.Length != 0 || ai.DisplayNames.Length != 0 || !ai.SequenceTracks.IsEmpty || ai.Abilities.Length != 48);
        NativeBattleStatusAllocation? innate = resolved.NativeTemplate?.NativeStatusAllocation;
        if (string.IsNullOrWhiteSpace(allocation.EncounterBatchId) || string.IsNullOrWhiteSpace(allocation.EncounterRole) || resolved.Id != allocation.Id || resolved.IsPlayer || resolved.SceneSlot != allocation.SceneSlot || resolved.ModelId != prototype.ModelId || resolved.NativeTemplate is null || hasLifecycleAi || resolved.Rewards is not null || resolved.RewardSlot is not null || resolved.NativeMug is not null || resolved.NativeTemplate.NativeMug is not null || (resolved.NativeCombatantFlags & 0x20) != 0 || (resolved.NativeTemplate.NativeCombatantFlags & 0x20) != 0 || resolved.MaximumHp <= 0 || resolved.CurrentHp != resolved.MaximumHp || resolved.MaximumHpMultiplier != 1 || resolved.Level is < 0 or > 100 || resolved.InitialActive || !resolved.InitialHidden || !resolved.InitialUntargetable || !resolved.InitialSuspended || resolved.InitialAtb != 0 || resolved.NativeStatus0 != (innate?.Saved ?? 0) || resolved.NativeStatus1 != (innate?.Battle ?? 0) || resolved.StatusEffects.Length != 0)
        {
            throw new InvalidDataException("Encounter allocation snapshot has incompatible resolved statistics, identity or lifecycle ownership.");
        }
    }

    private void InitializeEncounterAllocatedStatistics(BattleCombatant actor)
    {
        actor.ResolvedNativeLevel = actor.Level;
        actor.NativeCombatantFlags = (actor.SourceDefinition.NativeCombatantFlags & ~0xE3u) | 0xC2;
        nativeActionMemory.State = nativeActionMemory.State with { Rows = nativeActionMemory.State.Rows.SetItem(actor.NativeIndex, ImmutableArray.CreateRange(new byte?[8] { 0, 0, 0, 0, 0, 0, 0, 0 })) };
        nativePresentationStatusMemory.State = nativePresentationStatusMemory.State.Observe(actor.NativeIndex, new(0, 0));
        actor.NativePendingReaction = new(0, 0);
        actor.NativeReturnDamage = 0;
        actor.NativeRemainingHits = 0;
        actor.ResetNativeStatusAllocation();
        ResetNativeDrawAllocation(actor);
        actor.CurrentHp = actor.MaximumHp;
        actor.Atb = 0;
        actor.AiTurnCount = 0;
        Array.Clear(actor.AiVariables);
        Array.Fill(actor.NativeStatMultipliers, (byte)10);
        actor.NativeAllocationPendingStatistics = false;
    }

    private NativeBattleGraphicsState QueueEncounterAllocationLoad(NativeBattleGraphicsState graphics)
    {
        BattleEncounterAllocationState batch = encounterAllocation!;
        BattleCombatant actor = GetActor(batch.ActorIds[batch.LoadingIndex]);
        nativeLastAllocatedIndex = actor.NativeIndex;
        nativeScriptState = nativeScriptState with { AllocationControl = new(checked((byte)actor.SceneSlot!.Value), true, true) };
        graphics = QueueNativeDescriptor(graphics, 102, NativeParticipantAddress(actor.NativeIndex), checked((ushort)actor.NativeIndex), checked((ushort)(actor.ModelId + 16)));
        return QueueNativeDescriptor(graphics, 113, 0x48AD10);
    }

    private NativeBattleGraphicsState CompleteEncounterAllocationLoad(NativeBattleGraphicsState graphics)
    {
        BattleEncounterAllocationState batch = encounterAllocation!;
        BattleCombatant actor = GetActor(batch.ActorIds[batch.LoadingIndex]);
        if (actor.NativeAllocationPendingStatistics || graphics.Models[actor.NativeIndex].EffectCounter97 != 2 || (graphics.Models[actor.NativeIndex].Flags0 & 2) == 0)
        {
            throw new InvalidDataException("Encounter allocation reached completion before its resolved statistics and model were loaded.");
        }
        encounterAllocation = batch with { LoadingIndex = batch.LoadingIndex + 1 };
        if (encounterAllocation.LoadingIndex < batch.ActorIds.Length)
        {
            return QueueEncounterAllocationLoad(graphics);
        }
        encounterAllocation = encounterAllocation with { Phase = BattleEncounterAllocationPhase.Ready };
        encounterAllocationRollback = null;
        return graphics;
    }

    /// <summary>Exposes the loaded models for an arrival track while keeping both actors outside active targeting and scheduling.</summary>
    public void BeginEncounterAllocationArrival()
    {
        BattleEncounterAllocationState batch = encounterAllocation ?? throw new InvalidOperationException("No encounter allocation is staged.");
        if (batch.Phase is BattleEncounterAllocationPhase.Arriving or BattleEncounterAllocationPhase.Complete)
        {
            return;
        }
        if (batch.Phase != BattleEncounterAllocationPhase.Ready)
        {
            throw new InvalidOperationException("Encounter arrival requires every staged model to be loaded.");
        }
        if (NativeGraphics is { } graphics)
        {
            foreach (string id in batch.ActorIds)
            {
                int index = GetActor(id).NativeIndex;
                graphics = graphics with { Models = graphics.Models.SetItem(index, graphics.Models[index] with { Flags0 = (ushort)(graphics.Models[index].Flags0 & ~4) }) };
            }
            NativeGraphics = graphics;
        }
        encounterAllocation = batch with { Phase = BattleEncounterAllocationPhase.Arriving };
    }

    public void CompleteEncounterAllocationArrival()
    {
        BattleEncounterAllocationState batch = encounterAllocation ?? throw new InvalidOperationException("No encounter allocation is staged.");
        if (batch.Phase == BattleEncounterAllocationPhase.Complete)
        {
            return;
        }
        if (batch.Phase != BattleEncounterAllocationPhase.Arriving)
        {
            throw new InvalidOperationException("Encounter allocation must finish its arrival before activation.");
        }
        foreach (string id in batch.ActorIds)
        {
            BattleCombatant actor = GetActor(id);
            actor.IsActive = true;
            actor.Hidden = false;
            actor.Untargetable = false;
            actor.Suspended = false;
            actor.AiDeathHandled = false;
        }
        encounterAllocation = batch with { Phase = BattleEncounterAllocationPhase.Complete };
        if (nativeBattleClock is not null)
        {
            CompleteNativeAllocationGate();
        }
        else
        {
            nativeScriptState = nativeScriptState with { AllocationControl = nativeScriptState.AllocationControl! with { ActionGate = false, PauseGate = false } };
        }
        Log("EncounterSpawn", batch.SourceActorId, null, batch.ActorIds.Length, $"Encounter allocation {batch.BatchId} entered battle.");
    }

    public void AbortEncounterAllocation()
    {
        if (encounterAllocationRollback is not { } rollback)
        {
            return;
        }
        foreach (NativeBattleEnemyAllocation allocation in nativeAllocations.Skip(rollback.NativeAllocations.Length).ToArray())
        {
            combatants.Remove(byId[allocation.Id]);
            byId.Remove(allocation.Id);
        }
        nativeAllocations.RemoveRange(rollback.NativeAllocations.Length, nativeAllocations.Count - rollback.NativeAllocations.Length);
        encounterAllocation = null;
        encounterAllocationRollback = null;
        PublishCandidate(Restore(rollback));
    }

    private void RequireNoPendingEncounterAllocation()
    {
        if (encounterAllocation is { Phase: not BattleEncounterAllocationPhase.Complete })
        {
            throw new InvalidOperationException("A native allocation cannot replace slots reserved by an unfinished encounter allocation.");
        }
    }

    private BattleEncounterAllocationState? CaptureEncounterAllocation()
    {
        if (encounterAllocation is { Phase: BattleEncounterAllocationPhase.Loading })
        {
            throw new InvalidOperationException("Capture must wait for the atomic encounter model allocation to finish loading.");
        }
        return encounterAllocation;
    }

    private void RestoreEncounterAllocation(BattleEncounterAllocationState? state)
    {
        if (state is not null)
        {
            if (string.IsNullOrWhiteSpace(state.BatchId) || !byId.ContainsKey(state.SourceActorId) || state.ActorIds.IsDefaultOrEmpty || state.ActorIds.Length > 3 || state.Roles.IsDefault || state.Roles.Length != state.ActorIds.Length || state.ActorIds.Distinct(StringComparer.Ordinal).Count() != state.ActorIds.Length || state.Roles.Any(string.IsNullOrWhiteSpace) || state.Roles.Distinct(StringComparer.Ordinal).Count() != state.Roles.Length || !Enum.IsDefined(state.Phase) || state.Phase == BattleEncounterAllocationPhase.Loading || state.LoadingIndex != state.ActorIds.Length)
            {
                throw new InvalidDataException("Invalid encounter allocation publication state.");
            }
            for (int index = 0; index < state.ActorIds.Length; index++)
            {
                NativeBattleEnemyAllocation allocation = nativeAllocations.SingleOrDefault(candidate => candidate.Id == state.ActorIds[index]) ?? throw new InvalidDataException("Encounter role has no retained allocation.");
                if (allocation.EncounterBatchId != state.BatchId || allocation.EncounterRole != state.Roles[index] || allocation.ResolvedDefinition is null)
                {
                    throw new InvalidDataException("Encounter role and resolved allocation ownership disagree.");
                }
            }
        }
        else if (nativeAllocations.Any(allocation => allocation.EncounterBatchId is not null))
        {
            throw new InvalidDataException("Encounter allocations require their retained batch owner.");
        }
        encounterAllocation = state;
        encounterAllocationRollback = null;
        ValidateEncounterRetirements();
    }
}
