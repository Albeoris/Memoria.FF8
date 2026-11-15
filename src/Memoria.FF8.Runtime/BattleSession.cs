using Memoria.Battle;

namespace Memoria.FF8.Runtime;

/// <summary>
/// Playable battle owner with original formation/AI contracts and declared scaffold
/// ATB, status and presentation timing projections under TD-009.
/// </summary>
public sealed partial class BattleSession
{
    // TODO(TD-009): Replace scaffold gauge and action timings with recovered battle contracts.
    public const int FramesPerSecond = 30;
    public const int AtbMaximum = 10_000;
    public const int AttackWindupFrames = 12;
    public const int AttackRecoveryFrames = 12;
    public const int MaximumAdvanceFrames = 18_000;
    private BattleDefinition definition;
    // Callback and snapshot order follows the original definition and allocation history.
    private readonly List<BattleCombatant> combatants;
    private readonly List<BattleCombatant> allies = [];
    private readonly List<BattleCombatant> enemies = [];
    private readonly Dictionary<string, BattleCombatant> byId;
    private readonly Dictionary<BattleStatusKind, IBattleStatusEffectHandler> handlers;
    private readonly Queue<BattleAttackCommand> actions = new();
    private readonly DiagnosticBattleEventLog events = [];
    private NativeBattleRandomState random;

    public BattleSession(BattleDefinition definition, BattleAtbMode mode) : this(definition, mode, [])
    {
    }

    public BattleSession(BattleDefinition definition, BattleAtbMode mode, IEnumerable<IBattleStatusEffectHandler> additionalHandlers) : this(definition, mode, additionalHandlers, true)
    {
    }

    private BattleSession(BattleDefinition definition, BattleAtbMode mode, IEnumerable<IBattleStatusEffectHandler> additionalHandlers, bool initializeNativeAi)
    {
        ValidateMode(mode);
        handlers = Enum.GetValues<BattleStatusKind>().ToDictionary(kind => kind, kind => (IBattleStatusEffectHandler)new ScaffoldBattleEffectHandler(kind));
        HashSet<BattleStatusKind> customKinds = [];
        foreach (IBattleStatusEffectHandler handler in additionalHandlers)
        {
            if (!Enum.IsDefined(handler.Kind) || handler.AtbRatePercent is < 1 or > 1_000 || !customKinds.Add(handler.Kind))
            {
                throw new ArgumentException("Duplicate or invalid status effect handler.", nameof(additionalHandlers));
            }
            handlers[handler.Kind] = handler;
        }
        BattleDefinition clonedDefinition = CloneDefinition(definition);
        GameplayTuning tuning = (clonedDefinition.Tuning ?? throw new InvalidDataException("Battle tuning is missing.")).DiscardLegacyStatusStatTuning();
        this.definition = NativeBattleStorageSnapshots.Capture(clonedDefinition with { StatusStatModifiers = StatusStatRules.Resolve(clonedDefinition.StatusStatModifiers), Tuning = tuning });
        nativeGil = this.definition.NativeGil;
        NativeCamera = this.definition.NativeStageCameraBank is null ? null : NativeBattleCameraState.Initialize();
        chocoboRewards = this.definition.ChocoboRewards.ObserveInventory(this.definition.NativeItemInventory ?? []);
        nativeCards = this.definition.NativeCards?.DeepCopy();
        nativeCrtRandom = this.definition.NativeCrtRandom;
        nativeBattleUiRandom = this.definition.NativeBattleUiRandom;
        nativeGlobalRandom = this.definition.NativeGlobalRandom;
        ValidateDefinition(this.definition);
        NativeGlobalSoundProcess = this.definition.NativeGlobalSoundProcess;
        NativeMusicProcess = this.definition.NativeMusicProcess;
        InitializeNativeRetainedMemory();
        nativeScannedModels = this.definition.NativeScannedModels;
        nativeKnownMagic = this.definition.NativeKnownMagic;
        combatants = this.definition.Combatants.Select(actor => new BattleCombatant(actor)).ToList();
        foreach (BattleCombatant actor in combatants)
        {
            (actor.IsPlayer ? allies : enemies).Add(actor);
            actor.NativeActionMemoryOwner = UsesExpandedRoster ? null : nativeActionMemory;
            actor.NativePresentationStatusOwner = UsesExpandedRoster ? null : nativePresentationStatusMemory;
            if (!actor.IsPlayer)
            {
                actor.ExpandedEnemyLevels = this.definition.ExpandedEnemyLevels?.GetValueOrDefault(actor.ModelId);
            }
            InitializeNativeAtb(actor);
        }
        Combatants = combatants.AsReadOnly();
        byId = combatants.ToDictionary(actor => actor.Id, StringComparer.Ordinal);
        InitializeBattleMagic(this.definition.MagicVitals, initializeNativeAi);
        RestoreNativeItems(this.definition.NativeItemInventory ?? ExpandItemStocks(this.definition.Items));
        foreach (BattleGuardianForceVital gf in this.definition.GuardianForces)
        {
            if (gf.Id is < 0 or >= 16 || gf.Acquired && gf.HitPoints > gf.MaximumHitPoints || !guardianForces.TryAdd(gf.Id, gf))
            {
                throw new InvalidDataException("Invalid battle GF HP owner.");
            }
        }
        if (initializeNativeAi)
        {
            if (UsesExpandedRoster)
            {
                foreach (BattleCombatant ally in allies)
                {
                    // 48B310 records whether the entry inventory contains any magic.
                    if (NativeAiDrawInventory(ally).Any(stock => stock.SpellId != 0))
                    {
                        ally.NativeStatus1 |= 1u << 30;
                    }
                    else
                    {
                        ally.NativeStatus1 &= ~(1u << 30);
                    }
                }
            }
            RefreshNativeEnemyDrawSlots();
        }
        random = this.definition.InitialRandom ?? NativeBattleRandomState.Initialize(this.definition.NativeSeed);
        random.Validate();
        persistentAiVariables = this.definition.PersistentAiVariables?.ToArray();
        Mode = mode;
        InitializeNativeSupportState();
        InitializeNativeBattleClock();
        int allySlot = 0;
        int enemySlot = 0;
        int nativeAllies = Math.Min(allies.Count, UsesExpandedRoster && this.definition.MagicVitals is { } vitals
            ? vitals.GetBattlePartyRows(NativeBattleSlots.AllyCount).Length
            : NativeBattleSlots.AllyCount);
        foreach (BattleCombatant actor in combatants.ToArray())
        {
            if (actor.IsPlayer)
            {
                actor.BindRosterSlot(BattleRosterSlot.Ally(allySlot), bindNative: allySlot < nativeAllies);
                allySlot++;
            }
            else
            {
                bool addedEnemy = IsAddedEnemy(actor);
                if (UsesOriginalEnemySlots && addedEnemy)
                {
                    int position = NativeBattleSlots.EnemyCount + actor.SourceDefinition.SceneSlot!.Value - NativeBattleSlots.FormationSlotCount;
                    actor.BindRosterSlot(BattleRosterSlot.Enemy(position), bindNative: false);
                }
                else
                {
                    actor.BindRosterSlot(actor.IsActive ? BattleRosterSlot.Enemy(enemySlot++) : null, bindNative: !addedEnemy);
                }
            }
            if (initializeNativeAi && actor.IsActive && HasNativeRosterSlot(actor) && !UsesExpandedRoster)
            {
                nativeActionMemory.State = nativeActionMemory.State.InitializeStatistics(actor.EncodeNativeSlot());
            }
            if (initializeNativeAi && UsesExpandedRoster)
            {
                actor.LastNativeAction = new(0, null, null, null, null, null);
            }
            if (initializeNativeAi && !UsesExpandedRoster)
            {
                PublishNativeElementAttack(actor);
            }
        }
        RefreshNativePacketBindings();
        if (initializeNativeAi)
        {
            RefreshNativeKnownMagicStocks();
            if (!UsesExpandedRoster && definition.NativeGraphics is null)
            {
                InitializeNativeStageCamera();
            }
            InitializeNativePrimitiveDepth47CE10();
            InitializeNativeBattleTimer47CCB0();
            InitializeNativeEntry();
            InitializeNativeGraphics();
            originalParticipantSlotsInitialized = true;
            InitializeNativeParticipants();
            if (UsesExpandedRoster)
            {
                InitializeNativeSupportCallbacks();
            }
        }
        if (nativeScriptState.ScriptedExitRequested)
        {
            CompleteBattleIfNeeded();
        }
        else
        {
            Outcome = DetermineOutcome();
        }
        RefreshPreparedPresentation();
    }

    public bool UsesExpandedRoster => definition.UsesExpandedRoster;
    private bool UsesOriginalEnemySlots => definition.UsesOriginalEnemySlots;

    public BattleDifficulty Difficulty { get; private set; } = BattleDifficulty.Normal;
    public BattleAtbMode Mode { get; private set; }
    public long Frame { get; private set; }
    public BattleOutcome Outcome { get; private set; }
    public byte? NativeResult => DebugOutcome is null && NativeGraphics?.NativeOutcome?.ResultByte6E7 is 1 or 2 or 4 ? NativeGraphics!.NativeOutcome!.ResultByte6E7 : Outcome switch { BattleOutcome.Active => null, BattleOutcome.Victory => 4, BattleOutcome.Escaped => 2, BattleOutcome.Defeat or BattleOutcome.ScriptedExit => 1, _ => throw new InvalidOperationException("Unknown native battle outcome.") };
    public bool CanReturnToField => Outcome is BattleOutcome.Victory or BattleOutcome.Escaped || Outcome == BattleOutcome.ScriptedExit && ((definition.NativeFieldBattleMode ?? throw new NotSupportedException("Native scripted exit requires the actual field battle-mode WORD182.")) & 0x200) != 0;
    public IReadOnlyList<BattleCombatant> Combatants { get; }
    public IReadOnlyList<BattleEvent> Events => events.AsReadOnly();
    public IReadOnlyList<BattleAttackCommand> QueuedActions => actions.Concat(nativeCommandBanks.BoundCommands).ToArray();
    public BattleActionPresentation? ActiveAction { get; private set; }
    public string Name => definition.Name;
    public int? EncounterId => definition.EncounterId;
    public IReadOnlyList<BattleStock> Items => nativeItems.Where(stock => stock.ItemId != 0 && stock.Count > 0).Select(stock => new BattleStock(stock.ItemId, stock.Count)).ToArray();
    public IReadOnlyList<BattleActionDefinition> ActionDefinitions => definition.ActionDefinitions.Select(action => action with { Statuses = action.Statuses?.ToArray() }).ToArray();

    public void SetDifficulty(BattleDifficulty difficulty)
    {
        if (!Enum.IsDefined(difficulty))
        {
            throw new ArgumentOutOfRangeException(nameof(difficulty));
        }
        if (Difficulty == BattleDifficulty.Hardcore && difficulty != BattleDifficulty.Hardcore)
        {
            throw new InvalidOperationException("Hardcore difficulty cannot be disabled.");
        }
        Difficulty = difficulty;
    }

    public void SetMode(BattleAtbMode mode)
    {
        ValidateMode(mode);
        if (mode != Mode)
        {
            StopTurnAdvance();
        }
        Mode = mode;
    }

    public void Tick()
    {
        RequireExpandedPresentationDispatchIdle();
        try
        {
            TickCore();
        }
        finally
        {
            RefreshNativeSlotsDeal();
        }
    }

    private void TickCore()
    {
        if (NativeGraphics is not null || expandedPresentationProcess is not null)
        {
            if (!nativeGraphicsPresentationDriven)
            {
                DispatchTurnBasedFrame();
                if (NativeGraphics is not null)
                {
                    AdvanceSkippedGuardianFormPresentation();
                }
            }
            return;
        }
        if (Outcome != BattleOutcome.Active)
        {
            if (!nativeGraphicsPresentationDriven)
            {
                AdvanceNativeGraphicsFrame();
            }
            return;
        }
        DispatchTurnBasedFrame();
    }

    private bool HasPendingTurn => nativeScriptState.WaitCounters.Length != 0 || QueuedActions.Count != 0 || ActiveAction is not null || nativeTimedAction is not null || nativeActionScratch.ComponentCompletion is not null || NativeScriptAllocationPending || NativeGraphics is { Requests.IsEmpty: false } || NativeGraphics is { Descriptors.Queue.IsEmpty: false } || HasPendingExpandedDoom;

    private bool HasGuardianForceTimeBoundary => guardianForceCasts.Values.Any(cast => cast.RemainingTicks == 0 || (byId[cast.ActorId].NativeStatus1 & 0x80000000) == 0);

    private bool ExpandedTimedLimitOwnsInput => nativeShotProcesses.Count != 0 || nativeRenzokukenProcesses.Count != 0 || nativeDuelProcesses.Count != 0;

    private bool CanExplicitlyAdvanceTurn => Outcome == BattleOutcome.Active && !HasPendingNativeOutcome && !HasPendingExpandedEscape && !HasPendingExpandedTerminal && deferredExpandedTerminalOutcome is null && !HasExpandedHostedGuardianFormEntry && NativeEntryReady && !HasGuardianForceTimeBoundary && nativeScriptState.WaitCounters.Length == 0 && !NativeScriptAllocationPaused && BattleClockUi?.PauseDE9 != true && (nativeBattleClock is null || !HasGraphicsClockOwner || nativeBattleClock.Active != 0) && (!UsesExpandedRoster || !ExpandedTimedLimitOwnsInput);

    private bool CanAdvanceTurnBasedTime => Outcome == BattleOutcome.Active && !HasPendingNativeOutcome && !HasPendingExpandedTerminal && NativeEntryReady && !HasPendingTurn && !HasGuardianForceTimeBoundary && BattleClockUi?.PauseDE9 != true && (nativeBattleClock is null || !HasGraphicsClockOwner || NativeBattleClockRules.Admit(nativeBattleClock, true, false, NativeScriptAllocationPaused).AtbAdmitted != 0);

    private bool CanAutomaticallyAdvanceTurn => Mode == BattleAtbMode.TurnBased && CanAdvanceTurnBasedTime && nativeShotProcesses.Count == 0 && nativeDuelProcesses.Count == 0 && nativeRenzokukenProcesses.Count == 0 && HasAutomaticTurnWaiter;

    private bool HasAutomaticTurnWaiter => diagnosticReadinessActor is { } selected ? CanNativeBattleActorAct(selected) && !selected.IsBusy && !selected.IsReady : !combatants.Any(actor => actor.IsReady) && combatants.Any(actor => IsNativeBattleCapable(actor) && !actor.Suspended);

    private void DispatchTurnBasedFrame()
    {
        if (expandedPresentationProcess is { } process)
        {
            DispatchExpandedPresentationFrame(process);
            return;
        }
        DispatchTurnBasedFrameCore();
    }

    private void DispatchTurnBasedFrameCore()
    {
        if (HasExpandedHostedGuardianFormEntry)
        {
            DispatchGuardianFormTransitions();
            CompleteBattleIfNeeded();
            return;
        }
        bool advancingExpandedEscape = HasPendingExpandedEscape;
        AdvanceNativeEscapeFrame();
        if (advancingExpandedEscape || HasPendingExpandedEscape || HasPendingExpandedTerminal)
        {
            return;
        }
        bool realTime = UsesRealTimeBattleClock;
        if (realTime)
        {
            StopTurnAdvance();
        }
        bool timeAdvanced = !realTime && ContinueTurnAdvance();
        if (!realTime && !timeAdvanced && CanAutomaticallyAdvanceTurn)
        {
            for (int count = 1; count <= MaximumAdvanceFrames; count++)
            {
                AdvanceTurnBasedTime();
                timeAdvanced = true;
                if (!CanAutomaticallyAdvanceTurn || HasGuardianForceTimeBoundary)
                {
                    break;
                }
                if (count == MaximumAdvanceFrames)
                {
                    throw new TimeoutException($"No combatant became ready within {MaximumAdvanceFrames} battle time steps.");
                }
            }
        }
        if (NativeGraphics is not null)
        {
            PumpNativeHostFrame(!timeAdvanced);
        }
        else if (WaitingForTurnBasedDecision && !HasPendingTurn && !combatants.Any(actor => actor.IsReady && (!actor.IsPlayer || UsesExpandedRoster && RequiresNativeCompelledCommand(actor))))
        {
            DispatchNativeScriptTasks();
            if (expandedPresentationProcess is not null)
            {
                CompleteBattleIfNeeded();
            }
        }
        else
        {
            DispatchFrame(!nativeGraphicsPresentationDriven, !timeAdvanced);
        }
    }

    public int AdvanceTurn(int maxFrames = MaximumAdvanceFrames)
    {
        RequireNoPendingSlotsDeal();
        if (Mode != BattleAtbMode.TurnBased)
        {
            throw new InvalidOperationException("Advance Turn requires Turn-Based mode.");
        }
        if (maxFrames is < 1 or > MaximumAdvanceFrames)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFrames));
        }
        if (expandedHostedEvent is not null || !CanExplicitlyAdvanceTurn)
        {
            return 0;
        }
        if (expandedPresentationProcess is not null && DetermineOutcome() != BattleOutcome.Active)
        {
            CompleteBattleIfNeeded();
            return 0;
        }
        HashSet<string> waiting = combatants.Where(actor => IsNativeBattleCapable(actor) && !actor.Suspended && (!actor.IsBusy || guardianForceCasts.ContainsKey(actor.Id) && (actor.NativeCombatantFlags & 0x400) == 0) && !actor.IsReady).Select(actor => actor.Id).ToHashSet(StringComparer.Ordinal);
        if (waiting.Count == 0)
        {
            return 0;
        }
        // Logical time runs on owned state so a bounded failure cannot publish partially advanced gauges or effects.
        BattleSession candidate = Restore(Capture(), handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler));
        candidate.deferExpandedHostedTerminal = expandedPresentationProcess is not null;
        candidate.stagingExpandedHostedGuardianForms = expandedPresentationProcess is not null;
        if (nativeBattleClock is not null && expandedPresentationProcess is not null)
        {
            candidate.expandedTurnClockObservation = ExpandedClockUi ?? throw new InvalidDataException("Hosted turn advance requires its initialized UI services.");
        }

        candidate.StopTurnAdvance();
        return BoundedBattleAdvance.Run<BattleSession, int>(candidate, maxFrames,
            (next, count) =>
            {
                HashSet<string> becameReady = next.AdvanceTurnBasedTime(explicitAdvance: true);
                return !next.CanExplicitlyAdvanceTurn || becameReady.Overlaps(waiting) || next.combatants.Any(actor => waiting.Contains(actor.Id) && actor.IsReady) || next.HasGuardianForceTimeBoundary || next.HasPendingExpandedDoom ? count : null;
            },
            (next, count) =>
            {
                next.AwaitAdvancedEnemyTurn(waiting, maxFrames - count);
                PreparedHostedGuardianFormChange? form = PrepareStagedHostedGuardianForm(expandedPresentationProcess, next);
                if (next.deferredExpandedTerminalOutcome is BattleOutcome outcome)
                {
                    ExpandedBattlePresentationProcess process = expandedPresentationProcess ?? throw new InvalidOperationException("The prepared terminal result lost its attached presentation owner.");
                    ExpandedBattlePresentationProcess.PreparedTerminal preparation = process.PrepareTerminal(this, outcome, form);
                    PublishAdvanceTurnCandidateWithTerminal(next, process, preparation);
                    return;
                }
                if (form is not null)
                {
                    ExpandedBattlePresentationProcess process = expandedPresentationProcess!;
                    process.ValidatePreparedGuardianFormChange(form);
                    PublishValidatedCandidate(next, null, () =>
                    {
                        process.CommitGuardianFormChange(form);
                        PublishHostedGuardianFormAudio(form);
                    });
                    return;
                }
                PublishCandidate(next);
            },
            $"No living unready combatant became ready within {maxFrames} dispatched battle frames.");
    }

    public void QueueAttack(string actorId, string targetId)
    {
        RequireNoPendingSlotsDeal();
        RequireNativeEntryReady();
        RequireNormalGuardianFormCommands(actorId);
        BattleCombatant actor = GetActor(actorId);
        BattleCombatant target = GetActor(targetId);
        if (Outcome != BattleOutcome.Active || UsesExpandedRoster && actor.IsPlayer && (ExpandedTimedLimitOwnsInput || RequiresNativeCompelledCommand(actor)) || !actor.IsReady || !CanNativeBattleActorAct(actor) || !target.IsActive || !target.IsAlive || target.Untargetable || !actor.Abilities.Contains(BattleAbility.Attack))
        {
            throw new InvalidOperationException("Attack requires a ready living actor with Attack and a living target in an active battle.");
        }
        if (!UsesExpandedRoster && actor.IsPlayer && PhysicalCommand(actor) == 12)
        {
            QueueNativePlayerCommand(actorId, targetId, 12, 0);
            return;
        }
        EnqueueAttack(actor, target);
    }

    public void InstantVictory()
    {
        RequireNoPendingSlotsDeal();
        if (Outcome != BattleOutcome.Active || HasPendingExpandedTerminal)
        {
            throw new InvalidOperationException("Debug victory requires an active battle.");
        }
        foreach (BattleCombatant enemy in combatants.Where(actor => !actor.IsPlayer))
        {
            enemy.CurrentHp = 0;
            enemy.NativeStatus0 |= 1;
            enemy.Atb = 0;
        }
        DebugOutcome = BattleOutcome.Victory;
        Log("debug-victory", null, null, 0, "Debug shortcut: all enemies defeated.");
        CompleteBattleIfNeeded();
    }

    public void InstantDefeat()
    {
        RequireNoPendingSlotsDeal();
        if (Outcome != BattleOutcome.Active || HasPendingExpandedTerminal)
        {
            throw new InvalidOperationException("Debug defeat requires an active battle.");
        }
        foreach (BattleCombatant actor in combatants.Where(actor => actor.IsPlayer))
        {
            actor.CurrentHp = 0;
            actor.NativeStatus0 |= 1;
            actor.Atb = 0;
        }
        DebugOutcome = BattleOutcome.Defeat;
        Log("debug-defeat", null, null, 0, "Debug shortcut: all party members defeated.");
        CompleteBattleIfNeeded();
    }

    public BattleDefinition ReadDefinition() => CloneDefinition(definition);

    public BattleSnapshot Capture()
    {
        RequireEncounterDamagePublicationBoundary();
        return NativeBattleStorageSnapshots.Capture(CaptureState());
    }

    private BattleSnapshot CaptureState()
    {
        RequireEncounterDamagePublicationBoundary();
        return new BattleSnapshot
        {
            DiagnosticTerminalHold = DiagnosticTerminalHold,
            OriginalStockDrawOperations = originalStockDrawOperations.ToArray(),
            TurnAdvance = turnAdvance,
            Escape = nativeEscape,
            Definition = CloneDefinition(definition),
            GuardianForms = CaptureGuardianForms(),
            MagicVitals = magicVitals?.DeepCopy(),
            NativeEntryAdvantage = NativeEntryAdvantage,
            NativeCamera = NativeCamera,
            NativeGraphics = NativeGraphics,
            NativeTimedAction = CaptureNativeTimedAction(),
            NativeGuardianBoost = nativeGuardianBoost,
            NativeReactionOrder = nativeReactionOrder,
            NativeActionPackets = nativeActionPackets,
            NativeCommandBanks = nativeCommandBanks,
            NativePartyStatusMirrors = nativePartyStatusMirrors,
            NativePartyPresentation = nativePartyPresentation,
            NativeEffectIoResult = NativeEffectIoResult,
            NativeFileIo = NativeFileIo,
            NativeVibrationProcess = NativeVibrationProcess,
            NativeGlobalSoundProcess = NativeGlobalSoundProcess,
            NativeMusicProcess = NativeMusicProcess,
            NativeEffectMemory = NativeEffectMemory,
            NativeStage = NativeStage,
            NativeModelTextureCache = NativeModelTextureCache,
            NativeModelLoader = NativeModelLoader,
            NativeFileStaging = NativeFileStaging,
            NativeModelCpuResources = NativeModelCpuResources,
            NativeActionMemory = nativeActionMemory.State,
            NativePresentationStatusMemory = nativePresentationStatusMemory.State,
            NativeActionScratch = nativeActionScratch,
            NativeElementAttack = nativeElementAttack,
            ChocoboRewards = chocoboRewards,
            NativeCards = nativeCards?.DeepCopy(),
            NativeCrtRandom = nativeCrtRandom,
            NativeBattleUiRandom = nativeBattleUiRandom,
            NativeGlobalRandom = nativeGlobalRandom,
            NativeGlobalRandomCalls = nativeGlobalRandomCalls,
            Difficulty = Difficulty,
            Mode = Mode,
            GunbladeMode = GunbladeMode,
            Frame = Frame,
            NativeHostCallbacks = NativeHostCallbacks,
            NativeExamTimer = NativeExamTimer,
            Outcome = Outcome,
            DebugOutcome = DebugOutcome,
            Combatants = combatants.Select(actor => new BattleCombatantSnapshot(actor.Id, actor.HumanHitPoints, actor.Atb, actor.StatusEffects.ToArray()) { NativeAtb = actor.NativeAtb, LimitTurnCrisis = actor.LimitTurnCrisis, GuardianForceCompatibility = actor.GuardianForceCompatibility?.ToArray(), NativeMagicInventory = actor.NativeMagicInventory.ToArray(), NativeDrawSlots = actor.NativeDrawSlots.ToArray(), DrawnMagicIds = actor.DrawnMagicIds.ToArray(), NativeParticipant = CaptureNativeParticipant(actor), NativeStatus = CaptureNativeStatus(actor) }).ToArray(),
            NativeAllocations = CaptureNativeAllocations(),
            OriginalParticipantSlotsInitialized = originalParticipantSlotsInitialized,
            EncounterAllocation = CaptureEncounterAllocation(),
            EncounterPolicies = encounterPolicies,
            EncounterArrivals = CaptureEncounterArrivals(),
            EncounterNativeEvents = CaptureEncounterNativeEvents(),
            QueuedActions = actions.ToArray(),
            ActiveAction = ActiveAction,
            Events = events.ToArray(),
            NativeItemInventory = nativeItems.ToArray(),
            NativeGil = nativeGil,
            NativeScannedModels = nativeScannedModels,
            ScanKnowledge = scanKnowledge,
            NativeKnownMagic = nativeKnownMagic,
            NativeSupport = nativeSupport,
            PendingEnemyDraw = pendingEnemyDraw,
            NativeClock = nativeBattleClock,
            EnemyDefeats = enemyDefeats.ToArray(),
            Random = random,
            NativeAiVariables = combatants.Any(actor => actor.NativeAi is not null) ? nativeAiVariables.ToArray() : null,
            PersistentAiVariables = persistentAiVariables?.ToArray(),
            ScriptedRewards = scriptedRewards.ToArray(),
            NativeRewardItems = nativeRewardItems.ToArray(),
            NativeLimits = CaptureNativeLimits(),
            PendingSlotsDeal = PendingSlotsDeal,
            GuardianForces = GuardianForces.ToArray(),
            GuardianForceCasts = GuardianForceCasts.ToArray(),
            NativeLastAllocatedTarget = nativeLastAllocatedTarget,
            NativeScriptState = NativeScriptState,
            CustomHandlers = handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).Select(handler => handler.Kind).Order().ToArray()
        };
    }

    public static BattleSession Restore(BattleSnapshot snapshot, IEnumerable<IBattleStatusEffectHandler>? additionalHandlers = null)
    {
        return RestoreSnapshot(snapshot, additionalHandlers, null);
    }

    private static BattleSession RestoreSnapshot(BattleSnapshot snapshot, IEnumerable<IBattleStatusEffectHandler>? additionalHandlers, HostedCandidateProof? hostedProof)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var completion = hostedProof as HostedEventCompletionCandidateProof;
        var duel = hostedProof as HostedDuelCandidateProof;
        var renzokuken = hostedProof as HostedRenzokukenCandidateProof;
        if (duel is not null)
        {
            ValidateExpandedHostedDuelProof(snapshot, duel);
        }
        if (renzokuken is not null)
        {
            ValidateExpandedHostedRenzokukenProof(snapshot, renzokuken);
        }
        if (completion is not null && (!ReferenceEquals(snapshot, completion.Snapshot) || !snapshot.Definition.UsesExpandedRoster || snapshot.NativeGraphics is not null || snapshot.Outcome != BattleOutcome.Active ||
            completion.Event.CompletionToken != completion.Receipt.Token || completion.Event.Callback != completion.Receipt.Callback || completion.Event.Command.ActorId != completion.Receipt.ActorId))
        {
            throw new InvalidDataException("A hosted completion candidate requires its exact pending logical event and retired callback.");
        }
        try
        {
            BattleDefinition snapshotDefinition = snapshot.Definition;
            snapshotDefinition.Tuning?.RequireCurrentBattleStatusRules();
            snapshot = NativeBattleStorageSnapshots.Restore(snapshot with { Definition = null! }) with { Definition = snapshotDefinition };
            if ((snapshot.Definition.MagicVitals is not null) != (snapshot.MagicVitals is not null))
            {
                throw new InvalidDataException("A battle checkpoint with magic rules requires its authoritative banks and reservations.");
            }
            BattleSession session = new(snapshot.Definition, snapshot.Mode, additionalHandlers ?? [], false);
            session.InitializeBattleMagic(snapshot.MagicVitals, false);
            session.RestoreNativeAllocations(snapshot.NativeAllocations);
            session.originalParticipantSlotsInitialized = snapshot.OriginalParticipantSlotsInitialized;
            session.RestoreNativeEntry(snapshot);
            session.RestoreNativeCamera(snapshot);
            session.expandedHostedEvent = completion?.Event ?? duel?.Event ?? renzokuken?.Event;
            session.RestoreValidated(snapshot, (hostedProof as HostedTerminalCandidateProof)?.Terminal, renzokuken?.Trigger);
            session.ValidateBattleMagicSnapshot(completion is null ? null : snapshot.ActiveAction, completion?.Event.Command.MagicReservationId);
            session.expandedHostedEvent = null;
            session.RefreshPreparedPresentation();
            return session;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or NullReferenceException or KeyNotFoundException)
        {
            throw new InvalidDataException("Invalid battle snapshot.", error);
        }
    }

    private HashSet<string> DispatchFrame(bool advanceGraphics = true, bool advanceFrame = true)
    {
        try
        {
            return DispatchFrameCore(advanceGraphics, advanceFrame);
        }
        finally
        {
            RefreshNativeSlotsDeal();
        }
    }

    private HashSet<string> DispatchFrameCore(bool advanceGraphics, bool advanceFrame)
    {
        HashSet<string> becameReady = [];
        if (HasPendingExpandedTerminal)
        {
            return becameReady;
        }
        if (advanceGraphics)
        {
            AdvanceNativeGraphicsFrame();
        }
        if (DispatchNativeEntryCore() || NativeGraphics is { EntryReady: false })
        {
            return becameReady;
        }
        if (HasPendingNativeOutcome)
        {
            if (NativeGraphics!.NativeOutcome!.FadeDurationB0C == 0)
            {
                Outcome = PendingNativeOutcome;
                CompleteBattleIfNeeded();
                return becameReady;
            }
            DispatchNativeScriptTasks();
            CompleteBattleIfNeeded();
            return becameReady;
        }
        if (advanceFrame && (NativeGraphics is null || !WaitingForTurnBasedDecision || HasPendingTurn))
        {
            Frame = checked(Frame + 1);
        }
        if (DispatchNativeWaits())
        {
            if (nativeBattleClock is not null && HasGraphicsClockOwner)
            {
                AdmitNativeBattleClock(true, true);
            }
            AdvanceNativeSequenceDuringWait();
            DispatchNativeScriptTasks();
            return becameReady;
        }
        // TODO(TD-009): Components without the raw descriptor bank retain the existing gauge clock.
        bool clockAdmitted = UsesRealTimeBattleClock && (!UsesExpandedRoster || !ExpandedTimedLimitOwnsInput) && (HasPolledNativeBattleClock ? nativeBattleClock!.AtbAdmitted != 0 : nativeBattleClock is null || NativeGraphics is null || AdmitNativeBattleClock(true, false));
        if (clockAdmitted)
        {
            AdvanceBattleTime(becameReady, !HasPolledNativeBattleClock);
        }
        CancelDeadActions();
        CompleteBattleIfNeeded();
        if (Outcome != BattleOutcome.Active || HasPendingNativeOutcome || HasPendingExpandedTerminal)
        {
            return becameReady;
        }
        DispatchGuardianFormTransitions();
        DispatchNativeCompelledActors();
        foreach (BattleCombatant enemy in combatants.Where(actor => !actor.IsPlayer && actor.IsReady && CanNativeBattleActorAct(actor)).ToArray())
        {
            if (diagnosticHoldEnemyCommands || UsesExpandedRoster && ExpandedTimedLimitOwnsInput)
            {
                continue;
            }
            if (HasNativeUiServiceClock)
            {
                //Raw4842B0 owns full-gauge registration;4847B0 below publishes its deferred callback without resetting the gauge.
                continue;
            }
            if (enemy.NativeAi is not null)
            {
                enemy.Atb = 0;
                if (UsesExpandedRoster)
                {
                    QueueExpandedAiCallback(enemy, 1);
                }
                else
                {
                    QueueNativeCallback(enemy, 1, 2);
                }
                continue;
            }
            // TODO(TD-009): First living opponent is scaffold AI until native enemy decision programs are implemented.
            BattleCombatant target = combatants.First(actor => actor.IsPlayer && actor.IsAlive);
            EnqueueAttack(enemy, target);
        }
        if (HasNativeUiServiceClock)
        {
            //Ordinary4847F0 inputs are already folded into their physical banks; consume200 before event dispatch.
            PublishNativeRegisteredCallbacks4847B0();
        }
        DispatchAction();
        FlushEncounterCounterCommands();
        DispatchNativeTriggerReceipt();
        if (clockAdmitted)
        {
            DispatchNativeStatusTimers();
        }
        DispatchNativePeriodicSupport(clockAdmitted);
        DispatchNativeScriptTasks();
        CancelDeadActions();
        CompleteBattleIfNeeded();
        return becameReady;
    }

    private void DispatchAction()
    {
        if (HasExpandedHostedGuardianFormEntry || nativeTimedAction is not null || UsesExpandedRoster && nativeDuelProcesses.Values.Any(duel => duel.Phase == BattleNativeDuelPhase.Ready))
        {
            return;
        }
        if (ActiveAction is null)
        {
            if (UsesExpandedRoster && TryDispatchPendingExpandedDoom())
            {
                return;
            }
            if (NativeGraphics is { Requests.IsEmpty: false } && !HasCurrentNativeQueuedPresentation())
            {
                return;
            }
            while (TryTakeQueuedCommand(out BattleAttackCommand command))
            {
                if (command.ExpandedAiCallback is { } expandedCallback)
                {
                    DispatchExpandedAiCallback(command, expandedCallback);
                    return;
                }
                if (command.ExpandedAiDescriptor is { } descriptor)
                {
                    DispatchExpandedAiDescriptor(command, descriptor);
                    return;
                }
                ObserveNativeUnknownStackCallback(0x485460, "new action dispatch");
                try
                {
                    if (command.NativeCallback is { } callback)
                    {
                        DispatchNativeCallback(callback);
                        return;
                    }
                    BattleCombatant actor = byId[command.ActorId];
                    if (UsesExpandedRoster)
                    {
                        if (command.NativeRenzokuken is not null && expandedPresentationProcess is { } renzokukenHost)
                        {
                            PrepareAndPublishExpandedHostedRenzokuken(actor, command, renzokukenHost);
                        }
                        else if (command.NativeRenzokuken is not null)
                        {
                            StartNativeRenzokuken(command);
                        }
                        else if (command.NativeDuel is not null)
                        {
                            StartExpandedDuelInvocation(actor, command);
                        }
                        else
                        {
                            StartExpandedAction(actor, command);
                        }
                        return;
                    }
                    if (command.NativeHeader is not null)
                    {
                        ValidateCurrentNativeQueuedHeader(command);
                    }
                    if (command.NativeHeader is null && (command.GuardianFormAction is not null || command.GuardianFormInvocation is not null) && GetGuardianForm(actor.Id) is null)
                    {
                        actor.IsBusy = false;
                        Log("Cancelled", actor.Id, command.TargetId, 0, "GF form invocation was cancelled.");
                        continue;
                    }
                    if (command.NativeHeader is null)
                    {
                        command = PrepareNativeQueuedCommand(actor, command);
                    }
                    if (!CanExecuteMagicReservation(actor, command))
                    {
                        RefundCancelledNativeItem(command);
                        actor.IsBusy = false;
                        Log("Cancelled", actor.Id, command.TargetId, 0, "Magic cancelled because its reserved resource or caster is no longer available.");
                        continue;
                    }
                    if (command.NativeRenzokuken is not null)
                    {
                        StartNativeRenzokuken(command);
                        return;
                    }
                    if (NativeGraphics is not null && command.Ability == BattleAbility.GuardianForce && command.NativeQueuedCommand == 3)
                    {
                        BeginNativeBattleActionPause();
                        QueueGuardianForce(actor, command with { NativeQueuedCommand = 254 });
                        QueueNativeActionClockCompletion(0x47DDA0);
                        return;
                    }
                    BattleCombatant target = byId[command.TargetId];
                    if (command.NativeHeader is null && (!CanNativeSupportCommandAct(actor, command.NativePlayerReaction) || !(command.Ability == BattleAbility.Draw ? target.IsAlive : ActionHasValidTarget(command.TargetRequest, command.NativeGraphic))))
                    {
                        RefundCancelledNativeItem(command);
                        actor.IsBusy = false;
                        Log("Cancelled", actor.Id, target.Id, 0, "Attack cancelled because a participant was defeated.");
                        continue;
                    }
                    if (command.NativeHeader is null)
                    {
                        command = PrepareNativeDuelInvocation(actor, command);
                        command = PrepareNativeDrawCommand(actor, command);
                        command = PrepareNativeSpecialCommand(actor, command);
                        command = PrepareIndependentGuardianItem(actor, command);
                        PrepareNativeLimitCommand(command);
                    }
                    command = ProduceNativeQueuedCommand(actor, target, command);
                    DispatchNativeSecondaryBankCommand();
                    command = PrepareNativeGraphicsAction(actor, command);
                    BeginNativeBattleActionPause();
                    ActiveAction = new BattleActionPresentation(actor.Id, target.Id, BattleActionPhase.Windup, NativeActionWindupFrames(actor, command.NativeSequence), command.TargetRequest, command.Ability, command.ActionId, command.DrawCast) { MagicReservationId = command.MagicReservationId, SmartAttack = command.SmartAttack, NativeHeader = command.NativeHeader, NativeEventOwner = command.NativeEventOwner, NativeDuel = command.NativeDuel, NativeSlots = command.NativeSlots, NativeAiCallbackStage = command.NativeAiCallbackStage, NativePlayerReaction = command.NativePlayerReaction, NativeDraw = command.NativeDraw, DrawCastTargetId = command.DrawCastTargetId, NativeLimitCallback = command.NativeLimitCallback, MagicCasts = command.MagicCasts, NativeMagicCastMode = command.NativeMagicCastMode, NativeSequenceFrame = NativeGraphics is not null || NativeControlTrack(actor, command.NativeSequence) is null ? null : 0, NativeGraphic = command.NativeGraphic, IndependentGuardian = command.IndependentGuardian, NativeSpecial = command.NativeSpecial, NativeQueuedCommand = command.NativeQueuedCommand, NativeAction = command.NativeAction, NativeSequence = command.NativeSequence, GuardianFormAction = command.GuardianFormAction, GuardianFormInvocation = command.GuardianFormInvocation, FreeNativeMagic = command.FreeNativeMagic, ReservedNativeItemSlot = command.ReservedNativeItemSlot };
                    BeginComponentGuardianBoost(ActiveAction);
                    Log("Windup", actor.Id, target.Id, 0, $"{actor.Name}: {command.NativeAction?.Name ?? ActionName(command.Ability, command.ActionId)} → {target.Name}.");
                    if (command.NativeHeader is not null && command.NativeQueuedCommand is not (0 or 247) && command.NativeAiCallbackStage is null && command.NativePlayerReaction is null && nativeActionScratch.ComponentCompletion is null)
                    {
                        if (NativeGraphics is { } duelGraphics && nativeDuelProcesses.TryGetValue(actor.Id, out BattleNativeDuelState? duel) && duel.Selection1D28DEF is not null)
                        {
                            if (command.NativeDuel is not null)
                            {
                                NativeGraphics = QueueNativeDescriptor(duelGraphics, 10, 0x4852B0);
                            }
                        }
                        else
                        {
                            QueueNativeActionClockCompletion(0x47DE70);
                        }
                    }
                    PublishNativeSequenceMessages(actor, command.NativeSequence, 0);
                    return;
                }
                finally
                {
                    ReleaseNativeDispatchedCommand();
                }
            }
            return;
        }
        BattleActionPresentation action = ActiveAction;
        if (UsesExpandedRoster)
        {
            AdvanceExpandedAction(action);
            return;
        }
        int remaining;
        if (action.Phase == BattleActionPhase.Windup && NativeGraphics is { } graphics)
        {
            //Factual104 headers complete at their successful descriptor10 event; AA is a contact signal, not event completion.
            remaining = action.NativeHeader is not null ? 1 : graphics.ActionCallbackReady ? 0 : 1;
        }
        else if (action.Phase == BattleActionPhase.Windup && NativeGraphics is null && nativeGuardianBoost is not null && action.NativeQueuedCommand == 254)
        {
            remaining = Math.Max(1, action.RemainingFrames - 1);
        }
        else if (action.Phase == BattleActionPhase.Windup && action.NativeSequenceFrame is not null)
        {
            action = AdvanceNativeControlSequence(action);
            remaining = action.NativeSequenceFrame >= NativeControlTrack(byId[action.ActorId], action.NativeSequence)!.CompletionFrame ? 0 : action.RemainingFrames;
        }
        else
        {
            remaining = action.RemainingFrames - 1;
        }
        if (remaining > 0)
        {
            ActiveAction = action with { RemainingFrames = remaining };
            return;
        }
        ObserveNativeUnknownStackCallback(0x485460, "active action advancement");
        if (action.Phase == BattleActionPhase.Windup)
        {
            CompleteNativeComponentTrigger(action);
            BattleCombatant actor = byId[action.ActorId];
            BattleCombatant target = byId[action.TargetId];
            if (action.NativeHeader is null && CanNativeSupportCommandAct(actor, action.NativePlayerReaction) && ActionHasValidTarget(action.TargetRequest, action.NativeGraphic))
            {
                ResolveActionTransaction(actor, target, action);
            }
            else if (action.NativeHeader is null)
            {
                RefundCancelledNativeItem(action);
            }
            ActiveAction = action with { Phase = BattleActionPhase.Recovery, RemainingFrames = AttackRecoveryFrames };
            return;
        }
        CompleteActionPresentation(action, "Attack recovery completed.");
    }

    private void CompleteActionPresentation(BattleActionPresentation action, string message, bool completeActiveAction = true)
    {
        byId[action.ActorId].IsBusy = CompleteNativeDuelAction(action) || CompleteNativeShotAction(action) || QueuedActions.Any(command => command.NativeCallback is null && command.ActorId == action.ActorId) || !completeActiveAction && ActiveAction?.ActorId == action.ActorId;
        PublishNativeDrawComponentResult(action);
        CompleteNativeComponentPacketPresentation(action);
        if (completeActiveAction)
        {
            CompleteExpandedHostedActionPause();
            ActiveAction = null;
            CompleteNativeGraphicsAction();
        }
        if (action.GuardianFormInvocation is { Mode: GuardianFormInvocationMode.FinisherWithoutEntry } invocation && GetGuardianForm(action.ActorId) is { Phase: BattleGuardianFormPhase.Finishing } form && form.InvocationId == invocation.Id)
        {
            ReturnGuardianForm(byId[action.ActorId], false);
        }
        if (GetGuardianForm(action.ActorId) is { Phase: BattleGuardianFormPhase.Returning })
        {
            ReturnGuardianForm(byId[action.ActorId], (NativeSavedStatusBits(byId[action.ActorId]) & 1) != 0);
        }
        Log("ActionComplete", action.ActorId, action.TargetId, 0, message);
        CompleteEncounterPolicyPresentation(action);
    }

    private void EnqueueAttack(BattleCombatant actor, BattleCombatant target)
    {
        byte physical = UsesExpandedRoster && actor.IsPlayer && PhysicalCommand(actor) == 12 ? (byte)12 : (byte)1;
        BattleNativeAction? native = physical == 12 ? FindNativeCommandAction(actor, 12, 0) : null;
        StopTurnAdvance();
        OnNativePlayerCommandSubmitted(actor, physical);
        nativeLimitLevels.Remove(actor.Id);
        actor.Atb = 0;
        actor.IsBusy = true;
        BattleTargetRequest request = UsesExpandedRoster ? SelectBattleTarget(target, eligibility: BattleTargetEligibility.Living)
            : new(NativeBattleTargetRequestAdapter.SelectOriginalSlot(NativeBattleSlots.Decode(target.EncodeNativeSlot())), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.UseCurrentBattlePolicy);
        BattleAttackCommand command = new(actor.Id, target.Id, request) { NativeQueuedCommand = physical, NativeAction = native };
        if (actor.IsPlayer && !UsesExpandedRoster)
        {
            EnqueueNativeInputCommand(command);
        }
        else
        {
            actions.Enqueue(command);
        }
        Log("Queued", actor.Id, target.Id, 0, $"{actor.Name}: Attack queued.");
    }

    private void ApplyDamage(BattleCombatant? actor, BattleCombatant target, int amount, string source, bool counterEligible = true, bool publishDamage = true, bool bypassDamageLimit = false, EncounterHpCause cause = EncounterHpCause.Damage)
    {
        if (!bypassDamageLimit)
        {
            amount = Math.Min(amount, definition.Tuning.MaximumDamage);
        }
        bool ownsTransaction = nativeActionCommitDepth == 0;
        if (ownsTransaction)
        {
            if (nativeProductionContext is null)
            {
                BeginNativeReactionBatch();
            }
            nativeActionCommitDepth++;
        }
        try
        {
            if (!TryApplyEncounterDamage(actor, target, amount, source, counterEligible, publishDamage, cause))
            {
                ApplyDamageCore(actor, target, amount, source, counterEligible, publishDamage);
            }
            if (nativeContactWorking is not null)
            {
                RefreshNativePresentationStatus(target);
            }
        }
        finally
        {
            if (ownsTransaction)
            {
                nativeActionCommitDepth--;
                if (UsesExpandedRoster)
                {
                    FlushExpandedEnemyReactions();
                }
                else
                {
                    FlushNativeReactions();
                }
            }
        }
    }

    private void ApplyDamageCore(BattleCombatant? actor, BattleCombatant target, int amount, string source, bool counterEligible, bool publishDamage)
    {
        if (!target.IsActive)
        {
            return;
        }
        if (nativeContactWorking?.CommittingSecondary != true)
        {
            target.LastNativeHealing = amount == 0 ? (byte)1 : (byte)0;
        }
        NoteNativeContactCommit(target);
        if (UsesExpandedRoster && target.CurrentHp == 0 && target.AiDeathHandled && target == nativeContactWorking?.Actor &&
            resolvingNativeAction is { NativeAiCallbackStage: 3, TargetRequest.DeathCallbackSelf: { } deathSource } && deathSource == TargetActorReference(target))
        {
            return;
        }
        if (ApplyGuardianForceShield(target, amount))
        {
            return;
        }
        //48FE20 passes its original action actor as494410.a5 for both halves.
        //Return Damage consumes formula magnitude before HP/KO clamping.
        BattleCombatant? nativeSource = nativeContactWorking?.Actor ?? actor;
        int nativeMagnitude = nativeContactWorking is { CommittingSecondary: false } contact ? contact.Magnitude : amount;
        AccumulateNativeReturnDamage(target, nativeSource, nativeMagnitude);
        if (ApplyGuardianFormDamage(actor, target, amount, source, counterEligible, publishDamage))
        {
            return;
        }
        int applied = (int)Math.Min(unchecked((uint)target.CurrentHp), checked((uint)amount));
        bool nativeDeath = (NativeSavedStatusBits(target) & 1) != 0 || (NativeBattleStatusBits(target) & 0x10000) != 0 || applied == target.CurrentHp;
        bool finalizeDeath = nativeDeath && NativeDeathContactAllowed(target);
        ushort? deathCount = target.IsPlayer && finalizeDeath ? NextNativePlayerDeathCount(target) : null;
        int diagnosticBeforeHp = target.CurrentHp;
        target.CurrentHp = nativeDeath ? 0 : unchecked(target.CurrentHp - applied);
        ObserveDiagnosticResolution("damage", actor, target, amount, diagnosticBeforeHp, target.CurrentHp);
        if (target.CurrentHp == 0 && finalizeDeath)
        {
            target.NativeStatus0 |= 1;
        }
        bool storeNativeMemory = nativeContactWorking is null || target.CurrentHp != 0 && NativeSurvivingContactStoresMemory(target) || finalizeDeath && !target.IsPlayer && (target.NativeCombatantFlags & 0x20) != 0 && (NativeBattleStatusBits(target) & 0x10000) == 0;
        if (storeNativeMemory && actor is not null && (resolvingNativeAction ?? ActiveAction) is { } activeAction)
        {
            RecordNativeActionMemory(nativeSource ?? actor, target, activeAction);
        }
        if (publishDamage)
        {
            Log("Damage", actor?.Id, target.Id, applied, $"{source}: {target.Name} takes {applied} damage.");
        }
        if (!target.IsAlive && finalizeDeath)
        {
            MarkNativeContactDeath(target);
            if (target.IsPlayer)
            {
                target.NativeDeathCount = deathCount;
                RemoveNativeStatuses(target, 0x37E, 0x38E7FFF);
            }
            ObserveEnemyDefeat(actor, target);
            if (target.IsPlayer && ShouldRunNativePlayerDeathSupport(target, actor))
            {
                if (actor is not null && (resolvingNativeAction ?? ActiveAction) is { } deathAction)
                {
                    RecordNativeActionMemory(nativeSource ?? actor, target, deathAction);
                }
                QueueNativeReaction(target, 3);
            }
            if (target.NativeAi is not null && !target.AiDeathHandled && (target.NativeCombatantFlags & 0x20) != 0 && (NativeBattleStatusBits(target) & 0x10000) == 0)
            {
                QueueNativeReaction(target, 3);
                RunEncounterImmediateCounter(target, death: true);
                if (!UsesExpandedRoster)
                {
                    target.NativeStatus0 &= 0xFFFE;
                }
            }
            if (nativeContactWorking is null || NativeGraphics is null)
            {
                //TODO(TD-009): Components without the graphical bank still project493840 at logical death; raw contacts reset readiness only when493D80 presents them.
                target.Atb = 0;
                target.IsBusy = false;
            }
            Log("Defeated", actor?.Id, target.Id, 0, $"{target.Name} is defeated.");
        }
        else if (target.IsAlive && (target.IsPlayer || target.NativeAi is not null))
        {
            if (counterEligible && actor is not null)
            {
                QueueNativeReaction(target, 2);
            }
            if (counterEligible && actor is not null && !target.IsPlayer && NativeImmediateCounterAllowed(target) && (target.NativeCombatantFlags & 0x10) != 0)
            {
                RunEncounterImmediateCounter(target);
            }
        }
    }

    private void ApplyHealing(BattleCombatant actor, int amount, string source)
    {
        if (TryDeferEncounterHealing(actor, amount, source))
        {
            return;
        }
        if (nativeContactWorking?.CommittingSecondary != true)
        {
            actor.LastNativeHealing = 1;
        }
        if (!actor.IsAlive)
        {
            return;
        }
        if (actor.GuardianFormVitals is not null)
        {
            MarkGuardianFormContact(actor);
        }
        int applied = Math.Min(actor.MaximumHp - actor.CurrentHp, amount);
        int diagnosticBeforeHp = actor.CurrentHp;
        actor.CurrentHp += applied;
        ObserveDiagnosticResolution("healing", null, actor, amount, diagnosticBeforeHp, actor.CurrentHp);
        if (applied != 0)
        {
            Log("Healing", actor.Id, actor.Id, applied, $"{source}: {actor.Name} recovers {applied} HP.");
        }
    }

    private void CancelDeadActions()
    {
        ReconcileExpandedShot();
        ReconcileExpandedDuel();
        if (expandedHostedEvent is null && ActiveAction is { NativeHeader: null } active && (!CanRetainBattleActionActor(byId[active.ActorId], active.NativePlayerReaction, active.NativeAiCallbackStage, active.TargetRequest) || active.Phase == BattleActionPhase.Windup && active.MagicCasts.IsDefaultOrEmpty && !CanRetainTargets(active.ActorId, active.TargetId, active.Ability, active.ActionId, active.NativeAction, active.TargetRequest, active.NativeGraphic, active.NativeLimitCallback)))
        {
            if (!UsesExpandedRoster)
            {
                ObserveNativeUnknownStackCallback(0x485460, "active action cancellation");
            }
            RefundCancelledNativeItem(active);
            CompleteExpandedHostedActionPause();
            if (!UsesExpandedRoster)
            {
                QueueNativeActionClockCompletion(0x47DDA0);
            }
            byId[active.ActorId].IsBusy = false;
            ActiveAction = null;
            Log("Cancelled", active.ActorId, active.TargetId, 0, "Defeated actor cannot complete an action.");
        }
        BattleAttackCommand[] pending = actions.ToArray();
        actions.Clear();
        foreach (BattleAttackCommand command in pending)
        {
            if (command.NativeCallback is not null || command.NativeHeader is not null || command.ExpandedAiDescriptor is not null)
            {
                actions.Enqueue(command);
                continue;
            }
            if (command.ExpandedAiCallback is { } callback)
            {
                if (CanDispatchExpandedAiCallback(ValidateExpandedAiCallback(command, callback), callback))
                {
                    actions.Enqueue(command);
                }
                continue;
            }
            BattleCombatant actor = byId[command.ActorId];
            if (CanRetainBattleActionActor(actor, command.NativePlayerReaction, command.NativeAiCallbackStage, command.TargetRequest) && (!command.MagicCasts.IsDefaultOrEmpty || command.NativeRenzokuken is not null || UsesExpandedRoster && (command.NativeDuel is not null || command.SmartAttack is not null || RequiresExpandedCompelledReplacement(actor, command)) || CanRetainTargets(command.ActorId, command.TargetId, command.Ability, command.ActionId, command.NativeAction, command.TargetRequest, command.NativeGraphic, command.NativeLimitCallback)))
            {
                actions.Enqueue(command);
            }
            else
            {
                RefundCancelledNativeItem(command);
                byId[command.ActorId].IsBusy = false;
                Log("Cancelled", command.ActorId, command.TargetId, 0, "Defeated actor cannot act.");
            }
        }
        BattleActionPresentation? busyAction = expandedHostedEvent is { LogicallyComplete: true } completed &&
            ReferenceEquals(ActiveAction, completed.ActivePresentation) ? null : ActiveAction;
        foreach (BattleCombatant actor in combatants)
        {
            bool nativeQueued = !UsesExpandedRoster && actor.EncodeNativeSlot() is >= 0 and < 7 && nativeCommandBanks.CountActor(checked((byte)actor.EncodeNativeSlot())) != 0;
            bool retainedSupport = ActiveAction is { } support && support.ActorId == actor.Id && IsNativeSupportInvocation(support.NativePlayerReaction) || actions.Any(command => command.ActorId == actor.Id && IsNativeSupportInvocation(command.NativePlayerReaction));
            bool retainedDeathAction = UsesExpandedRoster && (HasExpandedEnemyDeathAction(actor) ||
                ActiveAction?.ExpandedAiCallback is { Stage: 3 } callback && callback.Actor == TargetActorReference(actor) ||
                actions.Any(command => command.ExpandedAiCallback is { Stage: 3 } request && request.Actor == TargetActorReference(actor)));
            bool enteringForm = HasExpandedHostedGuardianFormEntry && GetGuardianForm(actor.Id) is { Phase: BattleGuardianFormPhase.Entering };
            actor.IsBusy = (NativeGraphics is not null || actor.IsAlive || retainedSupport || retainedDeathAction) && (nativeQueued || nativeTimedAction?.Command.ActorId == actor.Id || UsesExpandedRoster && (nativeShotProcesses.ContainsKey(actor.Id) || nativeDuelProcesses.ContainsKey(actor.Id)) || guardianForceCasts.ContainsKey(actor.Id) || enteringForm || busyAction?.ActorId == actor.Id || actions.Any(command => command.NativeCallback is null && command.ExpandedAiDescriptor is null && command.ActorId == actor.Id));
        }

        bool CanRetainTargets(string actorId, string targetId, BattleAbility ability, int actionId, BattleNativeAction? native, BattleTargetRequest request, BattleNativeGraphicCommand? graphic, NativeLimitCallback? callback)
        {
            if (UsesExpandedRoster && IsShotCallback(callback))
            {
                return nativeShotProcesses.ContainsKey(actorId);
            }
            if (UsesExpandedRoster && IsDuelCallback(callback))
            {
                return nativeDuelProcesses.ContainsKey(actorId);
            }
            if (UsesExpandedRoster)
            {
                return graphic is not null || ExpandedActionTargets(GetActor(actorId), targetId, ability, actionId, request, native).Length != 0;
            }
            return ability == BattleAbility.Draw && native is null ? GetActor(targetId).IsAlive : ActionHasValidTarget(request, graphic);
        }
    }

    private BattleOutcome DetermineOutcome()
    {
        if (nativeEscape.Completed)
        {
            return BattleOutcome.Escaped;
        }
        if (DebugOutcome is BattleOutcome requested)
        {
            return requested;
        }
        if (HasPendingNativeOutcome)
        {
            BattleOutcome pending = PendingNativeOutcome;
            return Outcome == pending && NativeGraphics!.NativeOutcome!.FadeDurationB0C == 0 ? pending : BattleOutcome.Active;
        }
        if (!NativeEntryReady || NativeScriptAllocationPending || nativeTimedAction is not null || ActiveAction?.NativeHeader is not null || IsNativeSupportInvocation(ActiveAction?.NativePlayerReaction) || IsNativeReturnDamageAction(ActiveAction?.NativeAction) || QueuedActions.Any(command => command.NativeHeader is not null || IsNativeReturnDamageAction(command.NativeAction) || IsNativeSupportInvocation(command.NativePlayerReaction) || IsNativeSupportCallback(command.NativeCallback)) ||
            UsesExpandedRoster && (expandedHostedEvent is not null || HasExpandedAiDescriptors || actions.Any(command => command.ExpandedAiCallback is not null) || enemies.Any(actor => actor.ExpandedAiRemovalPending || HasExpandedEnemyDeathAction(actor)) || nativeShotProcesses.Count != 0 || nativeDuelProcesses.Count != 0 || IsShotCallback(ActiveAction?.NativeLimitCallback) || actions.Any(command => IsShotCallback(command.NativeLimitCallback))))
        {
            return BattleOutcome.Active;
        }
        if (nativeScriptState.ScriptedExitRequested)
        {
            if (ActiveAction is not null || nativeActionCommitDepth != 0 || flushingNativeReactions)
            {
                return BattleOutcome.Active;
            }
            return BattleOutcome.ScriptedExit;
        }
        if (NativeGraphics is not null)
        {
            if (encounterPolicies?.TerminalReached == true)
            {
                return BattleOutcome.Victory;
            }
            if (!allies.Any(actor => actor.Slot is { Position: < NativeBattleSlots.AllyCount } && (actor.NativeCombatantFlags & 1) != 0 && (NativeSavedStatusBits(actor) & 5) == 0))
            {
                return BattleOutcome.Defeat;
            }
            return enemies.Any(actor => actor.Slot is { Position: < NativeBattleSlots.EnemyCount } && (NativeSavedStatusBits(actor) & 5) == 0) ? BattleOutcome.Active : BattleOutcome.Victory;
        }
        if (!allies.Any(IsNativeBattleCapable))
        {
            return BattleOutcome.Defeat;
        }
        if (encounterPolicies?.TerminalReached == true)
        {
            return BattleOutcome.Victory;
        }
        return enemies.Any(actor => IsNativeBattleCapable(actor) && (UsesExpandedRoster || !actor.Untargetable)) ? BattleOutcome.Active : BattleOutcome.Victory;
    }

    private void CompleteBattleIfNeeded()
    {
        if (HasPendingExpandedTerminal || deferredExpandedTerminalOutcome is not null)
        {
            return;
        }
        if (UsesExpandedRoster)
        {
            CompleteExpandedAiRemovals();
        }
        CleanupDefeatedEncounterAllocations();
        CleanupEncounterPoliciesAtTerminal();
        BattleOutcome outcome = DetermineOutcome();
        if (outcome == BattleOutcome.Active)
        {
            return;
        }
        // A latched diagnostic decision must not repeat the natural Phoenix admission or its RNG.
        if (DiagnosticTerminalHold is { Phase: BattleDiagnosticTerminalHoldPhase.Held } held && outcome == held.Declaration.Candidate && DeferDiagnosticTerminalOutcome(outcome))
        {
            return;
        }
        if (outcome == BattleOutcome.Victory && DeferDiagnosticTerminalOutcome(outcome))
        {
            return;
        }
        if (UsesExpandedRoster && DebugOutcome is null && ActiveAction is not null)
        {
            return;
        }
        StopTurnAdvance();
        if (DebugOutcome is null && outcome == BattleOutcome.Victory && NativeGraphics is { } graphics && graphics.NativeOutcome?.ResultByte6E7 != 4)
        {
            BeginNativeVictory486500();
            return;
        }
        if (DebugOutcome is null && outcome == BattleOutcome.Defeat && NativeGraphics?.NativeOutcome?.ResultByte6E7 != 1)
        {
            if (nativeActionCommitDepth != 0 || flushingNativeReactions)
            {
                return;
            }
            bool recoveryAlreadyConsidered = DiagnosticTerminalHold is { } recoveryHold && recoveryHold.Sequence > recoveryHold.Rearms && (recoveryHold.Phase is BattleDiagnosticTerminalHoldPhase.Held or BattleDiagnosticTerminalHoldPhase.Released or BattleDiagnosticTerminalHoldPhase.Expired);
            if (!recoveryAlreadyConsidered && nativeSupport is { EntryInitialized: true } && TryQueueNativePhoenixRecovery())
            {
                return;
            }
            if (DeferDiagnosticTerminalOutcome(BattleOutcome.Defeat))
            {
                return;
            }
            if (NativeGraphics is not null)
            {
                if (TryRunEncounterNativeDefeatTerminal())
                {
                    return;
                }
                BeginNativeDefeat486450();
                return;
            }
        }
        if (DebugOutcome is null && outcome == BattleOutcome.ScriptedExit && NativeGraphics is not null && NativeGraphics.NativeOutcome?.ResultByte6E7 != 1)
        {
            BeginNativeScriptedExit4863F0();
            return;
        }
        if (DebugOutcome is null && OwnsExpandedTerminal)
        {
            AdmitExpandedHostedTerminal(outcome);
            return;
        }
        FinalizeBattleOutcome(outcome);
    }

    private void FinalizeBattleOutcome(BattleOutcome outcome)
    {
        expandedInvocationProgress = null;
        if (guardianForms is not null)
        {
            foreach (string actorId in guardianForms.Keys.ToArray())
            {
                BattleCombatant actor = GetActor(actorId);
                ReturnGuardianForm(actor, (NativeSavedStatusBits(actor) & 1) != 0 || (NativeBattleStatusBits(actor) & 0x10000) != 0);
            }
        }
        Outcome = outcome;
        CompleteExpandedHostedActionPause();
        if (OwnsExpandedBattleClock && nativeBattleClock is not null)
        {
            SetNativeBattleClockActive(false);
        }
        foreach (BattleAttackCommand queued in actions)
        {
            RefundCancelledNativeItem(queued);
        }
        if (ActiveAction is { } pending)
        {
            RefundCancelledNativeItem(pending);
        }
        actions.Clear();
        ActiveAction = null;
        ClearNativeQueuedCommands();
        if (guardianForms is not null)
        {
            foreach (string actorId in guardianForms.Keys.ToArray())
            {
                BattleCombatant actor = GetActor(actorId);
                ReturnGuardianForm(actor, (NativeSavedStatusBits(actor) & 1) != 0 || (NativeBattleStatusBits(actor) & 0x10000) != 0);
            }
        }
        nativeGuardianBoost = null;
        guardianForceCasts.Clear();
        CompleteNativeGraphicsAction();
        foreach (BattleCombatant actor in combatants)
        {
            actor.IsBusy = false;
            actor.ExpandedAiRemovalPending = false;
            actor.NativeStatus1 &= 0x7FFFFFFF;
        }
        Log("Outcome", null, null, 0, outcome.ToString());
    }

    private BattleCombatant GetActor(string id)
    {
        if (id is null || !byId.TryGetValue(id, out BattleCombatant? actor))
        {
            throw new ArgumentException("Unknown battle combatant.", nameof(id));
        }
        return actor;
    }

    private void Log(string kind, string? actorId, string? targetId, int amount, string message, BattleActionPresentation? actionContext = null, bool deferredImpact = false)
    {
        BattleActionPresentation? presented = actionContext ?? resolvingNativeAction ?? ActiveAction;
        BattleAbility ability = presented is not null && presented.ActorId == actorId ? presented.Ability : BattleAbility.Attack;
        events.Add(new BattleEvent(Frame, kind, actorId, targetId, amount, message, ability) { NativeSequence = presented is not null && presented.ActorId == actorId ? presented.NativeSequence : null, NativeGraphicMagicId = presented?.NativeGraphic?.MagicId, DeferredImpact = deferredImpact || deferringExpandedImpacts && (kind is "Damage" or "Healing") });
    }

    private static void ValidateMode(BattleAtbMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private static int CalculateAtbGain(int speed, int ratePercent)
    {
        return Math.Max(1, checked(speed * ratePercent / 100));
    }

    public static BattleDefinition CloneDefinition(BattleDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.Combatants);
        return source with { MagicVitals = source.MagicVitals?.DeepCopy(), NativeScanNames = source.NativeScanNames.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()), NativeScanActorNames = source.NativeScanActorNames.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()), NativeCards = source.NativeCards?.DeepCopy(), NativeDevourEffects = source.NativeDevourEffects.ToArray(), NativeCardNames = source.NativeCardNames.ToArray(), NativeItemNames = source.NativeItemNames.ToArray(), NativeGlobalMessages = source.NativeGlobalMessages.ToArray(), NativeCommandNames = source.NativeCommandNames.ToArray(), NativeExamTimerAtEntry = source.NativeExamTimerAtEntry is { } timer ? timer with { Alternatives = timer.Alternatives.ToArray() } : null, PersistentAiVariables = source.PersistentAiVariables?.ToArray(), Combatants = source.Combatants.Select(actor => actor with { NativeStatusResistances = actor.NativeStatusResistances.ToArray(), GuardianForceCompatibility = actor.GuardianForceCompatibility?.ToArray(), EquipmentIds = actor.EquipmentIds.ToArray(), Abilities = actor.Abilities.ToArray(), StatusEffects = actor.StatusEffects.ToArray(), MagicStocks = actor.MagicStocks.ToArray(), NativeMagicInventory = actor.NativeMagicInventory?.ToArray(), NativeDrawSlots = actor.NativeDrawSlots?.ToArray(), DrawSpells = actor.DrawSpells.ToArray(), DrawGuardianForces = actor.DrawGuardianForces.ToArray(), ElementDefenses = actor.ElementDefenses.ToArray(), NativeAi = CloneNativeAi(actor.NativeAi), NativeTemplate = CloneNativeTemplate(actor.NativeTemplate) }).ToArray(), GuardianForces = source.GuardianForces.ToArray(), Items = source.Items.ToArray(), NativeItemInventory = source.NativeItemInventory?.ToArray(), NativeActions = source.NativeActions.ToArray(), ActionDefinitions = source.ActionDefinitions.Select(action => action with { Statuses = action.Statuses?.ToArray() }).ToArray() };
    }

    private void ValidateDefinition(BattleDefinition source)
    {
        int enemyCount = source.Combatants.Count(actor => !actor.IsPlayer);
        int addedCount = source.AddedEnemySources?.Count ?? 0;
        int enemySlotCount = source.UsesExpandedRoster && !source.UsesOriginalEnemySlots ? enemyCount : NativeBattleSlots.FormationSlotCount + addedCount;
        int activeEnemyCount = source.Combatants.Count(actor => !actor.IsPlayer && actor.InitialActive);
        if (source.UsesExpandedRoster && (source.NativeGraphics is not null || !ExpandedBattleFormation.SupportsRoster(source.Combatants.Length - enemyCount, activeEnemyCount) || source.Combatants.Any(actor => !actor.InitialActive && (actor.IsPlayer || !source.UsesOriginalEnemySlots))))
        {
            throw new NotSupportedException("The expanded battle host requires a supported roster and host-owned actions.");
        }
        if (source.UsesOriginalEnemySlots)
        {
            BattleCombatantDefinition[] enemyDefinitions = source.Combatants.Where(actor => !actor.IsPlayer).ToArray();
            BattleCombatantDefinition[] originalEnemies = enemyDefinitions.Where(actor => !IsAddedEnemy(actor)).ToArray();
            int activeOriginalEnemies = originalEnemies.Count(actor => actor.InitialActive);
            if (!source.UsesExpandedRoster || activeOriginalEnemies is < 1 or > NativeBattleSlots.EnemyCount ||
                originalEnemies.Any(actor => actor.SceneSlot is null or < 0 or >= NativeBattleSlots.FormationSlotCount || actor.NativeTemplate is null || actor.RewardSlot >= NativeBattleSlots.FormationSlotCount) ||
                originalEnemies.Select(actor => actor.SceneSlot).Distinct().Count() != originalEnemies.Length)
            {
                throw new InvalidDataException("Original enemy slots require unique formation prototypes, native templates and one to four initial enemy allocations.");
            }
            BattleCombatantDefinition[] addedEnemies = enemyDefinitions.Where(IsAddedEnemy).ToArray();
            if (addedEnemies.Length != addedCount || addedEnemies.Select(actor => actor.SceneSlot).Distinct().Count() != addedCount ||
                addedEnemies.Any(actor => !actor.InitialActive || actor.NativeTemplate is not null || actor.Rewards is not null && actor.RewardSlot != actor.SceneSlot))
            {
                throw new InvalidDataException("Mixed battle additions require a unique active logical enemy for each added placement.");
            }
        }
        if (source.AddedEnemySources is { } added)
        {
            int authoredCount = enemyCount - added.Count;
            int firstAddedSlot = source.UsesOriginalEnemySlots ? NativeBattleSlots.FormationSlotCount : authoredCount;
            if (!source.UsesExpandedRoster || added.Count > ExpandedBattleFormation.MaximumAddedEnemies ||
                !added.Keys.SequenceEqual(Enumerable.Range(firstAddedSlot, added.Count)) ||
                !source.UsesOriginalEnemySlots && (authoredCount is < 1 or > NativeBattleSlots.EnemyCount ||
                    !source.Combatants.Where(actor => !actor.IsPlayer).Select(actor => actor.RewardSlot).SequenceEqual(Enumerable.Range(0, enemyCount).Select(slot => (int?)slot))))
            {
                throw new InvalidDataException("Added enemy provenance does not match the ordered battle roster.");
            }
            foreach (BattleEnemyRosterSource sourcePlacement in added.Values)
            {
                (sourcePlacement ?? throw new InvalidDataException("Added enemy provenance requires its source formation placement.")).Validate();
            }
        }
        if (source.ExpandedEnemyLevels is { } levelProfiles)
        {
            if (!source.UsesExpandedRoster || !levelProfiles.Keys.ToHashSet().SetEquals(source.Combatants.Where(actor => !actor.IsPlayer).Select(actor => actor.ModelId)))
            {
                throw new InvalidDataException("Expanded level projections require an installed profile for every enemy model.");
            }
            foreach (ExpandedEnemyLevelProfile profile in levelProfiles.Values)
            {
                if (profile is null || profile.Levels.IsDefault || profile.Levels.Length != 100 || profile.DrawBands.IsDefault || profile.DrawBands.Length != 3 ||
                    profile.Levels.Any(level => level is null || level.MaximumHp <= 0 || level.BaseStatistics.IsDefault || level.BaseStatistics.Length != 6 || level.AiLevelBand is < 0 or > 2))
                {
                    throw new InvalidDataException("Expanded level projections require all 100 original levels and three Draw banks.");
                }
                foreach (var band in profile.DrawBands)
                {
                    if (band.IsDefault)
                    {
                        throw new InvalidDataException("Expanded level projections lack an original Draw bank.");
                    }
                    BattleCombatant.ValidateDrawSlots(band.ToArray());
                }
            }
        }
        source.Tuning.Validate();
        source.MagicVitals?.Validate();
        source.MagicVitals?.ValidateMagicSystemConsistency();
        if (source.Combatants.Any(actor => actor.MagicRecordId is int row && ((uint)row >= (source.MagicVitals?.Records.Length ?? NativeCharacterVitals.NativeRecordCount) || !actor.IsPlayer)) || source.Combatants.Where(actor => actor.IsPlayer && actor.MagicRecordId is not null).Select(actor => actor.MagicRecordId).Distinct().Count() != source.Combatants.Count(actor => actor.IsPlayer && actor.MagicRecordId is not null))
        {
            throw new InvalidDataException("Battle magic banks require distinct stable physical player record identities.");
        }
        source.NativeGlobalSoundProcess?.Validate();
        source.NativeMusicProcess?.Validate();
        source.NativeScannedModels?.Validate();
        source.NativeKnownMagic?.Validate();
        source.NativeExamTimerAtEntry?.Validate();
        source.NativePocketStation?.Validate();
        if (source.NativeEffectCatalog.IsDefault || !source.NativeEffectCatalog.IsEmpty && (source.NativeEffectCatalog.Length != 400 || source.NativeEffectCatalog.Select(binding => binding?.Effect).Order().Where((effect, index) => effect != index + 1).Any()))
        {
            throw new InvalidDataException("Native effect catalog requires all400 unique original effect IDs when present.");
        }
        if (source.PersistentAiVariables is { Length: not 8 })
        {
            throw new InvalidDataException("Native persistent battle AI bank requires all eight executed DWORDs.");
        }
        int maximumCombatants = source.UsesExpandedRoster ? ExpandedBattleFormation.MaximumAllies + (source.UsesOriginalEnemySlots ? ExpandedBattleFormation.MaximumRewardSlots : ExpandedBattleFormation.MaximumEnemies) : 16;
        if (string.IsNullOrWhiteSpace(source.Name) || source.StageId < 0 || source.EncounterId < 0 || source.Combatants.Length < 2 || source.Combatants.Length > maximumCombatants || !source.Combatants.Any(actor => actor.IsPlayer) || !source.Combatants.Any(actor => !actor.IsPlayer))
        {
            throw new ArgumentException($"Battle requires a name and both player and enemy combatants (2–{maximumCombatants} total).");
        }
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<int> rewardSlots = [];
        foreach (BattleCombatantDefinition actor in source.Combatants)
        {
            ValidateNativeAi(actor, enemySlotCount);
            if (actor.ExpandedEnemyBaseStatistics is { } baseStatistics && (!source.UsesExpandedRoster || actor.IsPlayer || baseStatistics.Length != 6))
            {
                throw new InvalidDataException("Expanded enemy statistics require exactly six original values on an expanded enemy.");
            }
            if (actor.Luck is < 0 or > 255 || actor.Hit is < 0 or > 255 || actor.Evasion is < 0 or > 255)
            {
                throw new ArgumentException("Native hit, luck and evasion statistics require their BYTE widths.");
            }
            bool invalidName = actor.Name is null || (actor.IsPlayer || actor.NativeTemplate is null) && string.IsNullOrWhiteSpace(actor.Name);
            bool invalidModel = actor.ModelId < -1 || actor.IsPlayer && actor.ModelId < 0 || actor.ModelId == -1 && (source.EncounterId is not null || actor.WeaponId is not null || actor.Costume != 0);
            // Both native enemy projections carry the original HP DWORD.
            bool hasNativeHpProjection = actor.NativeTemplate is not null || actor.ExpandedEnemyBaseStatistics.HasValue;
            if (string.IsNullOrWhiteSpace(actor.Id) || !ids.Add(actor.Id) || invalidName || invalidModel || actor.Costume < 0 || actor.MaximumHp < 0 || !hasNativeHpProjection && actor.MaximumHp > 1_000_000 || actor.MaximumHp == 0 && (actor.InitialActive || actor.NativeTemplate is null) || actor.CurrentHp < 0 || actor.CurrentHp > actor.MaximumHp || actor.Speed is < 1 or > AtbMaximum || actor.Attack is < 0 or > 1_000_000 || actor.Defense is < 0 or > 1_000_000 || actor.InitialAtb is < 0 or > AtbMaximum || actor.WeaponId < 0 || actor.EquipmentIds.Any(id => id < 0) || actor.Level is < 0 or > 100 || actor.Magic is < 0 or > 255 || actor.Spirit is < 0 or > 255 || actor.PhysicalPower is < 0 or > 255 || actor.ElementAttack is < 0 or > 255 || actor.ElementAttackPercent is < 0 or > 255 || actor.DrainPercent is < 0 or > 255 || actor.ElementDefenses.Length != 8 || actor.ElementDefenses.Any(value => value is < 0 or > 2550) || actor.DrawSpells.Any(spell => spell.Id is < 1 or >= 64 || spell.Resistance is < 0 or > 255) || actor.DrawSpells.Select(spell => spell.Id).Distinct().Count() != actor.DrawSpells.Length)
            {
                throw new ArgumentException($"Battle combatant '{actor.Id}' has invalid identity, vitals, stats or equipment.");
            }
            if (actor.Abilities.Length == 0 || actor.Abilities.Any(ability => !Enum.IsDefined(ability)) || actor.Abilities.Distinct().Count() != actor.Abilities.Length)
            {
                throw new NotSupportedException("Battle command list contains an unsupported or duplicate command.");
            }
            ValidateStocks(actor.MagicStocks, 32);
            if (actor.NativeMagicInventory is { } nativeMagic)
            {
                BattleCombatant.ValidateMagicInventory(nativeMagic);
            }
            if (actor.Rewards is { } rewards)
            {
                rewards.Validate();
                if (actor.IsPlayer || actor.RewardSlot is not int slot || (uint)slot >= enemySlotCount || !rewardSlots.Add(slot) || actor.Level == 0 && actor.InitialActive)
                {
                    throw new InvalidDataException("Enemy reward definitions require their unique original formation slots and actual levels.");
                }
            }
            else if (actor.RewardSlot is not null)
            {
                throw new InvalidDataException("An enemy reward slot requires its original reward definition.");
            }
            HashSet<BattleStatusKind> statusKinds = [];
            foreach (BattleStatusEffectDefinition effect in actor.StatusEffects)
            {
                if (effect.DurationFrames is < 1 or > 1_000_000 || effect.TickIntervalFrames is < 1 or > 1_000_000 || effect.Magnitude is < 0 or > 1_000_000)
                {
                    throw new ArgumentException("Invalid battle status duration, interval or magnitude.");
                }
                if (!statusKinds.Add(effect.Kind))
                {
                    throw new ArgumentException("Duplicate battle status effect.");
                }
                if (!handlers.TryGetValue(effect.Kind, out IBattleStatusEffectHandler? handler))
                {
                    throw new NotSupportedException($"Battle status {effect.Kind} has no implemented handler.");
                }
                handler.Validate(effect);
            }
        }
        ValidateStocks(source.Items, 198);
        if (source.ActionDefinitions.Any(action => !Enum.IsDefined(action.Ability) || !Enum.IsDefined(action.Effect) || action.Id < 0 || string.IsNullOrWhiteSpace(action.Name) || action.Power < 0 || action.Statuses?.Any(kind => !handlers.ContainsKey(kind)) == true) || source.ActionDefinitions.GroupBy(action => (action.Ability, action.Id)).Any(group => group.Count() != 1))
        {
            throw new InvalidDataException("Invalid or unsupported battle action catalog.");
        }
    }

    private void RestoreValidated(BattleSnapshot snapshot, HostedBattleTerminal? preparedTerminal, NativeBattleTriggerReceipt? preparedRenzokukenTrigger)
    {
        DiagnosticRestoreCount++;
        ResetDiagnosticCounterOwners();
        if (snapshot.Version != 1 || snapshot.Frame < 0 || !Enum.IsDefined(snapshot.Difficulty) || !Enum.IsDefined(snapshot.Outcome) || snapshot.Combatants is null || snapshot.Combatants.Length != combatants.Count || snapshot.QueuedActions is null || snapshot.Events is null)
        {
            throw new InvalidDataException("Invalid battle snapshot envelope.");
        }
        if (!snapshot.CustomHandlers.Order().SequenceEqual(handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).Select(handler => handler.Kind).Order()))
        {
            throw new NotSupportedException("Battle snapshot requires its explicit custom effect handlers.");
        }
        nativeScriptState = snapshot.NativeScriptState?.DeepCopy() ?? throw new InvalidDataException("Native battle script state is missing.");
        nativeActionPackets = snapshot.NativeActionPackets ?? throw new InvalidDataException("Native action packet owner is missing; explicitly migrate the standalone snapshot.");
        nativeActionPackets.Validate();
        nativeCommandBanks = snapshot.NativeCommandBanks ?? throw new InvalidDataException("Native command banks are missing; explicitly migrate the standalone snapshot.");
        nativeCommandBanks.Validate();
        nativePartyStatusMirrors = snapshot.NativePartyStatusMirrors ?? throw new InvalidDataException("Native party prior-status mirrors are missing; explicitly migrate the standalone snapshot.");
        nativePartyStatusMirrors.Validate();
        RestoreNativePartyPresentation(snapshot.NativePartyPresentation);
        NativeEffectIoResult = snapshot.NativeEffectIoResult;
        snapshot.NativeFileIo?.Validate();
        NativeFileIo = snapshot.NativeFileIo;
        snapshot.NativeVibrationProcess?.Validate();
        NativeVibrationProcess = snapshot.NativeVibrationProcess;
        snapshot.NativeGlobalSoundProcess?.Validate();
        NativeGlobalSoundProcess = snapshot.NativeGlobalSoundProcess;
        snapshot.NativeMusicProcess?.Validate();
        NativeMusicProcess = snapshot.NativeMusicProcess;
        snapshot.NativeEffectMemory?.Validate();
        NativeEffectMemory = snapshot.NativeEffectMemory;
        snapshot.NativeStage?.Validate();
        NativeStage = snapshot.NativeStage;
        snapshot.NativeModelTextureCache?.Validate();
        NativeModelTextureCache = snapshot.NativeModelTextureCache;
        snapshot.NativeModelLoader?.Validate();
        snapshot.NativeFileStaging?.Validate();
        if (snapshot.NativeModelLoader?.Task is { Phase: > 0 } && snapshot.NativeFileStaging is null)
        {
            throw new InvalidDataException("Native model load snapshot lacks its actual shared199B018 staging owner; migrate the active load explicitly.");
        }
        snapshot.NativeModelCpuResources?.Validate();
        NativeModelLoader = snapshot.NativeModelLoader;
        NativeFileStaging = snapshot.NativeFileStaging;
        NativeModelCpuResources = snapshot.NativeModelCpuResources;
        RestoreNativeActionMemory(snapshot.NativeActionMemory);
        RestoreNativePresentationStatusMemory(snapshot.NativePresentationStatusMemory);
        nativeActionScratch = snapshot.NativeActionScratch ?? throw new InvalidDataException("Native action scratch owner is missing; explicitly migrate the standalone snapshot.");
        nativeActionScratch.Validate();
        HashSet<string> restored = new(StringComparer.Ordinal);
        foreach (BattleCombatantSnapshot saved in snapshot.Combatants)
        {
            BattleCombatant actor = GetActor(saved.Id);
            RestoreNativeParticipant(actor, saved.NativeParticipant);
            actor.RestoreMagicInventory(saved.NativeMagicInventory ?? throw new InvalidDataException("Battle snapshot lacks its physical magic inventory; migrate it explicitly."));
            actor.RestoreDrawnMagic(saved.DrawnMagicIds);
            actor.RestoreDrawSlots(saved.NativeDrawSlots ?? throw new InvalidDataException("Battle snapshot lacks its physical Draw bank; migrate it explicitly."));
            actor.ReprojectNativePlayerStatistics();
            bool deferredTimedRecipient = nativeActionScratch.DeferredEffectsE00 == true && (snapshot.NativeTimedAction?.Command.TargetId == saved.Id || UsesExpandedRoster && snapshot.NativeLimits?.Any(limit => limit?.Duel is { } duel && ReadExpandedDuelTarget(duel) == saved.Id) == true);
            bool invalidHp = actor.NativeAi is null && (saved.CurrentHp < 0 || saved.CurrentHp > actor.MaximumHp || saved.CurrentHp == 0 && saved.Atb != 0 && snapshot.NativeGraphics is null && !deferredTimedRecipient);
            if (!restored.Add(saved.Id) || invalidHp || saved.Atb is < 0 or > AtbMaximum || saved.StatusEffects is null)
            {
                throw new InvalidDataException("Invalid battle combatant state.");
            }
            Dictionary<BattleStatusKind, BattleStatusState> initial = actor.Effects.ToDictionary(effect => effect.Definition.Kind);
            actor.Effects.Clear();
            HashSet<BattleStatusKind> kinds = [];
            foreach (BattleStatusSnapshot status in saved.StatusEffects)
            {
                BattleStatusEffectDefinition? effectDefinition = status.Definition ?? initial.GetValueOrDefault(status.Kind)?.Definition;
                if (effectDefinition is null || !handlers.ContainsKey(status.Kind) || effectDefinition.Kind != status.Kind || !kinds.Add(status.Kind) || status.RemainingFrames < 1 || status.RemainingFrames > effectDefinition.DurationFrames || status.UntilTickFrames < 1 || status.UntilTickFrames > effectDefinition.TickIntervalFrames)
                {
                    throw new InvalidDataException("Invalid battle status timer.");
                }
                int elapsedFrames = effectDefinition.DurationFrames - status.RemainingFrames;
                int expectedUntilTick = effectDefinition.TickIntervalFrames - elapsedFrames % effectDefinition.TickIntervalFrames;
                if (!effectDefinition.Permanent && status.UntilTickFrames != expectedUntilTick)
                {
                    throw new InvalidDataException("Battle status duration and periodic timer disagree.");
                }
                handlers[status.Kind].Validate(effectDefinition);
                actor.Effects.Add(new BattleStatusState(effectDefinition, status.RemainingFrames, status.UntilTickFrames));
            }
            if (saved.GuardianForceCompatibility is { Length: not 16 })
            {
                throw new InvalidDataException("Saved GF compatibility requires sixteen native WORDs.");
            }
            actor.MutableGuardianForceCompatibility = saved.GuardianForceCompatibility?.ToArray() ?? actor.MutableGuardianForceCompatibility;
            actor.CurrentHp = saved.CurrentHp;
            if (!UsesExpandedRoster && snapshot.NativePresentationStatusMemory is not null && HasNativeRosterSlot(actor) && saved.NativeStatus?.Presentation != actor.NativePresentationStatus)
            {
                throw new InvalidDataException("Native actor B92/B1C projection contradicts its physical presentation-status bank.");
            }
            RestoreNativeStatus(actor, saved.NativeStatus);
            RestoreNativeAtb(actor, saved.NativeAtb, saved.Atb);
            if (saved.LimitTurnCrisis is > 4 || saved.LimitTurnCrisis is not null && (!definition.LimitOncePerTurn || !actor.IsPlayer || actor.NativeLimit is null))
            {
                throw new InvalidDataException("Saved turn Limit result has invalid character ownership or crisis level.");
            }
            actor.LimitTurnCrisis = saved.LimitTurnCrisis;
            ValidateStocks(saved.MagicStocks, 32);
        }
        if (UsesOriginalEnemySlots)
        {
            ValidateExpandedOriginalEnemySlots();
        }
        RestoreNativeElementAttack(snapshot.NativeElementAttack);
        (snapshot.NativeScriptState ?? throw new InvalidDataException("Native battle script state is missing.")).Validate();
        RestoreGuardianForces(snapshot);
        RestoreGuardianForms(snapshot.GuardianForms, snapshot.Outcome);
        RestoreNativeSupport(snapshot.NativeSupport);
        RestoreExpandedEnemyDraw(snapshot.PendingEnemyDraw);
        RestoreNativeBattleClock(snapshot.NativeClock);
        RestoreNativeGraphics(snapshot.NativeGraphics);
        ValidateNativeSupportEntryOwner();
        HashSet<string> busy = new(StringComparer.Ordinal);
        if (combatants.Where(actor => actor.IsActive).GroupBy(actor => actor.Slot).Any(group => group.Count() != 1))
        {
            throw new InvalidDataException("Several active battle allocations occupy the same physical slot.");
        }
        ValidateNativeCommandBindings(busy);
        foreach (BattleAttackCommand command in snapshot.QueuedActions)
        {
            if (command.ExpandedAiCallback is { } callback)
            {
                BattleCombatant owner = ValidateExpandedAiCallback(command, callback);
                if (!CanDispatchExpandedAiCallback(owner, callback))
                {
                    throw new InvalidDataException("Queued expanded AI callback has no current eligible actor.");
                }
                busy.Add(owner.Id);
                actions.Enqueue(command);
                continue;
            }
            if (command.ExpandedAiDescriptor is not null)
            {
                ValidateExpandedAiDescriptor(command);
                actions.Enqueue(command);
                continue;
            }
            ValidateGuardianFormCommand(command);
            ValidateNativeEventOwner(command.ActorId, command.NativeHeader, command.NativeEventOwner, command.NativeAiCallbackStage, command.NativeAction);
            if (command.NativeCallback is not null)
            {
                ValidateSavedNativeCallback(command);
                actions.Enqueue(command);
                continue;
            }
            ValidateNativeAiCommandStage(GetActor(command.ActorId), command.Ability, command.NativeAiCallbackStage);
            ValidateExpandedMagicTargets(command.ActorId, command.TargetId, command.Ability, command.ActionId, command.MagicCasts, command.NativeMagicCastMode);
            ValidateNativeDrawPacket(command.Ability, command.ActionId, command.DrawCast, command.DrawCastTargetId, command.NativeDraw, false, command.NativeAction, command.TargetRequest);
            if (command.NativeHeader is null && command.IndependentGuardian is not null)
            {
                throw new InvalidDataException("Queued GF item cannot already own a dispatched invocation.");
            }
            if (UsesExpandedRoster && command.NativeSpecial is not null)
            {
                throw new InvalidDataException("Queued expanded commands prepare their special action at impact.");
            }
            ValidateSavedAction(command.ActorId, command.TargetId, busy, command.Ability, command.ActionId, !command.MagicCasts.IsDefaultOrEmpty || command.NativeHeader is not null || UsesExpandedRoster && command.SmartAttack is not null, command.NativeAction, command.TargetRequest, command.NativeGraphic, command.NativeRenzokuken, command.NativePlayerReaction, command.NativeHeader, duel: command.NativeDuel, slots: command.NativeSlots, nativeQueuedCommand: command.NativeQueuedCommand, aiCallbackStage: command.NativeAiCallbackStage, limitCallback: command.NativeLimitCallback);
            ValidateNativeActionInventory(byId[command.ActorId], command.Ability, command.NativeAction, command.ReservedNativeItemSlot, command.FreeNativeMagic);
            actions.Enqueue(command);
        }
        if (snapshot.ActiveAction is { ExpandedAiCallback: { } activeCallback } callbackAction)
        {
            busy.Add(ValidateExpandedAiCallbackPresentation(callbackAction, activeCallback).Id);
        }
        else if (snapshot.ActiveAction is { } action)
        {
            ValidateGuardianFormPresentation(action);
            ValidateNativeEventOwner(action.ActorId, action.NativeHeader, action.NativeEventOwner, action.NativeAiCallbackStage, action.NativeAction);
            ValidateNativeAiCommandStage(GetActor(action.ActorId), action.Ability, action.NativeAiCallbackStage);
            ValidateExpandedMagicTargets(action.ActorId, action.TargetId, action.Ability, action.ActionId, action.MagicCasts, action.NativeMagicCastMode);
            ValidateNativeDrawPacket(action.Ability, action.ActionId, action.DrawCast, action.DrawCastTargetId, action.NativeDraw, !UsesExpandedRoster || action.Phase == BattleActionPhase.Recovery, action.NativeAction, action.TargetRequest);
            ValidateNativeActionInventory(byId[action.ActorId], action.Ability, action.NativeAction, action.ReservedNativeItemSlot, action.FreeNativeMagic);
            int maximum = action.Phase == BattleActionPhase.Windup ? NativeActionWindupFrames(byId[action.ActorId], action.NativeSequence) : AttackRecoveryFrames;
            if (!Enum.IsDefined(action.Phase) || action.RemainingFrames < 1 || action.RemainingFrames > maximum)
            {
                throw new InvalidDataException("Invalid battle presentation timer.");
            }
            ValidateSavedAction(action.ActorId, action.TargetId, busy, action.Ability, action.ActionId, !action.MagicCasts.IsDefaultOrEmpty || action.NativeHeader is not null || action.Phase == BattleActionPhase.Recovery, action.NativeAction, action.TargetRequest, action.NativeGraphic, action.NativeRenzokuken, reaction: action.NativePlayerReaction, nativeHeaderIndex: action.NativeHeader, duel: action.NativeDuel, slots: action.NativeSlots, nativeQueuedCommand: action.NativeQueuedCommand, aiCallbackStage: action.NativeAiCallbackStage, limitCallback: action.NativeLimitCallback);
            ValidateIndependentGuardianInvocation(byId[action.ActorId], action);
            ValidateNativeSequenceProgress(byId[action.ActorId], action);
            if (UsesExpandedRoster ? action.NativeSpecial is not null : action.NativeAction?.AttackType is 17 or 19 && action.NativeSpecial is null)
            {
                throw new InvalidDataException("Saved Card/Devour presentation disagrees with its host's invocation timing.");
            }
            if (action.GunbladeTriggerTiming is short timing && (timing != 1 && timing is not (>= 20 and <= 32) || !IsGunbladeAction(byId[action.ActorId], action)))
            {
                throw new InvalidDataException("Invalid saved gunblade trigger input.");
            }
        }
        nativeReactionOrder = snapshot.NativeReactionOrder;
        RestoreNativeTimedAction(snapshot.NativeTimedAction, expandedHostedEvent);
        ActiveAction = snapshot.ActiveAction;
        ValidateExpandedAiRemovals();
        nativeEscape = snapshot.Escape;
        nativeEscape.Validate();
        if (HasPendingExpandedEscape && (!UsesExpandedRoster || snapshot.Outcome != BattleOutcome.Active || ActiveAction is not null))
        {
            throw new InvalidDataException("Expanded escape countdown requires an active roster with its actions drained.");
        }
        RestoreDebugOutcome(snapshot);
        Outcome = snapshot.Outcome;
        RestoreDiagnosticTerminalHold(snapshot);
        BattleOutcome expectedOutcome = DetermineOutcome();
        // Native graphics can release the last action after this host frame's outcome admission pass.
        bool awaitingNativeOutcomeAdmission = DebugOutcome is null && snapshot.Outcome == BattleOutcome.Active && NativeGraphics is not null && NativeGraphics.NativeOutcome?.ResultByte6E7 is null or 0 && expectedOutcome is BattleOutcome.Victory or BattleOutcome.Defeat;
        bool awaitingDiagnosticRearm = DiagnosticTerminalHold is { Phase: BattleDiagnosticTerminalHoldPhase.Held } heldOutcome && snapshot.Outcome == BattleOutcome.Active && expectedOutcome == heldOutcome.Declaration.Candidate;
        bool awaitingExpandedShotEnding = UsesExpandedRoster && snapshot.Outcome == BattleOutcome.Active && expectedOutcome == BattleOutcome.Victory && snapshot.NativeLimits?.Any(limit => limit?.Shot is { Phase: BattleNativeShotPhase.Ready } shot && ReadExpandedShotTarget(shot) is not null) == true;
        bool awaitingExpandedDuelEnding = UsesExpandedRoster && snapshot.Outcome == BattleOutcome.Active && expectedOutcome == BattleOutcome.Victory && snapshot.NativeLimits?.Any(limit => limit?.Duel is { } duel && ReadExpandedDuelTarget(duel) is not null) == true;
        // The privately validated Advance Turn candidate can reach a terminal roster before the hosted graphics run.
        bool awaitingPreparedHostedTerminal = preparedTerminal is { } terminal && UsesExpandedRoster && NativeGraphics is null && snapshot.Outcome == BattleOutcome.Active && expectedOutcome == terminal.Outcome;
        if ((!awaitingNativeOutcomeAdmission && !awaitingDiagnosticRearm && !awaitingExpandedShotEnding && !awaitingExpandedDuelEnding && !awaitingPreparedHostedTerminal && snapshot.Outcome != expectedOutcome) || (snapshot.Outcome != BattleOutcome.Active && busy.Count != 0))
        {
            throw new InvalidDataException("Battle outcome contradicts living actors or outstanding actions.");
        }
        long previousFrame = 0;
        foreach (BattleEvent item in snapshot.Events)
        {
            item.NativeSoundGain?.Validate();
            item.NativeFileAudio?.Validate();
            if (item.NativeModuleSound is { Allocation: not null } sound)
            {
                _ = sound.ReadCommittedVoice();
            }
            if (item.Frame < previousFrame || item.Frame > snapshot.Frame || string.IsNullOrWhiteSpace(item.Kind) || item.Message is null || item.Amount < 0 || (item.ActorId is not null && !byId.ContainsKey(item.ActorId)) || (item.TargetId is not null && !byId.ContainsKey(item.TargetId)))
            {
                throw new InvalidDataException("Invalid battle event log.");
            }
            previousFrame = item.Frame;
        }
        foreach (string id in busy)
        {
            byId[id].IsBusy = true;
        }
        Frame = snapshot.Frame;
        NativeHostCallbacks = snapshot.NativeHostCallbacks;
        NativeExamTimer = snapshot.NativeExamTimer;
        NativeExamTimer?.Validate();
        if (NativeExamTimer is not null && (definition.NativeExamTimerAtEntry is null || NativeHostCallbacks is null || NativeGraphics is null && !UsesExpandedRoster))
        {
            throw new InvalidDataException("Native timer snapshot lacks its declared graphical entry and executed host-callback owners.");
        }
        if (NativeHostCallbacks < 0)
        {
            throw new InvalidDataException("Native host callback ledger cannot be negative.");
        }
        SetDifficulty(snapshot.Difficulty);
        SetGunbladeMode(snapshot.GunbladeMode);
        Outcome = snapshot.Outcome;
        events.AddRange(snapshot.Events);
        RestoreOriginalStockDrawOperations(snapshot.OriginalStockDrawOperations);
        ValidateStocks(snapshot.Items, 198);
        RestoreNativeItems(snapshot.NativeItemInventory ?? throw new InvalidDataException("Battle snapshot requires its explicitly migrated198-slot native item inventory."));
        RestoreNativeGil(snapshot.NativeGil);
        RestoreEnemyDefeats(snapshot.EnemyDefeats);
        if ((snapshot.PersistentAiVariables is null) != (definition.PersistentAiVariables is null) || snapshot.PersistentAiVariables is { Length: not 8 })
        {
            throw new InvalidDataException("Battle snapshot persistent AI owner disagrees with its explicit entry bank.");
        }
        persistentAiVariables = snapshot.PersistentAiVariables?.ToArray();
        if (combatants.Any(actor => actor.NativeAi is not null))
        {
            if (snapshot.NativeAiVariables is null || snapshot.NativeAiVariables.Length != 8)
            {
                throw new InvalidDataException("Battle snapshot lacks its native shared AI DWORD bank.");
            }
            snapshot.NativeAiVariables.CopyTo(nativeAiVariables, 0);
        }
        ValidateStocks(snapshot.ScriptedRewards, 198);
        scriptedRewards.AddRange(snapshot.ScriptedRewards);
        BattleStock[] rewardItems = snapshot.NativeRewardItems ?? (snapshot.ScriptedRewards.Length == 0 && snapshot.EnemyDefeats.All(defeat => defeat.DropSelection < 0) ? [] : throw new InvalidDataException("Battle snapshot requires an explicitly migrated ordered native item reward bank; prior drop/script ordering cannot be inferred."));
        NativeBattleRewardDistribution.ValidateItemBank(rewardItems);
        nativeRewardItems.AddRange(rewardItems);
        random = snapshot.Random ?? throw new InvalidDataException("Battle snapshot lacks its owned native RNG.");
        random.Validate();
        nativeLastAllocatedTarget = snapshot.NativeLastAllocatedTarget;
        ValidateNativeSavedBattleFlagEffects();
        RestoreNativeLimits(snapshot.NativeLimits ?? throw new InvalidDataException("Saved Limit selection is missing."), expandedHostedEvent, preparedRenzokukenTrigger);
        ValidateNativeDuelControlOwners();
        RestoreNativeBattleUiRandom(snapshot.NativeBattleUiRandom);
        RestoreNativeGuardianBoost(snapshot.NativeGuardianBoost);
        ValidateGuardianFormOwners();
        RestoreNativeScannedModels(snapshot.NativeScannedModels);
        RestoreScanKnowledge(snapshot.ScanKnowledge);
        RestoreEncounterAllocation(snapshot.EncounterAllocation);
        RestoreEncounterPolicies(snapshot.EncounterPolicies);
        RestoreEncounterArrivals(snapshot.EncounterArrivals);
        RestoreEncounterNativeEvents(snapshot.EncounterNativeEvents);
        RestoreNativeKnownMagic(snapshot.NativeKnownMagic);
        RestoreNativeSpecialOwners(snapshot);
        RestoreNativeGlobalRandom(snapshot);
        RestoreNativeSlotsDeal(snapshot.PendingSlotsDeal);
        RestoreTurnAdvance(snapshot.TurnAdvance);
        ValidateOriginalMaximumHitPoints(snapshot);
    }

    private void ValidateSavedAction(string actorId, string targetId, HashSet<string> busy, BattleAbility ability, int actionId, bool allowDefeatedTarget, BattleNativeAction? nativeAction, BattleTargetRequest targetRequest, BattleNativeGraphicCommand? graphic = null, BattleNativeRenzokukenInvocation? renzokuken = null, BattleNativePlayerReaction? reaction = null, BattleActionHeaderReference? nativeHeaderIndex = null, bool allowNativeQueuedState = false, BattleNativeDuelInvocation? duel = null, BattleNativeSlotsRoll? slots = null, byte? nativeQueuedCommand = null, byte? aiCallbackStage = null, NativeLimitCallback? limitCallback = null)
    {
        BattleCombatant actor = GetActor(actorId);
        BattleCombatant target = GetActor(targetId);
        ArgumentNullException.ThrowIfNull(targetRequest);
        bool compelled = targetRequest.Eligibility == BattleTargetEligibility.CompelledAction;
        if (compelled && (!actor.IsPlayer || aiCallbackStage is not null || reaction is not null || targetRequest.Selection is not BattleTargetSelection.Explicit ||
            (ability, nativeQueuedCommand) is not ((BattleAbility.Attack, 1 or 12) or (BattleAbility.Magic, 2) or (BattleAbility.Item, 4)) ||
            nativeAction is not null && nativeAction.Command != nativeQueuedCommand || ability != BattleAbility.Attack && nativeAction is null))
        {
            throw new InvalidDataException("Saved compelled command lacks its player action and target selection.");
        }
        if ((targetRequest.Selection is BattleTargetSelection.Explicit selection && selection.Actors.Any(owner => FindTargetActor(owner) is null)) ||
            (targetRequest.DeathCallbackSelf is { } self && (aiCallbackStage != 3 || self != TargetActorReference(actor) || targetRequest.Selection is BattleTargetSelection.RandomOnSide)))
        {
            throw new InvalidDataException("Saved action has invalid canonical target ownership.");
        }
        if (targetRequest.Eligibility == BattleTargetEligibility.DrawnMagic && aiCallbackStage is not null &&
            (nativeQueuedCommand != 2 || nativeAction is null || !ExpandedBattleActionRules.SupportsDrawnEnemyMagic(nativeAction)))
        {
            throw new InvalidDataException("Saved expanded AI command has an invalid drawn-magic target policy.");
        }
        ValidateNativeHeaderReference(actor, nativeHeaderIndex);
        bool factualHeader = nativeHeaderIndex is not null;
        bool native = nativeAction is not null || graphic is not null || renzokuken is not null || duel is not null;
        bool installedNativeAction = native || HasInstalledNativeAction(actor, new(actorId, targetId, targetRequest, ability, actionId) { NativeAction = nativeAction, NativeGraphic = graphic });
        bool factualNativeAction = factualHeader && installedNativeAction;
        bool physicalQueuedNativeAction = allowNativeQueuedState && NativeGraphics is not null && installedNativeAction;
        ValidateNativePlayerReaction(actor, ability, nativeAction, reaction);
        if (UsesExpandedRoster && reaction == BattleNativePlayerReaction.AutomaticGuardian &&
            (!IsExpandedAutomaticGuardian(nativeAction, reaction) || nativeQueuedCommand != 245 || actionId != nativeAction!.ActionId || factualHeader ||
                target.IsPlayer || actor != NativeSupportActor() || !allowDefeatedTarget && ExpandedAutomaticGuardianTargets().Length == 0))
        {
            throw new InvalidDataException("Saved expanded automatic guardian lacks its installed actor, action, or logical target side.");
        }
        if (UsesExpandedRoster && reaction is BattleNativePlayerReaction.AngeloRush or BattleNativePlayerReaction.AngeloRecover or BattleNativePlayerReaction.AngeloReverse or BattleNativePlayerReaction.AngeloPeriodic &&
            (!IsExpandedAngeloAction(nativeAction) || nativeQueuedCommand != 240 || actionId != nativeAction!.ActionId || factualHeader ||
                (reaction == BattleNativePlayerReaction.AngeloPeriodic ? actor != NativeSupportActor() : reaction != BattleNativePlayerReaction.AngeloRush && actor != target) ||
                !allowDefeatedTarget && !IsExpandedAngeloTarget(target, nativeAction)))
        {
            throw new InvalidDataException("Saved expanded Angelo lacks its installed actor, action, or logical target.");
        }
        ValidateNativeMenuInvocation(actor, ability, actionId, nativeAction, duel, slots);
        if (slots is not null && nativeAction is not null)
        {
            ValidateNativeSlotsPreflight(slots, nativeAction);
        }
        if (renzokuken is not null && (nativeAction is not null || graphic is not null || ability != BattleAbility.LimitBreak || actionId != 5 << 16 || actor.NativeLimit?.Command != 5 || renzokuken.CrisisLevel is < 1 or > 4))
        {
            throw new InvalidDataException("Native Renzokuken control packet contradicts its character or original command.");
        }
        if (graphic is not null && (nativeAction is not null || ability != BattleAbility.EnemyAbility || actionId != 1 ||
            UsesExpandedRoster && (actor.IsPlayer || nativeQueuedCommand != 252 || aiCallbackStage is null or > 4)))
        {
            throw new InvalidDataException("Native graphical packet requires executable command252/id1 without kernel action metadata.");
        }
        if (!native && ability == BattleAbility.EnemyAbility)
        {
            throw new InvalidDataException("Saved native action lacks its exact installed command/action metadata.");
        }
        if (nativeAction is not null)
        {
            BattleNativeAction installed = nativeAction.PresentationOnly ? ValidateEncounterSummonControl(actor, targetId, ability, actionId, targetRequest) : nativeAction is { Command: 241, ActionId: 65530 or 65532 } ? ValidateNativeDuelControl241(actor, nativeHeaderIndex, nativeAction) : FindNativeCommandAction(actor, nativeAction.Command, nativeAction.ActionId, nativeAction.CrisisLevel);
            if (!System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonSerializer.SerializeToElement(installed), System.Text.Json.JsonSerializer.SerializeToElement(nativeAction)))
            {
                throw new InvalidDataException("Queued native action differs from its installed kernel contract.");
            }
        }
        bool expandedShot = UsesExpandedRoster && IsShotCallback(limitCallback) && IsExpandedShotAction(nativeAction) && actor.NativeLimit?.Command == 14;
        bool expandedSlots = UsesExpandedRoster && nativeAction is not null && slots is not null && CanCastExpandedSlots(nativeAction, slots) && actor.NativeLimit?.Command == 16;
        bool expandedRenzokuken = UsesExpandedRoster && renzokuken is not null && nativeQueuedCommand == 5 && !factualHeader && actor.IsPlayer && !target.IsPlayer;
        bool expandedDuel = UsesExpandedRoster && actor is { IsPlayer: true, NativeLimit.Command: 11 } && !target.IsPlayer && !factualHeader &&
            (duel is not null && nativeAction is null && nativeQueuedCommand == 11 || IsDuelCallback(limitCallback) && IsExpandedDuelMove(nativeAction) && nativeQueuedCommand == nativeAction!.Command && actionId == (nativeAction.Command << 16 | nativeAction.ActionId));
        if (UsesExpandedRoster && ability == BattleAbility.LimitBreak && !expandedRenzokuken && !expandedDuel && (nativeAction is null || !SupportsPlayerLimitSelection(actorId) || !expandedShot && !expandedSlots && (!SupportsExpandedLimit(nativeAction) || actor.NativeLimit?.Command != nativeAction.Command) || nativeQueuedCommand != nativeAction.Command || actionId != (nativeAction.Command << 16 | nativeAction.ActionId) || factualHeader || !allowDefeatedTarget && !expandedShot && ExpandedActionTargets(actor, targetId, ability, actionId, targetRequest, nativeAction).Length == 0))
        {
            throw new InvalidDataException("Saved expanded Limit lacks its installed command or logical recipients.");
        }
        if (UsesExpandedRoster && ability == BattleAbility.EnemyAbility && actor.IsPlayer && nativeAction?.Command == 2 &&
            (!HasNativeAngelWing(actor) || !SupportsExpandedAngelWingSpell(nativeAction) || nativeQueuedCommand != 2 || actionId != nativeAction.ActionId ||
                factualHeader || target.IsPlayer || !allowDefeatedTarget && ExpandedActionTargets(actor, targetId, ability, actionId, targetRequest, nativeAction).Length == 0))
        {
            throw new InvalidDataException("Saved expanded Angel Wing spell lacks its installed command or logical recipient.");
        }
        if (UsesExpandedRoster && ability == BattleAbility.NativeCommand && (nativeAction is null || !actor.IsPlayer || !SupportsExpandedOrdinaryCommand(nativeAction) ||
            !actor.NativeCommands.Any(binding => binding.Command == nativeAction.Command && NativeBindingActionId(binding) == nativeAction.ActionId) ||
            nativeQueuedCommand != nativeAction.Command || actionId != (nativeAction.Command << 16 | nativeAction.ActionId) || factualHeader ||
            IsExpandedMiniMogAction(nativeAction) && !target.IsPlayer ||
            !allowDefeatedTarget && ExpandedActionTargets(actor, targetId, ability, actionId, targetRequest, nativeAction).Length == 0))
        {
            throw new InvalidDataException("Saved expanded equipped command lacks its installed binding or logical recipients.");
        }
        bool formEntry = ability == BattleAbility.GuardianForce && actorId == targetId && GetGuardianForm(actorId) is { Phase: BattleGuardianFormPhase.Charging };
        bool validTargetSide = native || formEntry || ability == BattleAbility.Attack || (ability == BattleAbility.Draw ? actor.IsPlayer != target.IsPlayer : CanSelectEitherTargetSide(FindAction(ability, actionId)) || (actor.IsPlayer == target.IsPlayer) == FindAction(ability, actionId).Allies);
        bool unique = busy.Add(actorId);
        bool validTarget = renzokuken is not null || graphic is not null || allowDefeatedTarget || ActionHasValidTarget(targetRequest, graphic);
        if (UsesExpandedRoster && ability == BattleAbility.GuardianForce && !allowDefeatedTarget)
        {
            validTarget = ExpandedActionTargets(actor, targetId, ability, actionId, targetRequest).Length != 0;
        }
        //4842B0 grows DWORD20 despite pending flags4/8; only the native clock/status/80 gates stop it.
        //A validated physical action header may therefore retain a nonzero owned gauge between queueing and action-pause publication.
        //Ordinary Attack/Magic resolve installed metadata through the actor/action catalog and legitimately leave nativeAction null.
        //48E620's successful GF branch queues its FE input without494A60; its already-grown gauge survives before48E830.
        //Expanded native-clock actors retain this gauge between their ID-based queue and action pause.
        //Graphicsless components preserve the same initialization/reaction gauge in their logical clock.
        // Authored AI can refill ATB with opcode 36 after queueing its logical command.
        bool retainedExpandedAiGauge = UsesExpandedRoster && !actor.IsPlayer && actor.NativeAi is not null && ability == BattleAbility.EnemyAbility &&
            !compelled && aiCallbackStage is <= 4 && (nativeAction is not null || graphic is not null);
        bool retainedExpandedClockGauge = UsesExpandedRoster && definition.NativeClockRules is not null && actor.NativeAtb is not null && installedNativeAction;
        bool retainedNativeGauge = retainedExpandedAiGauge || retainedExpandedClockGauge || (factualNativeAction || physicalQueuedNativeAction) && (actor.NativeAtb is not null || factualNativeAction && NativeGraphics is null);
        if (UsesExpandedRoster && aiCallbackStage == 3 && !IsExpandedEnemyDeathAction(actor, aiCallbackStage, targetRequest) || nativeHeaderIndex is null && !allowNativeQueuedState && !CanRetainBattleActionActor(actor, reaction, aiCallbackStage, targetRequest) || actor.Atb != 0 && reaction is null && !retainedNativeGauge || !native && !validTargetSide || !validTarget || !unique && !native && !factualNativeAction && !physicalQueuedNativeAction || !native && !actor.Abilities.Contains(ability) && !HasRetainedReservedCommand(actor, ability, nativeQueuedCommand))
        {
            throw new InvalidDataException("Invalid queued or active battle action.");
        }
    }

    private bool HasRetainedReservedCommand(BattleCombatant actor, BattleAbility ability, byte? nativeQueuedCommand)
    {
        byte command = (ability, nativeQueuedCommand) switch
        {
            (BattleAbility.Magic, 2) => 2,
            (BattleAbility.GuardianForce, 3 or 254) => 3,
            (BattleAbility.Item, 4) => 4,
            (BattleAbility.Draw, 6) => 6,
            _ => 0
        };
        return command != 0 && HasRetainedReservedCommand(actor, command);
    }

    private bool HasRetainedReservedCommand(BattleCombatant actor, byte command)
    {
        return actor.IsPlayer && actor.NativeCommandSlots.Length == 4 && !actor.NativeCommandSlots.Contains(command) && actor.MagicRecordId is int row && definition.MagicVitals is { } vitals && (uint)row < vitals.Records.Length && vitals.GetReservedBattleCommand(row) == command;
    }

    private void PublishAdvanceTurnCandidateWithTerminal(BattleSession candidate, ExpandedBattlePresentationProcess process, ExpandedBattlePresentationProcess.PreparedTerminal preparation)
    {
        if (!ReferenceEquals(expandedPresentationProcess, process) || candidate.deferredExpandedTerminalOutcome != preparation.Terminal.Outcome)
        {
            throw new InvalidOperationException("The prepared terminal result differs from its logical candidate.");
        }
        process.ValidateTerminal(preparation);
        if (preparation.GuardianForm is { } form)
        {
            PublishValidatedCandidate(candidate, new HostedTerminalCandidateProof(preparation.Terminal), () =>
            {
                PublishExpandedHostedTerminal(process, preparation);
                PublishHostedGuardianFormAudio(form);
            });
            return;
        }
        PublishValidatedCandidate(candidate, new HostedTerminalCandidateProof(preparation.Terminal));
        PublishExpandedHostedTerminal(process, preparation);
    }

    private void PublishCandidate(BattleSession candidate)
    {
        PublishValidatedCandidate(candidate, null);
    }

    private void PublishValidatedCandidate(BattleSession candidate, HostedCandidateProof? hostedProof, Action? commitGraphics = null)
    {
        BattleSnapshot candidateSnapshot = candidate.Capture();
        candidate = RestoreSnapshot(candidateSnapshot, null, hostedProof);
        DiagnosticResetCount++;
        ResetDiagnosticCounterOwners();
        CopyCandidateState(candidate, candidateSnapshot);
        commitGraphics?.Invoke();
        RefreshPreparedPresentation();
    }

    private void CopyCandidateState(BattleSession candidate, BattleSnapshot candidateSnapshot, bool preparedExpandedAction = false)
    {
        magicVitals = candidate.magicVitals?.DeepCopy();
        nativeActionMemory.State = candidate.nativeActionMemory.State;
        nativePresentationStatusMemory.State = candidate.nativePresentationStatusMemory.State;
        foreach (NativeBattleEnemyAllocation allocation in candidate.nativeAllocations.Skip(nativeAllocations.Count))
        {
            CreateNativeAllocation(allocation);
            nativeAllocations.Add(allocation);
        }
        nativeAllocations.Clear();
        nativeAllocations.AddRange(candidate.nativeAllocations);
        foreach (BattleCombatant actor in combatants)
        {
            BattleCombatant replacement = candidate.byId[actor.Id];
            RestoreNativeParticipant(actor, CaptureNativeParticipant(replacement));
            actor.MutableGuardianForceCompatibility = replacement.GuardianForceCompatibility?.ToArray();
            actor.GuardianFormVitals = null;
            actor.HumanHitPoints = replacement.HumanHitPoints;
            RestoreNativeStatus(actor, CaptureNativeStatus(replacement));
            actor.NativeAtb = replacement.NativeAtb;
            if (replacement.NativeAtb is null)
            {
                actor.Atb = replacement.Atb;
            }
            actor.LimitTurnCrisis = replacement.LimitTurnCrisis;
            actor.IsBusy = replacement.IsBusy;
            actor.Effects.Clear();
            actor.Effects.AddRange(replacement.Effects);
            actor.RestoreMagicInventory(replacement.NativeMagicInventory);
            actor.RestoreDrawnMagic(replacement.DrawnMagicIds.ToArray());
            actor.RestoreDrawSlots(replacement.NativeDrawSlots.ToArray());
            actor.ReprojectNativePlayerStatistics();
        }
        actions.Clear();
        foreach (BattleAttackCommand command in candidate.actions)
        {
            actions.Enqueue(command);
        }
        events.Clear();
        events.AddRange(candidate.events);
        Frame = candidate.Frame;
        NativeHostCallbacks = candidate.NativeHostCallbacks;
        NativeExamTimer = candidate.NativeExamTimer;
        DiagnosticTerminalHold = candidate.DiagnosticTerminalHold;
        NativeEntryAdvantage = candidate.NativeEntryAdvantage;
        Difficulty = candidate.Difficulty;
        Mode = candidate.Mode;
        GunbladeMode = candidate.GunbladeMode;
        nativeEscape = candidate.nativeEscape;
        Outcome = candidate.Outcome;
        DebugOutcome = candidate.DebugOutcome;
        ActiveAction = candidate.ActiveAction;
        RestoreNativeItems(candidate.nativeItems);
        nativeGil = candidate.nativeGil;
        chocoboRewards = candidate.chocoboRewards;
        nativeScannedModels = candidate.nativeScannedModels;
        scanKnowledge = candidate.scanKnowledge;
        nativeKnownMagic = candidate.nativeKnownMagic;
        random = candidate.random;
        candidate.nativeAiVariables.CopyTo(nativeAiVariables, 0);
        persistentAiVariables = candidate.persistentAiVariables?.ToArray();
        scriptedRewards.Clear();
        scriptedRewards.AddRange(candidate.scriptedRewards);
        nativeRewardItems.Clear();
        nativeRewardItems.AddRange(candidate.nativeRewardItems);
        enemyDefeats.Clear();
        enemyDefeats.AddRange(candidate.enemyDefeats);
        nativeLastAllocatedTarget = candidate.nativeLastAllocatedTarget;
        originalParticipantSlotsInitialized = candidate.originalParticipantSlotsInitialized;
        encounterAllocation = candidate.encounterAllocation;
        encounterAllocationRollback = null;
        encounterPolicies = candidate.encounterPolicies;
        encounterArrivals = candidate.encounterArrivals;
        RestoreEncounterNativeEvents(candidate.CaptureEncounterNativeEvents());
        nativeScriptState = candidate.nativeScriptState.DeepCopy();
        nativeReactionOrder = candidate.nativeReactionOrder;
        nativeActionPackets = candidate.nativeActionPackets;
        nativeCommandBanks = candidate.nativeCommandBanks;
        nativePartyStatusMirrors = candidate.nativePartyStatusMirrors;
        nativeElementAttack = candidate.nativeElementAttack;
        nativePartyPresentation = candidate.nativePartyPresentation;
        NativeEffectIoResult = candidate.NativeEffectIoResult;
        NativeFileIo = candidate.NativeFileIo;
        NativeVibrationProcess = candidate.NativeVibrationProcess;
        NativeGlobalSoundProcess = candidate.NativeGlobalSoundProcess;
        NativeMusicProcess = candidate.NativeMusicProcess;
        NativeEffectMemory = candidate.NativeEffectMemory;
        NativeStage = candidate.NativeStage;
        NativeModelTextureCache = candidate.NativeModelTextureCache;
        NativeModelLoader = candidate.NativeModelLoader;
        NativeFileStaging = candidate.NativeFileStaging;
        NativeModelCpuResources = candidate.NativeModelCpuResources;
        nativeActionScratch = candidate.nativeActionScratch;
        RestoreNativeTimedAction(candidate.CaptureNativeTimedAction(), preparedExpandedAction ? candidate.expandedHostedEvent : null);
        RestoreNativeLimits(candidate.CaptureNativeLimits(), preparedExpandedAction ? candidate.expandedHostedEvent : null);
        nativeCards = candidate.nativeCards?.DeepCopy();
        nativeCrtRandom = candidate.nativeCrtRandom;
        nativeBattleUiRandom = candidate.nativeBattleUiRandom;
        nativeGlobalRandom = candidate.nativeGlobalRandom;
        nativeGlobalRandomCalls = candidate.nativeGlobalRandomCalls;
        pendingSlotsDeal = candidate.pendingSlotsDeal;
        NativeCamera = candidate.NativeCamera;
        NativeGraphics = candidate.NativeGraphics;
        if (!preparedExpandedAction)
        {
            RestoreNativeGuardianBoost(candidate.nativeGuardianBoost);
        }
        RestoreGuardianForces(candidateSnapshot);
        RestoreGuardianForms(candidate.CaptureGuardianForms(), candidate.Outcome);
        RestoreNativeSupport(candidate.nativeSupport);
        RestoreExpandedEnemyDraw(candidate.pendingEnemyDraw);
        RestoreNativeBattleClock(candidate.nativeBattleClock);
        turnAdvance = candidate.turnAdvance;
        RestoreOriginalStockDrawOperations(candidate.originalStockDrawOperations.ToArray());
    }
}
