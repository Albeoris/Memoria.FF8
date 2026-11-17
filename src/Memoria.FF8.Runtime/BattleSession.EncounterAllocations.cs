using static Memoria.FF8.Runtime.BattlePresentationProcess;
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
    private ImmutableArray<HostedModelLoadAdmission> hostedEncounterLoadAdmissions = [];

    public BattleEncounterAllocationState? EncounterAllocation => encounterAllocation;

    /// <summary>Reserves free roster slots and stages resolved copies without replacing an active participant.</summary>
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
            if (existing.Phase != BattleEncounterAllocationPhase.Complete)
            {
                throw new InvalidOperationException("This battle is still loading an encounter allocation batch.");
            }
        }
        BattleCombatant source = GetActor(sourceActorId);
        if (source.IsPlayer || !source.IsAlive || source.SceneSlot is not int sceneSlot || source.NativeAllocationPendingStatistics)
        {
            throw new InvalidOperationException("Encounter allocation requires its living enemy caster and scene owner.");
        }
        if (roles.Length is < 1 or > ExpandedBattleFormation.MaximumEnemies || roles.Any(string.IsNullOrWhiteSpace) || roles.Distinct(StringComparer.Ordinal).Count() != roles.Length)
        {
            throw new ArgumentException("Encounter allocation requires distinct role identities within the battle roster capacity.", nameof(roles));
        }
        if (NativeScriptAllocationPending || NativeScriptAllocationPaused || Presentation is { HasPendingModelLoad: true } or { Graphics.ModelLoadCount: > 0 })
        {
            throw new InvalidOperationException("Encounter allocation cannot overlap another native model allocation.");
        }
        BattleRosterSlot[] slots = ReadFreeEncounterRosterSlots(roles.Length);
        if (slots.Length != roles.Length)
        {
            throw new InvalidOperationException($"Encounter allocation needs {roles.Length} free enemy roster positions and has {slots.Length}.");
        }
        BattlePresentationProcess? process = preparingExpandedHostedAction ? null :
            Presentation ?? throw new InvalidOperationException("Encounter allocation requires its presentation process.");
        BattleSnapshot rollback = Capture();
        var prepared = ImmutableArray.CreateBuilder<BattleRosterAllocationRequest>(roles.Length);
        for (int index = 0; index < roles.Length; index++)
        {
            int generation = checked(nativeAllocations.Count + index + 1);
            string id = $"native-allocation-{generation}";
            string role = roles[index];
            BattleCombatant copySource = ReadEncounterCopySource(source, role);
            BattleCombatantDefinition resolved = CreateEncounterCopyDefinition(copySource, id, role);
            NativeBattleEnemyAllocation allocation = new(id, copySource.SceneSlot!.Value, generation) { ResolvedDefinition = resolved, EncounterBatchId = batchId, EncounterRole = roles[index], InheritedFromActorId = copySource.Id };
            BattleRosterSlot slot = slots[index];
            NativeBattleModelSlot? original = NativeBattleSlots.TryEncode(slot) is int indexAlias ? NativeBattleModelSlot.Decode(checked((byte)indexAlias)) : null;
            prepared.Add(new(allocation, slot, original, ReplaceOccupant: false));
        }
        encounterAllocationRollback = rollback;
        encounterAllocation = new(batchId, sourceActorId, prepared.Select(request => request.Allocation.Id).ToImmutableArray(), roles.ToImmutableArray(), BattleEncounterAllocationPhase.Loading, 0);
        try
        {
            BattleRosterMutationResult mutation = ApplyRosterMutation(new(TargetActorReference(source), prepared.MoveToImmutable()));
            foreach (BattleRosterAllocation allocation in mutation.Allocations)
            {
                BattleCombatant actor = GetActor(allocation.Actor.ActorId);
                actor.NativeAllocationPendingStatistics = true;
                actor.NativeCombatantFlags = 0xC2;
            }
            StopTurnAdvance();
            nativeScriptState = nativeScriptState with { AllocationControl = new(checked((byte)sceneSlot), true, true) };
            if (nativeBattleClock is not null)
            {
                SetNativeBattleClockActive(false);
            }
            if (process is null)
            {
                StageHostedEncounterAllocationLoad();
            }
            else
            {
                BattleCombatant first = GetActor(encounterAllocation.ActorIds[0]);
                HostedDispatchControl.LoadEnemy load = ReadHostedEncounterAllocationLoad(first);
                HostedModelLoadAdmission initial = PrepareHostedModelLoadAdmission(process, load, ImmutableHashSet<BattleModelOwner>.Empty);
                PrepareHostedEncounterBatch(process, [initial]);
                HostedDispatchControls controls = new([load, new HostedDispatchControl.AllocationStatistics()], []);
                PreparedHostedDispatch dispatch = process.PrepareDispatch([], controls: controls, allocations: [initial]);
                process.ValidatePreparedDispatch(dispatch);
                HostedEncounterLoadCheckpoint? checkpoint = process.PrepareEncounterLoad(encounterAllocation, dispatch);
                BindCurrentEncounterAllocation(first, load.SceneSlot);
                process.CommitDispatch(dispatch, checkpoint);
            }
            return encounterAllocation;
        }
        catch
        {
            AbortEncounterAllocation();
            throw;
        }
    }

    private BattleRosterSlot[] ReadFreeEncounterRosterSlots(int limit)
    {
        return Enumerable.Range(0, ExpandedBattleFormation.MaximumEnemies).Select(BattleRosterSlot.Enemy)
            .Where(slot => !enemies.Any(actor => actor.Slot == slot && actor.IsActive) && !IsEncounterRetirementPending(slot)).Take(limit).ToArray();
    }

    private BattleCombatant ReadEncounterCopySource(BattleCombatant caster, string role)
    {
        BattleCombatant source = caster;
        if (definition.EncounterMod?.Templates.GetValueOrDefault(role) is { } template)
        {
            string inherited = ResolveEncounterRole(template.InheritFrom) ?? throw new InvalidDataException($"Encounter template '{role}' has no allocated source for role '{template.InheritFrom}'.");
            source = GetActor(inherited);
        }
        if (!source.IsAlive || source.IsPlayer || source.SceneSlot is null || source.NativeTemplate is null || source.ResolvedNativeLevel is null || source.NativeAllocationPendingStatistics)
        {
            throw new InvalidOperationException($"Encounter role '{role}' requires a living enemy source with resolved statistics and installed model data.");
        }
        if (nativeAllocations.Any(allocation => allocation.EncounterRole == role && GetActor(allocation.Id).IsActive))
        {
            throw new InvalidOperationException($"Encounter role '{role}' already owns a live allocation.");
        }
        return source;
    }

    private BattleCombatantDefinition CreateEncounterCopyDefinition(BattleCombatant source, string id, string role)
    {
        BattleCombatantDefinition resolved = GetCombatantDefinition(source.Id);
        if (definition.EncounterMod is { } encounterMod && encounterMod.ActorProperties.TryGetValue(role + "/" + EncounterActorProperties.ReflectResistanceMultiplier, out double reflectResistanceMultiplier))
        {
            resolved = EncounterActorProperties.ScaleReflectResistance(resolved, reflectResistanceMultiplier);
        }
        NativeBattleEnemyTemplate template = resolved.NativeTemplate!;
        NativeBattleStatusAllocation freshStatuses = template.NativeStatusAllocation ?? new(0, 0, resolved.NativeStatusResistances.ToImmutableArray());
        NativeBattleAiDefinition? combatAbilities = resolved.NativeAi is { } ai ? new([[0], [0], [0], [0], [0]], ai.Abilities.ToArray(), ai.LevelBand, ai.Texts.ToArray()) : null;
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
        };
    }

    private static void ValidateEncounterAllocationDefinition(NativeBattleEnemyAllocation allocation, BattleCombatantDefinition resolved, BattleCombatantDefinition prototype)
    {
        bool hasLifecycleAi = resolved.NativeAi is { } ai && (ai.Scripts.Length != 5 || ai.Scripts.Any(script => script is not { Length: 1 } || script[0] != 0) || ai.Texts.Length != 0 || ai.NameBuffers.Length != 0 || ai.DisplayNames.Length != 0 || ai.Abilities.Length != 48);
        NativeBattleStatusAllocation? innate = resolved.NativeTemplate?.NativeStatusAllocation;
        if (string.IsNullOrWhiteSpace(allocation.EncounterBatchId) || string.IsNullOrWhiteSpace(allocation.EncounterRole) || resolved.Id != allocation.Id || resolved.IsPlayer || resolved.SceneSlot != allocation.SceneSlot || resolved.ModelId != prototype.ModelId || resolved.NativeTemplate is null || hasLifecycleAi || resolved.Rewards is not null || resolved.RewardSlot is not null || resolved.NativeMug is not null || resolved.NativeTemplate.NativeMug is not null || (resolved.NativeCombatantFlags & 0x20) != 0 || (resolved.NativeTemplate.NativeCombatantFlags & 0x20) != 0 || resolved.MaximumHp <= 0 || resolved.CurrentHp != resolved.MaximumHp || resolved.MaximumHpMultiplier != 1 || resolved.Level is < 0 or > 100 || resolved.InitialActive || !resolved.InitialHidden || !resolved.InitialUntargetable || !resolved.InitialSuspended || resolved.InitialAtb != 0 || resolved.NativeStatus0 != (innate?.Saved ?? 0) || resolved.NativeStatus1 != (innate?.Battle ?? 0))
        {
            throw new InvalidDataException("Encounter allocation snapshot has incompatible resolved statistics, identity or lifecycle ownership.");
        }
    }

    private void InitializeEncounterAllocatedStatistics(BattleCombatant actor)
    {
        actor.ResolvedNativeLevel = actor.Level;
        actor.NativeCombatantFlags = (actor.SourceDefinition.NativeCombatantFlags & ~0xE3u) | 0xC2;
        if (actor.NativeActorIndex is int original)
        {
            nativeActionMemory.State = nativeActionMemory.State.InitializeStatistics(original);
            nativePresentationStatusMemory.State = nativePresentationStatusMemory.State.Observe(original, new(0, 0));
        }
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

    private void StageHostedEncounterAllocationLoad()
    {
        BattleEncounterAllocationState batch = encounterAllocation!;
        BattleCombatant actor = GetActor(batch.ActorIds[batch.LoadingIndex]);
        HostedDispatchControl.LoadEnemy load = ReadHostedEncounterAllocationLoad(actor);
        BindCurrentEncounterAllocation(actor, load.SceneSlot);
        StageExpandedHostedControl(load);
        StageExpandedHostedControl(new HostedDispatchControl.AllocationStatistics());
    }

    private HostedDispatchControl.LoadEnemy ReadHostedEncounterAllocationLoad(BattleCombatant actor)
    {
        BattleModelOwner owner = CreatePresentationModelOwner(actor);
        if (owner is not BattleEnemyActorModel enemy || actor.Slot is not { Side: BattleSide.Enemies } slot || actor.SceneSlot is not int scene)
        {
            throw new InvalidDataException("Hosted encounter load requires its reserved enemy generation and scene resource.");
        }
        NativeBattleModelSlot? original = actor.NativeActorIndex is int alias ? NativeBattleModelSlot.Decode(checked((byte)alias)) : null;
        return new(enemy, slot, original, scene, actor.ModelId);
    }

    private void BindCurrentEncounterAllocation(BattleCombatant actor, int scene)
    {
        RecordLastNativeAllocation(actor);
        nativeScriptState = nativeScriptState with { AllocationControl = new(checked((byte)scene), true, true) };
    }

    private void PrepareHostedEncounterBatch(BattlePresentationProcess process, ImmutableArray<HostedModelLoadAdmission> initialLoads)
    {
        if (encounterAllocation is not { Phase: BattleEncounterAllocationPhase.Loading, LoadingIndex: 0 } batch || !hostedEncounterLoadAdmissions.IsEmpty)
        {
            return;
        }
        HostedModelLoadAdmission first = initialLoads.Single(admission => admission.Load.Owner.Actor.ActorId == batch.ActorIds[0]);
        var admissions = ImmutableArray.CreateBuilder<HostedModelLoadAdmission>(batch.ActorIds.Length);
        admissions.Add(first);
        foreach (string id in batch.ActorIds.Skip(1))
        {
            HostedDispatchControl.LoadEnemy load = ReadHostedEncounterAllocationLoad(GetActor(id));
            admissions.Add(PrepareHostedModelLoadAdmission(process, load, ImmutableHashSet<BattleModelOwner>.Empty));
        }
        hostedEncounterLoadAdmissions = admissions.MoveToImmutable();
    }

    private void AdvanceHostedEncounterAllocation(BattlePresentationProcess process)
    {
        if (encounterAllocation is { Phase: BattleEncounterAllocationPhase.Ready or BattleEncounterAllocationPhase.Arriving })
        {
            AdvanceHostedEncounterPolicySpawn(process);
            return;
        }
        if (encounterAllocation is not { Phase: BattleEncounterAllocationPhase.Loading } batch || !process.IsControlPrefixRetired)
        {
            return;
        }
        BattleCombatant actor = GetActor(batch.ActorIds[batch.LoadingIndex]);
        BattleModelOwner owner = CreatePresentationModelOwner(actor);
        if (actor.NativeAllocationPendingStatistics || !process.Graphics.ActorModels.TryGet(owner, out NativeBattleGraphicsModel? model) || !IsEncounterAllocationModelReady(model))
        {
            return;
        }
        int next = batch.LoadingIndex + 1;
        if (hostedEncounterLoadAdmissions.Length != batch.ActorIds.Length)
        {
            throw new InvalidDataException("Hosted encounter batch lost its prepared model bindings.");
        }
        if (next == batch.ActorIds.Length)
        {
            process.CompleteEncounterLoad();
            encounterAllocation = batch with { Phase = BattleEncounterAllocationPhase.Ready, LoadingIndex = next };
            encounterAllocationRollback = null;
            hostedEncounterLoadAdmissions = [];
            return;
        }
        BattleCombatant nextActor = GetActor(batch.ActorIds[next]);
        HostedDispatchControl.LoadEnemy load = ReadHostedEncounterAllocationLoad(nextActor);
        HostedModelLoadAdmission admission = hostedEncounterLoadAdmissions[next];
        if (admission.Load != load)
        {
            throw new InvalidDataException("A hosted encounter load no longer matches its prepared generation and slot.");
        }
        HostedDispatchControls controls = new([load, new HostedDispatchControl.AllocationStatistics()], []);
        PreparedHostedDispatch dispatch = process.PrepareDispatch([], controls: controls, allocations: [admission]);
        process.ValidatePreparedDispatch(dispatch);
        BindCurrentEncounterAllocation(nextActor, load.SceneSlot);
        encounterAllocation = batch with { LoadingIndex = next };
        process.CommitDispatch(dispatch);
    }

    private static bool IsEncounterAllocationModelReady(NativeBattleGraphicsModel model)
    {
        return (model.Flags0 & 2) != 0 && model.EffectCounter97 == 2 && model.Flags2 is ushort flags && (flags & 0x84) == 0 && model.Kind5 == 0 && model.SequenceState is { } sequence && model.Sequence == sequence.Default2;
    }

    /// <summary>Exposes the loaded models for an arrival track while keeping both actors outside active targeting and scheduling.</summary>
    public void BeginEncounterAllocationArrival()
    {
        BeginEncounterAllocationArrival(Presentation ?? throw new InvalidOperationException("Encounter arrival requires its presentation process."));
    }

    private void BeginEncounterAllocationArrival(BattlePresentationProcess process)
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
        process.RevealEncounterArrivalActors(batch.ActorIds.Select(id => CreatePresentationModelOwner(GetActor(id))).ToImmutableArray());
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
            BattleCombatant actor = byId[allocation.Id];
            combatants.Remove(actor);
            enemies.Remove(actor);
            byId.Remove(allocation.Id);
        }
        nativeAllocations.RemoveRange(rollback.NativeAllocations.Length, nativeAllocations.Count - rollback.NativeAllocations.Length);
        encounterAllocation = null;
        encounterAllocationRollback = null;
        if (expandedPresentationProcess is { } process)
        {
            bool presentationRolledBack = process.RollbackEncounterLoad();
            // Host time and the shared RNG/audio owners have progressed independently of the
            // paused allocation. The completed summon command keeps its admitted turn.
            rollback = rollback with
            {
                Frame = Frame,
                NativeHostCallbacks = NativeHostCallbacks,
                NativeExamTimer = NativeExamTimer,
                NativeCrtRandom = nativeCrtRandom,
                NativeBattleUiRandom = nativeBattleUiRandom,
                NativeGlobalRandom = nativeGlobalRandom,
                NativeGlobalRandomCalls = nativeGlobalRandomCalls,
                NativeMusicProcess = NativeMusicProcess,
                NativeGlobalSoundProcess = NativeGlobalSoundProcess,
                NativeVibrationProcess = NativeVibrationProcess,
                EncounterPolicies = rollback.EncounterPolicies is { } policies ? policies with { PendingSummon = null } : null
            };
            CopyCandidateState(Restore(rollback), rollback);
            ReleaseExpandedHostedFence();
            if (presentationRolledBack)
            {
                process.RefreshRolledBackEncounter();
                RefreshPreparedPresentation();
            }
            return;
        }
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
        if (encounterAllocation is { Phase: BattleEncounterAllocationPhase.Loading } && !preparingExpandedHostedAction)
        {
            throw new InvalidOperationException("Capture must wait for the atomic encounter model allocation to finish loading.");
        }
        return encounterAllocation;
    }

    private void RestoreEncounterAllocation(BattleEncounterAllocationState? state)
    {
        if (state is not null)
        {
            if (string.IsNullOrWhiteSpace(state.BatchId) || !byId.ContainsKey(state.SourceActorId) || state.ActorIds.IsDefaultOrEmpty || state.ActorIds.Length > ExpandedBattleFormation.MaximumEnemies || state.Roles.IsDefault || state.Roles.Length != state.ActorIds.Length || state.ActorIds.Distinct(StringComparer.Ordinal).Count() != state.ActorIds.Length || state.Roles.Any(string.IsNullOrWhiteSpace) || state.Roles.Distinct(StringComparer.Ordinal).Count() != state.Roles.Length || !Enum.IsDefined(state.Phase) || state.Phase == BattleEncounterAllocationPhase.Loading || state.LoadingIndex != state.ActorIds.Length)
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
