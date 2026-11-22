using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

/// <summary>Feeds native FFVII observations and physical controls to the shared exploration planner.</summary>
public sealed partial class Ff7AutoPlayPolicy : IDisposable
{
    private readonly Ff7Session session;
    private readonly Ff7AutoPlayDiscovery discovery;
    private readonly AutoPlayPlanner<Ff7AutoPlayDeferral> planner;
    private readonly Ff7AutoPlayChoiceWorkflow choices;
    private readonly Ff7AutoPlayScriptedInput scriptedInput;
    private readonly Action<Ff7SessionSave>? checkpointBeforeWorldBattle;
    private readonly Queue<(int X, int Y, int Z, int Triangle)> recentPositions = new();
    private readonly Dictionary<(string Visit, string Event), int> nativeDestinationHints = [];
    private readonly Ff7AutoPlayNativeReadiness nativeReadiness = new();
    private readonly Ff7AutoPlayNativeReadiness navigationPreparation = new();
    private readonly Ff7AutoPlayNativeReadiness cameraCompletion = new();
    private readonly Ff7AutoPlayNativeReadiness synchronousTriangleSetup = new();
    private readonly Ff7AutoPlayNativeReadiness synchronousHostCompletion = new();
    private readonly Ff7AutoPlayNativeReadiness excavationContinuation = new();
    private readonly Ff7AutoPlayNativeReadiness autonomousFadeCompletion = new();
    private string? movementEvent;
    private string? ordinaryLegIdentity;
    private (int X, int Y, int Z)? ordinaryLegStart;
    private long movementSampleTick = -1;

    public Ff7AutoPlayPolicy(Ff7Session session, Func<string, Ff7SessionSave, string> writeChoiceCheckpoint, Action<Ff7SessionSave>? checkpointBeforeWorldBattle = null)
    {
        this.session = session;
        this.checkpointBeforeWorldBattle = checkpointBeforeWorldBattle;
        State = session.AutoPlayState?.Clone() ?? new();
        State.Validate();
        RestoreUnscopedMovementFailures(State);
        planner = CreatePlanner(State, (visit, target) => NativeDestinationPhase(State, nativeDestinationHints.TryGetValue((visit.Key, target.Id), out int destination) ? destination : null), (visit, target) => nativeDestinationHints.TryGetValue((visit.Key, target.Id), out int destination) && destination > 64 && destination != short.MaxValue && NativeDestinationPhase(State, destination) == 0, (visit, target) => IsKeymanPreferredEntry(visit, target) || IsExcavationPreferredEntry(visit, target) || IsSnowboardPreferredEntry(visit, target), IsCurrentPersistentGoalEligible);
        discovery = new(session);
        choices = new(session, planner, State, writeChoiceCheckpoint);
        choices.RecoverLostRestoredChoiceObservation();
        ObservePendingMinigameChoice();
        scriptedInput = new(session);
        session.NativeInteractionScriptRequested += ObserveEffectAliasAdmission;
        session.InteractionScriptRequested += ObserveInteraction;
        session.NativeTickCompleted += ObserveTick;
        session.NativeFieldDeparting += ObserveNativeFieldDeparture;
        session.WorldBattleStarting += SaveWorldBattleBoundary;
        session.WorldFieldDeparting += ObserveWorldFieldDeparture;
        ObserveWorldAllocation();
        Publish();
    }

    public Ff7AutoPlayState State { get; }
    private Ff7AutoPlayObservation? ordinaryObservation;
    private IReadOnlyDictionary<string, string>? ordinaryPhysicalGuards;

    public Ff7AutoPlayFrontierProgress FrontierAttention => new(State.FrontierProgressKey, State.FrontierIdleSteps, Ff7AutoPlayProgressWatchdog.IdleStepLimit);
    public string FrontierProgress => $"{State.FrontierProgressKey}:world-catalogs:{worldCatalogs.Count}";
    public string WorkProgress => $"{State.FrontierProgressKey}:world-catalogs:{worldCatalogs.Count}:native-planning:{nativePlanningWork}";
    public string Status => State.Status ?? "Discovering native events";

    internal static AutoPlayPlanner<Ff7AutoPlayDeferral> CreatePlanner(Ff7AutoPlayState state, Func<AutoPlayVisit, AutoPlayEvent, int>? phaseOrder = null, Func<AutoPlayVisit, AutoPlayEvent, bool>? unvisitedDestination = null, Func<AutoPlayVisit, AutoPlayEvent, bool>? preferredNativeEntry = null, Func<AutoPlayVisit, AutoPlayEvent, bool>? currentGoalEligibility = null)
    {
        return new(state.Coverage, phaseOrder: phaseOrder, candidateProgressionOrder: (visit, target, candidate) =>
        {
            AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(entry => entry.Visit.Key == visit.Key)?.Events.SingleOrDefault(entry => entry.Event.Id == target.Id);
            if (state.DestinationField is not null && state.NavigationGoal is { EventId: not null } goal && goal.EventId == target.Id && (goal.Conditions is null || goal.Conditions == target.ConditionSignature) && state.Coverage.NavigationVisits.Any(navigation => navigation.Key == goal.NavigationKey && navigation.VisitKey == visit.Key))
            {
                return -3;
            }
            if (preferredNativeEntry?.Invoke(visit, target) == true)
            {
                return -3;
            }
            bool unconsumedCondition = target.ConditionSignature is string condition && coverage?.Conditions is { } conditions && !conditions.HasExecutedEntry(condition);
            if (coverage?.Completed != true || unconsumedCondition)
            {
                bool attempted = HasAttemptedEntryCondition(candidate, target);
                return attempted && unvisitedDestination?.Invoke(visit, target) != true ? 0 : target.ChangesStory ? -2 : -1;
            }
            return candidate.RouteNeedsRediscovery ? 0 : Math.Max(1, coverage.Conditions?.ConsumedEntries.Count ?? 1);
        }, observationsCoverWholeVisit: true, candidateExplorationOrder: (visit, target, candidate) =>
        {
            AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = state.Coverage.Visits.SingleOrDefault(entry => entry.Visit.Key == visit.Key)?.Events.SingleOrDefault(entry => entry.Event.Id == target.Id) ?? candidate;
            if (coverage.Completed && coverage.Conditions is { ConsumedEntries.Count: > 0 } conditions && target.ConditionSignature is string condition && condition == coverage.Event.ConditionSignature && !conditions.HasExecutedEntry(condition))
            {
                bool recordedNavigation = coverage.Routes.Any(route => route.DestinationVisitKey != visit.Key) || state.Coverage.NavigationRoutes.Any(route => route.EventId == target.Id && state.Coverage.NavigationVisits.Any(navigation => navigation.Key == route.SourceKey && navigation.VisitKey == visit.Key));
                if (!recordedNavigation)
                {
                    return -1;
                }
            }
            return coverage.Conditions?.ConsumedEntries.Count ?? (coverage.Completed ? 1 : 0);
        }, intrinsicGoal: (visit, target) => (state.DestinationField is not null || IsIntrinsicTransportGoal(state, visit.Key, target)) && currentGoalEligibility?.Invoke(visit, target) != false, reusableRoute: (visit, target, route) => IsReusableTransportRoute(state, visit.Key, target, route));
    }

    internal static int NativeDestinationPhase(Ff7AutoPlayState state, int? destination)
    {
        if (destination is null or <= 64 or short.MaxValue)
        {
            return 0;
        }
        string prefix = $"ff7:{destination}:";
        return state.Coverage.Visits.Any(visit => visit.Visit.Key.StartsWith(prefix, StringComparison.Ordinal)) ? 1 : 0;
    }

    internal static bool HasAttemptedEntryCondition(AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage, AutoPlayEvent target)
    {
        return coverage?.UnavailableAttempts.Any(attempt => attempt.SourceConditionSignature == target.ConditionSignature && (target.Variants is { Count: > 0 } variants ? variants : [target.Id]).Contains(attempt.TargetId ?? target.Id, StringComparer.Ordinal)) == true;
    }

    public int? ChooseDialogueSelection(Ff7Dialogue dialogue)
    {
        if (dialogue.FirstChoice is null)
        {
            return null;
        }
        int? selected = choices.ChooseSelection(dialogue);
        Publish();
        return selected;
    }

    public Ff7AutoplayInput ChooseInput(Ff7AutoplayDriver driver)
    {
        ObserveSummitRecoveryHost();
        RetireCurrentPersistentNoEffectGoal();
        nativeInputReservation = null;
        unknownTalkContactSearchOwner = null;
        try
        {
            Ff7NativePlanningQuantum quantum = new();
            Ff7AutoplayInput input = ChooseInput(driver, quantum, out bool controllable);
            if (driver.PlanningPending)
            {
                if (OwnsUnknownTalkContactSearch(State.Active, unknownTalkContactSearchOwner))
                {
                    CancelNativePlanning();
                }
                if (TryChooseOrdinaryAlternative(driver, out Ff7AutoplayInput ordinaryInput))
                {
                    CancelNativePlanning();
                    driver.BeginPlanningStep();
                    controllable = true;
                    input = ordinaryInput;
                }
                else
                {
                    if (!OwnsUnknownTalkContactSearch(State.Active, unknownTalkContactSearchOwner))
                    {
                        BeginActiveNativePlanning(driver);
                    }
                }
                if (driver.PlanningPending && !OwnsUnknownTalkContactSearch(State.Active, unknownTalkContactSearchOwner) && nativePlanningForActive && nativePlanning is not null && TakeNativePlanningTurn(true, ref activeNativeTurn))
                {
                    nativePlanningQuantum = new();
                    ContinueNativePlanning(driver);
                    if (State.NativeRoute is not null)
                    {
                        driver.BeginPlanningStep();
                        controllable = true;
                        input = ReadNativeRouteInput();
                    }
                }
            }
            else if (nativePlanningForActive)
            {
                CancelNativePlanning();
            }
            try
            {
                TrackControllableFrontierProgress(controllable, driver);
            }
            catch
            {
                RestoreUnappliedNativeInput();
                throw;
            }
            return input;
        }
        finally
        {
            nativeInputReservation = null;
        }
    }

    internal static bool RetireStaleUnacceptedNavigation(Ff7AutoPlayState state, Ff7AutoPlayObservation observation)
    {
        if (!observation.CanAcceptInput || state.NativeRoute is not null || state.PendingRestore is not null || state.Choice is not null || state.Active is not { Accepted: false, Returned: false, TransitionOwned: false, CompletionRecorded: false, NavigationKey: not null } active || active.Kind is < Ff7AutoPlayTargetKind.ActorTalk or > Ff7AutoPlayTargetKind.Gateway || active.Generation != observation.Generation || active.VisitId != observation.VisitId || active.NavigationKey == observation.NavigationKey)
        {
            return false;
        }
        AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == active.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == active.EventId);
        if (coverage?.Conditions?.ActiveEntry == active.ConditionFingerprint)
        {
            coverage.Conditions.ActiveEntry = null;
        }
        state.Active = null;
        return true;
    }

    internal static bool ShouldWaitForEntryFade(Ff7FadeProcess? fade)
    {
        return fade is { EntryCurve: true, Type: 1, Position: > 0, Speed: > 0, Completed: false };
    }

    internal static Ff7AutoplayInput WaitForQueuedNativeLine(Ff7AutoPlayState state, Ff7AutoPlayTarget queued, Action cancelSpeculation)
    {
        cancelSpeculation();
        state.Status = $"Waiting for queued native LINE {queued.Entity}/script {queued.Script}";
        return default;
    }

    internal static bool IsReturnedQueuedLineActivation(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget queued, FieldVmSnapshot vm)
    {
        return state.Active is { Accepted: true, Returned: true, CompletionRecorded: false } active && active.VisitId == observation.VisitId && active.NavigationKey == observation.NavigationKey && active.Generation == observation.Generation && active.EventId == queued.Id && active.Entity == queued.Entity && active.Script == queued.Script && active.Kind == queued.Kind && queued.AnalysisComplete && queued.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave && Ff7AutoPlayExecutionOwners.Matches(active, observation.Generation, vm.ProgramIdentity) && active.ExecutionProvenance is { RootReturned: true, RootDispatchTick: >= 0 } provenance && provenance.RootDispatchTick <= vm.Tick && (uint)queued.Entity < vm.Entities.Length && vm.Entities[queued.Entity].Priority != 1 && !(vm.PendingRequests ?? []).Any(request => request.Entity == queued.Entity && request.Priority == 1);
    }

    private Ff7AutoplayInput ChooseInput(Ff7AutoplayDriver driver, Ff7NativePlanningQuantum quantum, out bool controllable)
    {
        controllable = false;
        ObserveFieldDestinationArrival();
        ordinaryObservation = null;
        ordinaryPhysicalGuards = null;
        nativePlanningQuantum = quantum;
        if (nativePlanning is not null && !CanResumeNativePlanning())
        {
            CancelNativePlanning();
        }
        bool nativeProofCompleted = false;
        AutoPlayPlanStep? resumedNativePlan = null;
        AutoPlayEventAvailability? resumedNativeAvailability = null;
        if (State.PendingRestore is not null || session.Battle is not null)
        {
            return default;
        }
        ObserveWorldAllocation();
        if (session.World is { } world)
        {
            if (world.PendingBattle is not null)
            {
                return default;
            }
            return ChooseWorldInput(driver);
        }
        if (Ff7AutoPlayScriptedInput.IsExcavationProgram(session.Machine.ProgramIdentity) && excavationContinuation.ShouldWaitForExcavationBombContinuation(session.FieldGeneration, session.Machine.Capture(), session.Host.Process, session.Field.Scripts.Data.Span))
        {
            CancelNativePlanning("native-excavation-bomb-continuation");
            driver.BeginPlanningStep();
            controllable = true;
            State.Status = "Waiting for the admitted native excavation bomb continuation";
            Publish();
            return default;
        }
        if (!session.HasPendingTick && IsNativeGaeaWarmthProgram(session.Machine.ProgramIdentity) && ReadNativeGaeaWarmthInput(session.Machine.Capture(), session.Host.Process, session.Machine.Variables, session.Machine.Input) is Ff7AutoplayInput warmth)
        {
            controllable = true;
            driver.BeginPlanningStep();
            State.Status = "Warming through the current native cliff input worker";
            return warmth;
        }
        FieldVmSnapshot dialogueVm = session.Machine.Capture();
        if (autonomousFadeCompletion.ShouldWaitForAutonomousFadeCompletion(session.FieldGeneration, session.Field.Name, dialogueVm, session.Host.Process, session.Field.Scripts.Data.Span, (entity, pc) => session.Machine.AnalyzeScriptAtProgramCounter(entity, pc)))
        {
            CancelNativePlanning("native-autonomous-fade-completion");
            driver.BeginPlanningStep();
            State.Status = "Waiting for owned native fade completion";
            Publish();
            return default;
        }
        if (Ff7AutoPlayNativeReadiness.HasAdvancingDialoguePresentation(dialogueVm, session.Host.Process) || Ff7AutoPlayNativeReadiness.HasOwnedDialogueWait(dialogueVm, session.Host.Process) && nativeReadiness.ShouldWait(session.FieldGeneration, $"dialogue:{session.Field.Name}", dialogueVm, session.Host.Process, session.Field.Scripts.Data.Span, session.Machine.ReadCurrentInputDemands()))
        {
            CancelNativePlanning("native-dialogue-presentation");
            driver.BeginPlanningStep();
            State.Status = "Waiting for owned native dialogue presentation";
            Publish();
            return default;
        }
        if (State.NativeRoute is not null && resumedNativePlan is null)
        {
            controllable = true;
            return ReadNativeRouteInput();
        }
        if (session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is { Ladder.Completed: false } climbing)
        {
            controllable = true;
            return driver.Steer(new(climbing.X4096 / 4096f, climbing.Y4096 / 4096f, climbing.Z4096 / 4096f));
        }
        if (scriptedInput.ChooseInput() is Ff7AutoplayInput nativeInput)
        {
            controllable = true;
            State.Status = $"Native input {scriptedInput.CurrentDemand?.Identity}";
            return nativeInput;
        }
        if (Ff7AutoPlayNativeReadiness.HasFiniteCameraCompletion(session.Host.Process) && cameraCompletion.ShouldWaitForCameraCompletion(session.FieldGeneration, session.Field.Name, session.Machine.Capture(), session.Host.Process, session.Field.Scripts.Data.Span))
        {
            CancelNativePlanning("native-camera-completion");
            driver.BeginPlanningStep();
            State.Status = "Waiting for an owned native camera completion";
            Publish();
            return default;
        }
        RetainTempleReturnDestination();
        Ff7AutoPlayObservation observation = ObservePlanner();
        ordinaryObservation = observation;
        if (State.Active is { Accepted: true, TransitionOwned: true } arrived && arrived.Generation != observation.Generation)
        {
            LearnTransportCompletion(arrived, observation, true);
        }
        if (!observation.CanAcceptInput)
        {
            return default;
        }
        if (driver.ReadPendingNavigationLineRequest(observation.Events) is Ff7AutoPlayTarget queuedLine && !IsReturnedQueuedLineActivation(State, observation, queuedLine, session.Machine.Capture()))
        {
            driver.BeginPlanningStep();
            Ff7AutoplayInput waiting = WaitForQueuedNativeLine(State, queuedLine, () => CancelNativePlanning("queued-native-line"));
            Publish();
            return waiting;
        }
        if (ShouldWaitForEntryFade(session.Host.Process.FadeState))
        {
            CancelNativePlanning("native-entry-fade");
            driver.BeginPlanningStep();
            State.Status = "Waiting for native field-entry fade";
            Publish();
            return default;
        }
        FieldVmSnapshot readinessVm = session.Machine.Capture();
        if (synchronousHostCompletion.ShouldWaitForSynchronousHostCompletion(session.FieldGeneration, observation.NavigationKey, readinessVm, session.Host.Process, session.Field.Scripts.Data.Span, (entity, pc) => session.Machine.AnalyzeScriptAtProgramCounter(entity, pc)) || Ff7AutoPlayNativeReadiness.HasReadyCameraCompletion(session.Host.Process) && cameraCompletion.ShouldWaitForCameraCompletion(session.FieldGeneration, session.Field.Name, readinessVm, session.Host.Process, session.Field.Scripts.Data.Span) || synchronousTriangleSetup.ShouldWaitForSynchronousTriangleSetup(session.FieldGeneration, session.Field.Name, readinessVm, session.Host.Process, session.Field.Scripts.Data.Span, (entity, script) => session.Field.Scripts.Entities[entity].ScriptOffsets[script]) || navigationPreparation.ShouldWaitForNavigationPreparation(session.FieldGeneration, observation.NavigationKey, readinessVm, session.Host.Process, session.Field.Scripts.Data.Span, (entity, pc) => session.Machine.AnalyzeScriptAtProgramCounter(entity, pc)) || Ff7AutoPlayNativeReadiness.HasCompletedSynchronousReturn(readinessVm, session.Field.Scripts.Data.Span) || Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(readinessVm, session.Host.Process, session.Field.Scripts.Data.Span) || Ff7AutoPlayNativeReadiness.HasPendingPlayerControlAcquisition(readinessVm, session.Host.Process, session.Field.Scripts.Data.Span))
        {
            CancelNativePlanning("native-synchronous-return");
            driver.BeginPlanningStep();
            State.Status = "Waiting for an owned native setup completion";
            Publish();
            return default;
        }
        if (State.Active is { Accepted: false } optional && observation.Events.Any(target => target.Id != optional.EventId && IsNativeSnowboardDeparture(observation, target, session.Host.Process, session.Machine.Variables) && driver.HasNativeGeometricContact(target)) && RetireOptionalPlanForSnowboard(State, observation, session.Host.Process, session.Machine.Variables))
        {
            CancelNativePlanning("native-snowboard-departure-priority");
            Publish();
        }
        if (ReadNativeRockPrerequisite(observation) is { } rock && driver.HasNativeGeometricContact(rock) && RetireUnacceptedRockPlan(State, observation, session.Host.Process, session.Machine.Variables, Ff7AutoplayDriver.ReachableTriangles(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle)))
        {
            CancelNativePlanning("native-rock-prerequisite-priority");
            Publish();
        }
        if (ReadNativeIciclePrerequisite(observation) is { } icicle && driver.HasNativeGeometricContact(icicle) && RetireUnacceptedIciclePlan(State, observation, session.Host.Process, session.Machine.Variables, Ff7AutoplayDriver.ReachableTriangles(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle), session.Field.Walkmesh))
        {
            CancelNativePlanning("native-icicle-prerequisite-priority");
            Publish();
        }
        if (ReadNativeSummitRecovery(observation) is { } recovery && driver.HasNativeGeometricContact(recovery) && RetireUnacceptedSummitPlan(State, observation, session.Host.Process, session.Field.Walkmesh))
        {
            CancelNativePlanning("native-summit-recovery-priority");
            Publish();
        }
        if (ReadNativeWhirlwindTutorialPrerequisite(observation) is { } tutorial && driver.HasNativeGeometricContact(tutorial) && RetireUnacceptedWhirlwindTutorialPlan(State, observation, session.Host.Process, session.Machine.Variables, session.Field.Walkmesh))
        {
            CancelNativePlanning("native-whirlwind-tutorial-prerequisite-priority");
            Publish();
        }
        if (ReadNativeGaeaBossPrerequisite(observation) is { } boss && driver.HasNativeGeometricContact(boss) && RetireUnacceptedGaeaBossPlan(State, observation, session.Host.Process, session.Machine.Variables, session.Field.Walkmesh))
        {
            CancelNativePlanning("native-gaea-boss-prerequisite-priority");
            Publish();
        }
        if (ReadNativeGaeaMoviePrerequisite(observation) is { } movie && driver.HasNativeGeometricContact(movie) && RetireUnacceptedGaeaMoviePlan(State, observation, session.Host.Process, session.Machine.Variables, session.Field.Walkmesh))
        {
            CancelNativePlanning("native-gaea-movie-prerequisite-priority");
            Publish();
        }
        bool resumeNative = nativePlanning is not null && !nativePlanningForActive;
        if (resumeNative)
        {
            resumedNativePlan = ContinueNativePlanning(driver);
            if (driver.PlanningPending)
            {
                return default;
            }
            if (resumedNativePlan is not null)
            {
                AutoPlayEventCoverage<Ff7AutoPlayDeferral> admittedCoverage = State.Coverage.Visits.Single(visit => visit.Visit.Key == resumedNativePlan.VisitKey).Events.Single(item => item.Event.Id == resumedNativePlan.Event.Id);
                resumedNativeAvailability = new(admittedCoverage.Unavailable, new(admittedCoverage.UnavailableVariants, StringComparer.Ordinal), admittedCoverage.AvailabilityFingerprint);
            }
            nativeProofCompleted = true;
        }
        controllable = true;
        driver.ConfigureSpatialBoundaries(observation.Events);
        driver.SetPhysicalFailureReceipts(State.NavigationGuards.Where(entry => entry.Value.NavigationKey == observation.NavigationKey && IsUncertifiedMovementAdvice(entry.Key, entry.Value)).Select(entry => entry.Value.PhysicalFailure).OfType<Ff7AutoPlayPhysicalFailureReceipt>());
        Dictionary<string, string> physicalGuards = new(StringComparer.Ordinal);
        foreach (var entry in State.NavigationGuards.Where(entry => entry.Value.VisitId == observation.VisitId).ToArray())
        {
            Ff7AutoPlayNavigationGuard guard = entry.Value;
            if (guard.NavigationKey is not null && guard.NavigationKey != observation.NavigationKey)
            {
                continue;
            }
            if (guard.EventId is not string eventId)
            {
                physicalGuards[entry.Key] = driver.ReadPhysicalAvailabilityFingerprint(guard.Entity, guard.BlockingEntities);
                continue;
            }
            Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(target => target.Id == eventId);
            physicalGuards[entry.Key] = target is null ? $"{observation.ProgramIdentity}:absent:{eventId}" : driver.ReadPhysicalAvailabilityFingerprint(target, guard.BlockingEntities);
            physicalGuards[entry.Key] = ReadMovementFailureFingerprint(physicalGuards[entry.Key], guard.MovementFailure, session.Host.Process.Actors, guard.BlockingEntities);
            var coverage = target is null ? null : State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == target.Id);
            bool currentFailure = coverage?.UnavailableAttempts.Any(attempt => attempt.GuardKey == entry.Key && attempt.GuardFingerprint == physicalGuards[entry.Key]) == true;
            if (currentFailure)
            {
                guard = RestorePhysicalGuard(guard, observation.NavigationKey, session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle, driver.CaptureSpatialBoundaryTriangles());
                State.NavigationGuards[entry.Key] = guard;
            }
            if (guard.NavigationKey == observation.NavigationKey)
            {
                bool? inside = guard.MovementFailure is null ? null : ReadMovementSourceInside(guard.MovementFailure, session.Host.Process.Actors.Single(actor => actor.PlayerControlled));
                if (guard.ObservedFingerprint != physicalGuards[entry.Key] || guard.MovementSourceInside != inside)
                {
                    CancelNativePlanning($"guard:{entry.Key}:fingerprint:{guard.ObservedFingerprint}->{physicalGuards[entry.Key]}:inside:{guard.MovementSourceInside}->{inside}");
                }
                State.NavigationGuards[entry.Key] = guard with { ObservedFingerprint = physicalGuards[entry.Key], MovementSourceInside = inside };
            }
            if (target is not null && guard.SourceTriangle is int triangle && (uint)triangle < session.Field.Walkmesh.Count)
            {
                if (guard.NavigationKey == observation.NavigationKey)
                {
                    RefreshObservedNavigationRefusal(planner, observation.VisitId, target, entry.Key, guard, physicalGuards[entry.Key], currentFailure);
                }
            }
        }
        ordinaryPhysicalGuards = physicalGuards;
        Dictionary<string, IReadOnlyDictionary<string, string>?> componentGuards = new(StringComparer.Ordinal) { [observation.NavigationKey] = physicalGuards };
        AutoPlayNavigationVisit currentNavigation = State.Coverage.NavigationVisits.Single(visit => visit.Key == observation.NavigationKey);
        foreach (var item in State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Where(item => item.Available))
        {
            AutoPlayEvent observed = currentNavigation.Events.Single(target => target.Id == item.Event.Id);
            AutoPlayEventAvailability availability = ReadNavigationAvailability(currentNavigation, observed, observation, driver, componentGuards)!;
            ObserveNativePlanningAvailability(item.Event.Id, availability);
            item.Unavailable = availability.Unavailable;
            item.UnavailableVariants = availability.UnavailableVariants;
            item.AvailabilityFingerprint = availability.Fingerprint;
        }
        CompletePresentationActivation(observation);
        if (choices.ShouldWaitForNativeContinuation(readinessVm, session.Host.Process, session.Field.Scripts.Data.Span))
        {
            driver.BeginPlanningStep();
            State.Status = "Waiting for the actual native choice continuation to return";
            Publish();
            return default;
        }
        if (State.Active is { } active && (active.Returned || active.Generation != observation.Generation))
        {
            bool changedField = active.Generation != observation.Generation;
            bool nativeCompletion = !changedField && active.Accepted && active.Returned && CanCompleteReturnedSource(active, observation.Generation, Ff7AutoPlayExecutionOwners.HasOutstandingChildren(readinessVm, active, observation.Generation, owner => IsSettledPresentationChild(active, readinessVm, owner))) || changedField && (active.TransitionOwned && active.Kind is not (Ff7AutoPlayTargetKind.WorldMesh or Ff7AutoPlayTargetKind.WorldModel) || active.Kind == Ff7AutoPlayTargetKind.Gateway && active.DestinationField == session.MapId || IsSpatialTransitionCompletion(active, session.MapId) || IsAcceptedWorldDeparture(active, State.WorldDispatch, observation.Generation, session.MapId));
            if (nativeCompletion)
            {
                CompleteActive(observation, active, changedField, driver, physicalGuards);
                if (State.PendingRestore is not null)
                {
                    return default;
                }
                if (State.Active is null)
                {
                    driver.BeginPlanningStep();
                    return default;
                }
            }
            else if (changedField)
            {
                State.Status = "A different native event interrupted the planned contact";
                State.Active = null;
                Publish();
            }
        }
        if (RetireStaleUnacceptedNavigation(State, observation))
        {
            CancelNativePlanning("native-navigation-component-changed");
            State.Status = "Replanning from the current native navigation component";
            Publish();
        }
        RefreshOwnedTargetIntent(observation, driver, physicalGuards);
        if (DeferUnavailableActiveNavigation(State, observation))
        {
            Publish();
        }
        if (State.Active is null)
        {
            if (State.Choice is not null && choices.CompleteAtControlReturn())
            {
                Publish();
                return default;
            }
            RetainTempleReturnDestination();
            if (State.NativeRevalidation is { } pendingTask && (State.DestinationField is not null || !observation.Events.Any(target => target.Id == pendingTask.EventId && target.ConditionFingerprint == pendingTask.Condition && target.AnalysisComplete) || !HasSourceRevalidationRefusal(State, pendingTask.NavigationKey, pendingTask.EventId, pendingTask.Condition)))
            {
                State.NativeRevalidation = null;
                Publish();
            }
            if (CancelReleasedSourceRevalidation(State, observation, physicalGuards))
            {
                Publish();
            }
            var ordinaryAvailability = CreateOrdinarySchedulingAvailability(observation, driver, (navigation, target) => ReadNavigationAvailability(navigation, target, observation, driver, componentGuards));
            if (State.NavigationGoal is { EventId: not null } scheduledGoal && State.Coverage.NavigationVisits.SingleOrDefault(visit => visit.Key == scheduledGoal.NavigationKey) is { } goalSource && goalSource.Events.SingleOrDefault(item => item.Id == scheduledGoal.EventId && (scheduledGoal.Conditions is null || item.ConditionSignature == scheduledGoal.Conditions)) is { } goalEvent)
            {
                AutoPlayEventAvailability? nativeAvailability = ReadNavigationAvailability(goalSource, goalEvent, observation, driver, componentGuards);
                if (nativeAvailability?.Unavailable != true && ordinaryAvailability(goalSource, goalEvent)?.Unavailable == true)
                {
                    State.NavigationGoal = null;
                    CancelNativePlanning();
                    Publish();
                }
            }
            bool CanSelectDestination(Ff7AutoPlayTarget target)
            {
                AutoPlayEvent native = currentNavigation.Events.Single(item => item.Id == target.Id);
                return ReadKnownFieldDepartureSource(State, observation, target) is not null || driver.HasNativeGeometricContact(target) && ordinaryAvailability(currentNavigation, native)?.Unavailable != true;
            }
            // Player control can return before synchronous setup finishes changing the walkmesh.
            if (State.DestinationField is not null && nativeReadiness.ShouldWaitForDestinationSetup(session.FieldGeneration, observation.NavigationKey, session.Machine.Capture(), session.Host.Process, session.Field.Scripts.Data.Span, session.Machine.ReadCurrentInputDemands(), (entity, pc) => session.Machine.AnalyzeScriptAtProgramCounter(entity, pc)))
            {
                State.Status = "Waiting for existing native setup before resolving the destination";
                Publish();
                return default;
            }
            Dictionary<(string Producer, string Gateway), int?> gatewayPrerequisiteLengths = [];
            int? ReadGatewayPrerequisiteLength(Ff7AutoPlayTarget producer, Ff7AutoPlayTarget gateway)
            {
                var key = (producer.Id, gateway.Id);
                if (!gatewayPrerequisiteLengths.TryGetValue(key, out int? length))
                {
                    length = ReadDestinationGatewayPrerequisiteLength(producer, gateway, observation.Events);
                    gatewayPrerequisiteLengths.Add(key, length);
                }
                return length;
            }
            string? destinationReason = ResolveFieldDestinationGoal(State, observation, session.ReadAutoPlayFieldConnections, CanSelectDestination, (producer, gateway) => ReadGatewayPrerequisiteLength(producer, gateway) is not null, session.ReadAutoPlayWorldConnections, target => ReadKnownFieldDepartureSource(State, observation, target), field => session.IsKnownAutoPlayDestination(field) ? session.ReadAutoPlayFieldTopology(field) : null, session.Host.Process.Actors.Single(actor => actor.PlayerControlled), session.Host.Process.BlockedTriangles, ReadNativeRockPrerequisite(observation) ?? ReadNativeIciclePrerequisite(observation) ?? ReadNativeSummitRecovery(observation) ?? ReadNativeGaeaBossPrerequisite(observation) ?? ReadNativeGaeaMoviePrerequisite(observation) ?? ReadNativeWhirlwindTutorialPrerequisite(observation), ReadGatewayPrerequisiteLength);
            Ff7NativeSourceRevalidation[] revalidations = State.DestinationField is null ? ReadPendingSourceRevalidations(State, observation) : [];
            string? refreshSource = revalidations.FirstOrDefault()?.NavigationKey;
            bool continuingRefresh = State.NavigationGoal is { EventId: null } goal && revalidations.Any(item => item.NavigationKey == goal.NavigationKey);
            AutoPlayPlanStep? next = resumedNativePlan;
            if (!continuingRefresh)
            {
                next ??= ContinueOwnedTravel(observation, ordinaryAvailability);
                next ??= planner.PlanNavigation(observation.NavigationKey, needsRefresh: NeedsVisitRefresh, preferredGoal: State.NavigationGoal, readAvailability: ordinaryAvailability);
                next ??= planner.PlanNavigation(observation.NavigationKey, needsRefresh: NeedsVisitRefresh, allowRemoteConditionRoutes: true, preferredGoal: State.NavigationGoal, readAvailability: ordinaryAvailability);
            }
            if (refreshSource is not null)
            {
                next ??= SelectSourceRefreshPlan(State, planner, observation, revalidations.First(item => item.NavigationKey == refreshSource), NeedsVisitRefresh, ordinaryAvailability);
            }
            if (!IsDestinationPlan(State, next, observation.NavigationKey))
            {
                next = null;
            }
            if (next is null && !nativeProofCompleted)
            {
                next = TryProveNativeNavigation(observation, driver, physicalGuards, componentGuards);
            }
            if (driver.PlanningPending)
            {
                return default;
            }
            if (next is null)
            {
                if (nativeReadiness.ShouldWait(session.FieldGeneration, observation.NavigationKey, session.Machine.Capture(), session.Host.Process, session.Field.Scripts.Data.Span, session.Machine.ReadCurrentInputDemands()))
                {
                    State.Status = "Waiting for existing native setup to finish";
                    Publish();
                    return default;
                }
                string reason = State.DestinationField is ushort destination ? destinationReason ?? $"The native route toward field {destination} has no currently admitted transition in this navigation component." : observation.AnalysisComplete ? "No reachable unconsumed native event remains in the observed frontier." : observation.SupportReason ?? "Native event analysis remains incomplete.";
                throw new NotSupportedException(reason);
            }
            nativeReadiness.Reset();
            Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(target => target.Id == next.Event.Id && target.AnalysisComplete);
            if (target is null)
            {
                throw new InvalidOperationException("The shared planner selected a target absent from the current native visit.");
            }
            if (resumedNativePlan is not null && resumedNativeAvailability is not null)
            {
                ApplyNativePlanningAvailability(State, observation.VisitId, target.Id, resumedNativeAvailability);
            }
            if (next is { RefreshVisitKey: string refreshKey, Goal.EventId: null } && revalidations.FirstOrDefault(item => item.NavigationKey == refreshKey) is { } selectedDebt)
            {
                RetainSourceRevalidationTask(State, next, selectedDebt);
            }
            planner.BeginEvent(observation.VisitId, target.Id);
            RetainDestinationGoal(next, observation.NavigationKey, target.ConditionFingerprint);
            string? availability = State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == target.Id).AvailabilityFingerprint;
            State.Active = CreateNativeActiveEvent(State, observation, target, next, Story(), session.CaptureEventProgressWitness(), physicalGuards, availability);
            State.Status = $"{session.Field.Name}: {target.Id}";
            Publish();
        }
        Ff7AutoPlayActiveEvent current = State.Active!;
        if (State.NativeRoute is not null)
        {
            return ReadNativeRouteInput();
        }
        if (current.Accepted)
        {
            return default;
        }
        Ff7AutoPlayTarget? selectedTarget = observation.Events.SingleOrDefault(target => target.Id == current.EventId && target.AnalysisComplete);
        if (selectedTarget is null)
        {
            State.Active = null;
            Publish();
            return default;
        }
        driver.ConfigureSpatialBoundaries(observation.Events, selectedTarget);
        if (TryReorderUnacceptedNavigationPrerequisite(observation, selectedTarget) || TryBeginBlockingPushPrerequisite(observation, selectedTarget))
        {
            return default;
        }
        if (DetectMovementCycle(current))
        {
            bool retryTalkContact = RotateFailedTalkContact(current) && ReadFreshTalkContact(driver, observation, selectedTarget) is not null;
            if (!retryTalkContact)
            {
                State.TravelCommitment = null;
            }
            Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            int margin = checked(player.CollisionRadius * 4096);
            Ff7AutoPlayMovementFailure failure = new(recentPositions.Min(position => position.X) - margin, recentPositions.Max(position => position.X) + margin, recentPositions.Min(position => position.Y) - margin, recentPositions.Max(position => position.Y) + margin);
            string key = ReadMovementFailureGuardKey(observation.NavigationKey, current.EventId, "movement-cycle", failure);
            State.NavigationGuards[key] = new(current.VisitId, selectedTarget.Entity, [], selectedTarget.Id, observation.NavigationKey, player.Triangle, failure, driver.CaptureSpatialBoundaryTriangles(), MovementSourceInside: ReadMovementSourceInside(failure, player));
            string fingerprint = ReadMovementFailureFingerprint(driver.ReadPhysicalAvailabilityFingerprint(selectedTarget), failure, session.Host.Process.Actors);
            State.NavigationGuards[key] = CaptureMovementGuardObservation(State.NavigationGuards[key], fingerprint);
            planner.MarkUnavailable(current.VisitId, current.EventId, new(selectedTarget.ConditionFingerprint, current.VisitId, key, fingerprint, current.EventId));
            if (retryTalkContact)
            {
                State.Status = "Trying another native talk contact after a movement cycle";
                Publish();
                return default;
            }
            RecordOrdinaryGoalDeferral(observation, selectedTarget, driver);
            State.Active = null;
            State.Status = "Replanning after a native navigation cycle";
            Publish();
            return default;
        }
        if (nativePlanningForActive && nativePlanning is not null && selectedTarget.Kind == Ff7AutoPlayTargetKind.ActorTalk && ReadFreshTalkContact(driver, observation, selectedTarget) is { Kind: Ff7TalkContactPlanKind.UnknownNoFreshWitness })
        {
            CancelNativePlanning();
        }
        if (CanWaitForNativeAlternatives())
        {
            driver.DeferPlanning();
            return default;
        }
        try
        {
            Ff7AutoplayInput Prepare() => driver.ChooseTargetInput(selectedTarget, observation.Events, run => PrepareEntryInput(selectedTarget, ChooseOrdinaryTargetInput(driver, selectedTarget, run)));
            Ff7Line? phaseLine = current.LinePhase == 0 && selectedTarget.Kind is Ff7AutoPlayTargetKind.LineEnter or Ff7AutoPlayTargetKind.LineLeave ? session.Host.Process.Lines.Single(line => line.Entity == selectedTarget.Entity) : null;
            bool advancePhase = phaseLine is not null && (selectedTarget.Kind == Ff7AutoPlayTargetKind.LineEnter ? !phaseLine.Touch : phaseLine.Touch);
            return advancePhase ? PrepareLinePhase(State, driver, current, Prepare, Publish) : Prepare();
        }
        catch (Ff7AutoPlayNavigationUnavailableException absence)
        {
            bool retryTalkContact = RotateFailedTalkContact(current) && ReadFreshTalkContact(driver, observation, selectedTarget) is not null;
            if (!retryTalkContact)
            {
                State.TravelCommitment = null;
            }
            RecordOrdinaryGoalDeferral(observation, selectedTarget, driver);
            bool retainNativeAlternatives = nativePlanningForActive && nativePlanning is not null && CanResumeNativePlanning(validateIntent: true);
            string key = ReadMovementFailureGuardKey(observation.NavigationKey, current.EventId, "physical", absence.MovementFailure, absence.BlockingEntities);
            Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            State.NavigationGuards[key] = new(current.VisitId, selectedTarget.Entity, absence.BlockingEntities.ToArray(), selectedTarget.Id, observation.NavigationKey, player.Triangle, absence.MovementFailure, driver.CaptureSpatialBoundaryTriangles(), MovementSourceInside: absence.MovementFailure is null ? null : ReadMovementSourceInside(absence.MovementFailure, player), PhysicalFailure: absence.PhysicalFailure);
            string fingerprint = ReadMovementFailureFingerprint(driver.ReadPhysicalAvailabilityFingerprint(selectedTarget, absence.BlockingEntities), absence.MovementFailure, session.Host.Process.Actors, absence.BlockingEntities);
            State.NavigationGuards[key] = CaptureMovementGuardObservation(State.NavigationGuards[key], fingerprint);
            planner.MarkUnavailable(current.VisitId, current.EventId, new(selectedTarget.ConditionFingerprint, current.VisitId, key, fingerprint, current.EventId));
            physicalGuards[key] = fingerprint;
            if (retryTalkContact)
            {
                State.Status = "Trying another native talk contact after physical search exhaustion";
                driver.DeferPlanning();
                Publish();
                return default;
            }
            if (TryRetainFailedOrdinaryTarget(State, current, observation, selectedTarget, physicalGuards))
            {
                if (!retainNativeAlternatives)
                {
                    CancelNativePlanning();
                }
                else
                {
                    nativePlanningIntent = ReadNativePlanningIntent();
                    nativePlanningHeader = ReadNativePlanningHeader(State);
                    nativePlanningAvailability = ReadNativePlanningAvailability(State, observation.VisitId);
                }
                nativePhysicalFailure = Ff7AutoplayDriver.HasNativeRouteClockContract(session) ? new(current, State.NavigationGoal, Ff7AutoplayDriver.ReadNativeRouteFingerprint(session), session.GetPlayerMovementSpeed(true), session.GetPlayerMovementSpeed(false)) : null;
                State.Status = "Trying other ordinary native goals after a local failure";
                driver.DeferPlanning();
                Publish();
                return default;
            }
            Publish();
            throw new NotSupportedException("The ordinary native attempt cannot prepare a legal input and has no remaining supported authenticated alternative.", absence);
        }
    }

    private Ff7AutoplayInput PrepareEntryInput(Ff7AutoPlayTarget target, Ff7AutoplayInput input)
    {
        if (target.Kind == Ff7AutoPlayTargetKind.AutonomousSpatial && target.SpatialInputGuards.Length > 0)
        {
            return scriptedInput.ChooseSpatialEntryInput(target) ?? input;
        }
        bool ready = input.Talk;
        if (target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave)
        {
            Ff7Line line = session.Host.Process.Lines.Last(line => line.Entity == target.Entity);
            ready = IsLineEntryReady(target.Kind, line, input.Talk);
        }
        if (ready && scriptedInput.ChooseEntryInput(target.Entity, target.Script) is Ff7AutoplayInput button)
        {
            return ApplyReadyEntryInput(input, button);
        }
        return input;
    }

    internal static Ff7AutoplayInput ApplyReadyEntryInput(Ff7AutoplayInput approach, Ff7AutoplayInput required)
    {
        ushort held = (ushort)(approach.Held | required.Held);
        bool moving = approach.Horizontal != 0 || approach.Vertical != 0;
        return approach with { Held = held, Run = moving ? (held & 0x40) != 0 : approach.Run };
    }

    internal static bool IsSpatialTransitionCompletion(Ff7AutoPlayActiveEvent active, int actualMap)
    {
        return active.Kind == Ff7AutoPlayTargetKind.AutonomousSpatial && (!IsDirectSpatialTarget(active) && active.DirectEffectOwner is null || active.TransitionOwned) && (active.DestinationField == actualMap || active.SpatialDestinations?.Contains(actualMap) == true);
    }

    private static bool IsDirectSpatialTarget(Ff7AutoPlayActiveEvent active)
    {
        return active.EventId.StartsWith("spatial:", StringComparison.Ordinal) && active.EventId.Contains(":direct:", StringComparison.Ordinal);
    }

    private static bool IsDirectSpatialTransition(Ff7NativeTickObservation observation, Ff7AutoPlayActiveEvent active)
    {
        if (active.Kind != Ff7AutoPlayTargetKind.AutonomousSpatial || active.Script != -1 || active.Returned || active.Generation != observation.Generation || observation.Transition is not { } transition || observation.TransitionOwner is not { Priority: 7 } owner || owner.Entity != active.Entity || owner.Command != transition || active.DestinationField != transition.Map && active.SpatialDestinations?.Contains(transition.Map) != true)
        {
            return false;
        }
        return TryReadDirectSpatialIdentity(active.EventId, out int expectedEntity, out int programCounter, out _) && expectedEntity == active.Entity && programCounter == owner.ProgramCounter;
    }

    internal static bool TryReadDirectSpatialIdentity(string eventId, out int entity, out int programCounter, out int triangle)
    {
        entity = -1;
        programCounter = -1;
        triangle = -1;
        string[] identity = eventId.Split(':');
        bool supported = identity.Length == 6 || identity.Length == 8 && identity[6] == "input" && identity[7].Length == 64 && identity[7].All(Uri.IsHexDigit);
        return supported && identity is ["spatial", var source, var pc, "direct", "triangle", var sample, ..] && int.TryParse(source, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out entity) && entity >= 0 && int.TryParse(pc, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out programCounter) && programCounter >= 0 && int.TryParse(sample, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out triangle) && triangle >= 0;
    }

    internal static bool SourceStoryChanged(Ff7AutoPlayTargetKind kind, int before, int after)
    {
        // Native gateway dispatch only copies its transition descriptor; destination scripts own their progress.
        return kind != Ff7AutoPlayTargetKind.Gateway && before != after;
    }

    internal static bool IsLineEntryReady(Ff7AutoPlayTargetKind kind, Ff7Line line, bool talk)
    {
        return kind switch
        {
            Ff7AutoPlayTargetKind.LineTalk => talk,
            Ff7AutoPlayTargetKind.LinePush => line.Push,
            Ff7AutoPlayTargetKind.LineCross => line.Across,
            Ff7AutoPlayTargetKind.LineTouch => line.Touch,
            Ff7AutoPlayTargetKind.LineEnter => line.Enter,
            Ff7AutoPlayTargetKind.LineLeave => line.Leave,
            _ => false
        };
    }

    internal static bool CanCompleteReturnedSource(Ff7AutoPlayActiveEvent active, long generation, bool outstandingChildren)
    {
        return active.Generation == generation && active.Accepted && active.Returned && !outstandingChildren;
    }

    private void CompleteActive(Ff7AutoPlayObservation observation, Ff7AutoPlayActiveEvent active, bool changedField, Ff7AutoplayDriver? driver = null, IReadOnlyDictionary<string, string>? physicalGuards = null)
    {
        if (!changedField)
        {
            CompleteNativeSummitRecovery(State, observation, active, session.Host.Process);
        }
        bool ownedTravelArrival = IsOwnedTravelArrival(State, active, observation, changedField, IsPendingOwnedSourceGoal(State.NavigationGoal, observation)) || !changedField && IsOwnedNavigationPrerequisiteCompletion(State, active, observation);
        if (!changedField && active.NavigationPrerequisiteGoal is not null && session.Host.Process.Actors.SingleOrDefault(actor => actor.EntityBound && actor.Entity == active.Entity) is { } prerequisiteActor)
        {
            RecordNavigationPrerequisiteCompletion(State, active, prerequisiteActor);
        }
        Ff7AutoPlayActiveEvent completed = RefreshCompletedGatewayTravel(observation, active);
        if (ReferenceEquals(completed, active) && active.TravelRoute is { Selections.Count: > 0 } && active.TravelSelections is { Count: 0 } && active.Accepted && active.Returned && !changedField && observation.Events.SingleOrDefault(target => target.Id == active.EventId)?.ConditionFingerprint == active.ConditionFingerprint)
        {
            FieldScriptAnalysis closure = session.Machine.AnalyzeScript(active.Entity, active.Script);
            bool noChoices = closure.Complete && !closure.InstructionSites.Any(site => session.Field.Scripts.Data.Span[site.ProgramCounter] == 0x48);
            completed = RefreshCompletedChoiceFreeTravel(active, observation, noChoices);
        }
        if (!ReferenceEquals(completed, active))
        {
            active = completed;
            State.Active = active;
        }
        if (active.TravelRoute is { } route)
        {
            AutoPlayTravelChoice.RequireComplete(route, active.TravelSelections);
        }
        if (!active.CompletionRecorded)
        {
            string? exit = changedField ? null : observation.Events.SingleOrDefault(target => target.Id == active.EventId)?.ConditionFingerprint;
            bool changedComponent = active.NavigationKey is string source && source != observation.NavigationKey;
            int completionStory = active.Kind is Ff7AutoPlayTargetKind.WorldMesh or Ff7AutoPlayTargetKind.WorldModel && State.WorldDispatch is { DepartureStory: int departureStory } ? departureStory : Story();
            bool progressChanged = SourceStoryChanged(active.Kind, active.BeforeStory, completionStory);
            if (active.Kind is Ff7AutoPlayTargetKind.WorldMesh or Ff7AutoPlayTargetKind.WorldModel && State.WorldDispatch?.DepartureStory is not null && State.WorldVisits.TryGetValue(active.VisitId, out Ff7AutoPlayWorldVisitState? worldVisit))
            {
                worldVisit.SourceProgressEntries.Add(active.EventId + ":" + active.ConditionFingerprint);
            }
            bool navigationProbe = !changedField && observation.Events.SingleOrDefault(target => target.Id == active.EventId)?.MayChangeNavigation == true;
            bool navigationReceipt = changedField || changedComponent || navigationProbe;
            IReadOnlyList<AutoPlayChoiceSelection> sourceSelections = active.TravelRoute is null ? choices.ReadSourceSelections(active) : active.TravelSelections ?? [];
            RecordControlReturn(planner, active, observation, navigationReceipt, exit, progressChanged, sourceSelections);
            if (navigationReceipt && active.NavigationKey is string sourceKey)
            {
                planner.RecordNavigationRoute(sourceKey, observation.NavigationKey, active.EventId, new(observation.VisitId, active.ConditionFingerprint, progressChanged, sourceSelections, active.EventId), new(sourceKey, active.EventId, active.ConditionFingerprint, active.EventId));
            }
            LearnTransportCompletion(active, observation, changedField);
            State.Active = active with { CompletionRecorded = true };
            if (active.NavigationProbeKey is string probe)
            {
                State.NavigationProbes.Add(probe);
            }
        }
        if (choices.CompleteAtControlReturn())
        {
            Publish();
            return;
        }
        State.Active = null;
        if (State.NavigationGoal is { } goal && IsNavigationGoalCompleted(goal, active, observation.NavigationKey))
        {
            if (goal.EventId is null && State.NativeRevalidation is { } task && task.NavigationKey == observation.NavigationKey)
            {
                State.NativeRevalidation = task with { ArrivalAcknowledged = true };
            }
            State.NavigationGoal = null;
        }
        RetireConsumedEffectAliasGoal();
        State.TravelCommitment = ownedTravelArrival && State.NavigationGoal is { } retained ? new(retained, observation.NavigationKey, State.DestinationField) : null;
        if (driver is not null && physicalGuards is not null)
        {
            RefreshOwnedTargetIntent(observation, driver, physicalGuards);
        }
        State.Status = "Native event completed; selecting the next frontier";
        Publish();
    }

    internal static bool IsNavigationGoalCompleted(AutoPlayTravelGoal goal, Ff7AutoPlayActiveEvent active, string destinationKey)
    {
        return goal.EventId is null ? goal.NavigationKey == destinationKey : goal.NavigationKey == active.NavigationKey && goal.EventId == active.EventId && (goal.Conditions is null || goal.Conditions == active.ConditionFingerprint);
    }

    private void ObserveInteraction(int entity, int script, bool accepted)
    {
        if (accepted && State.TravelCommitment is { Goal: { } retained } && State.Active is { } owner && (owner.NavigationKey == retained.NavigationKey && owner.EventId == retained.EventId && owner.Entity == entity && owner.Script == script || owner.Entity != entity || owner.Script != script))
        {
            State.TravelCommitment = null;
        }
        if (accepted && State.Active is { Accepted: false } active && active.Generation == session.FieldGeneration && active.Entity == entity && active.Script == script && scriptedInput.IsEntryInputSatisfied(entity, script))
        {
            Ff7AutoPlayObservation observation = ObservePlanner();
            string signature = observation.Events.SingleOrDefault(target => target.Id == active.EventId)?.ConditionFingerprint ?? active.ConditionFingerprint;
            string? availability = State.Coverage.Visits.Single(visit => visit.Visit.Key == active.VisitId).Events.Single(item => item.Event.Id == active.EventId).AvailabilityFingerprint;
            planner.BeginEvent(active.VisitId, active.EventId);
            Ff7AutoPlayActiveEvent admitted = active with { Accepted = true, ConditionFingerprint = signature, BeforeStory = Story(), BeforeWitness = session.CaptureEventProgressWitness(), NavigationKey = observation.NavigationKey, EntryAvailabilityFingerprint = availability, TransportEntry = ReadCurrentTransportEntry(observation, active.EventId) };
            bool started = admitted.Kind is >= Ff7AutoPlayTargetKind.ActorTalk and <= Ff7AutoPlayTargetKind.LineLeave && admitted.SpatialOwner is null && admitted.DirectEffectOwner is null && currentInteractionAdmission is { Accepted: true, Started: true, Priority: 1 } actual && actual.Entity == entity && actual.Script == script;
            State.Active = admitted with { ExecutionProvenance = started ? Ff7AutoPlayExecutionOwners.Begin(admitted, session.Machine.ProgramIdentity, session.Field.Scripts.Entities[entity].ScriptOffsets[script], session.Machine.Tick) : active.ExecutionProvenance };
            Publish();
        }
        currentInteractionAdmission = null;
    }

    private Ff7AutoPlayObservation ObservePlanner()
    {
        if (session.World is { } world)
        {
            return ObserveWorldPlanner(world);
        }
        Ff7AutoPlayObservation observation = discovery.Observe();
        ObserveTransportRegions(observation);
        bool nativeCatalogChanged = ReplanForNewNativeTargets(State, observation);
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.Kind == Ff7AutoPlayTargetKind.Gateway && target.DestinationField is not null))
        {
            nativeDestinationHints[(observation.VisitId, target.Id)] = target.DestinationField!.Value;
        }
        AutoPlayEvent[] events = observation.Events.Where(target => target.AnalysisComplete).Select(target => new AutoPlayEvent(target.Id, target.Kind == Ff7AutoPlayTargetKind.Gateway ? AutoPlayEventKind.Transition : AutoPlayEventKind.Interaction, ChangesStory: target.ChangesStory, Order: TargetOrder(target), ConditionSignature: target.ConditionFingerprint) { EffectFingerprint = target.EffectFingerprint }).ToArray();
        bool plannerChanged = NativePlannerObservationChanged(State, observation, events);
        State.VisitGuards.TryGetValue(observation.VisitId, out Ff7AutoPlayVisitGuard? previousGuard);
        planner.Observe(new(observation.VisitId, observation.ProgramIdentity), events);
        if (observation.CanAcceptInput)
        {
            planner.ObserveNavigation(observation.VisitId, observation.NavigationKey, events);
            ObserveNavigationProbes(State, observation);
        }
        ObserveVisitGuard(observation);
        Ff7AutoPlayVisitGuard currentGuard = State.VisitGuards[observation.VisitId];
        if (plannerChanged || previousGuard is null || previousGuard.DiscoveryRevision != currentGuard.DiscoveryRevision || previousGuard.Fingerprint != currentGuard.Fingerprint || !previousGuard.Reads.SequenceEqual(currentGuard.Reads) || !previousGuard.HostReads.SequenceEqual(currentGuard.HostReads))
        {
            CancelNativePlanning();
        }
        if (nativeCatalogChanged)
        {
            Publish();
        }
        return observation;
    }

    internal static bool NativePlannerObservationChanged(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, IReadOnlyList<AutoPlayEvent> events)
    {
        AutoPlayVisitCoverage<Ff7AutoPlayDeferral>? visit = state.Coverage.Visits.SingleOrDefault(item => item.Visit.Key == observation.VisitId);
        if (visit is null || visit.Visit.Region != observation.ProgramIdentity || visit.Events.Count(item => item.Available) != events.Count)
        {
            return true;
        }
        foreach (AutoPlayEvent native in events)
        {
            AutoPlayEventCoverage<Ff7AutoPlayDeferral>? old = visit.Events.SingleOrDefault(item => item.Event.Id == native.Id);
            if (old is null || !old.Available || old.Event != (native with { DestinationVisitKey = native.DestinationVisitKey ?? old.Event.DestinationVisitKey, Kind = old.Event.DestinationVisitKey is null ? native.Kind : AutoPlayEventKind.Transition }) || old.Conditions is null && native.ConditionSignature is not null || old.BlockedReason is not null && native.ConditionSignature is string condition && old.Conditions?.HasExecutedEntry(condition) != true)
            {
                return true;
            }
        }
        if (!observation.CanAcceptInput)
        {
            return false;
        }
        AutoPlayNavigationVisit? navigation = state.Coverage.NavigationVisits.SingleOrDefault(item => item.Key == observation.NavigationKey);
        return navigation is null || !navigation.Events.SequenceEqual(events.OrderBy(item => item.Id, StringComparer.Ordinal)) || observation.Events.Where(target => target.AnalysisComplete && target.MayChangeNavigation && !target.ChangesStory).Any(target => !navigation.RouteRediscovery.TryGetValue(target.Id, out bool current) || current != (visit.Events.Single(item => item.Event.Id == target.Id).Completed && !state.NavigationProbes.Contains(NavigationProbeKey(observation.NavigationKey, target))));
    }

    internal static bool ReplanForNewNativeTargets(Ff7AutoPlayState state, Ff7AutoPlayObservation observation)
    {
        if (state.Active is not { Accepted: false, Returned: false, TransitionOwned: false, CompletionRecorded: false } active || active.Kind is Ff7AutoPlayTargetKind.AutonomousSpatial or Ff7AutoPlayTargetKind.WorldMesh or Ff7AutoPlayTargetKind.WorldModel || active.Generation != observation.Generation && active.Kind == Ff7AutoPlayTargetKind.Gateway || active.VisitId != observation.VisitId)
        {
            return false;
        }
        var known = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.ToDictionary(item => item.Event.Id, StringComparer.Ordinal) ?? [];
        bool newSpatial = observation.Events.Any(target => target.Kind == Ff7AutoPlayTargetKind.AutonomousSpatial && target.AnalysisComplete && !known.ContainsKey(target.Id));
        bool departure = active.Kind == Ff7AutoPlayTargetKind.Gateway || active.DestinationField is not null || observation.Events.Any(target => target.Id == active.EventId && target.ChangesField);
        bool newParticipant = observation.CanAcceptInput && active.Generation == observation.Generation && departure && observation.Events.Any(target =>
        {
            if (!target.AnalysisComplete || target.Id == active.EventId || target.Kind is Ff7AutoPlayTargetKind.Gateway or Ff7AutoPlayTargetKind.WorldMesh or Ff7AutoPlayTargetKind.WorldModel or Ff7AutoPlayTargetKind.AutonomousSpatial)
            {
                return false;
            }
            if (!known.TryGetValue(target.Id, out var previous))
            {
                return true;
            }
            return !previous.Available && (!previous.Completed || previous.Conditions is not null && !previous.Conditions.HasExecutedEntry(target.ConditionFingerprint));
        });
        if (!newSpatial && !newParticipant)
        {
            return false;
        }
        state.Active = null;
        if (state.TravelCommitment is not { } commitment || commitment.Goal is null || state.NavigationGoal != commitment.Goal || state.DestinationField != commitment.DestinationField || observation.NavigationKey != commitment.NavigationKey)
        {
            state.NavigationGoal = null;
            state.TravelCommitment = null;
        }
        state.Status = "Replanning the unaccepted route after native events became available";
        return true;
    }

    internal static string NavigationProbeKey(string navigationKey, Ff7AutoPlayTarget target)
    {
        return $"{navigationKey}|{target.Id}|{target.EntryInputContract}";
    }

    private void ObserveFieldDestinationArrival()
    {
        if (session.World is null && State.DestinationField == session.MapId)
        {
            State.DestinationField = null;
            Publish();
        }
    }

    private void ObserveTick(Ff7NativeTickObservation observation)
    {
        ObserveNativeRouteBoundary();
        ObserveFieldDestinationArrival();
        if (ObserveCompletedSynchronousActivation(observation))
        {
            return;
        }
        if (State.Active is not { } active || active.Generation != observation.Generation)
        {
            return;
        }
        bool spatialChanged = false;
        Ff7AutoPlayActiveEvent tracked = Ff7AutoPlayExecutionOwners.Observe(active, observation.Generation, session.Machine.ProgramIdentity, observation.Tick, observation.ScriptActivity ?? [], (entity, script) => session.Field.Scripts.Entities[entity].ScriptOffsets[script]);
        bool provenanceChanged = !ReferenceEquals(active, tracked);
        active = tracked;
        State.Active = active;
        if (active.SpatialOwner is not null || active.DirectEffectOwner is not null)
        {
            Ff7AutoPlayActiveEvent observed = ObserveSpatialScriptActivity(active, observation.ScriptActivity ?? []);
            spatialChanged = observed != active;
            active = observed;
            State.Active = active;
        }
        bool returned = active.SpatialOwner is null && active.DirectEffectOwner is null && active.Accepted && (active.ExecutionProvenance?.RootReturned ?? observation.ScriptReturns.Any(owner => owner.Entity == active.Entity && owner.Priority == 1));
        bool transition = IsOwnedTransition(observation, active);
        bool minigameCommitted = ObservePendingMinigameChoice();
        if (returned || transition || spatialChanged || provenanceChanged || minigameCommitted)
        {
            State.Active = active with { Returned = active.Returned || returned, TransitionOwned = active.TransitionOwned || transition };
            Publish();
        }
    }

    private bool ObservePendingMinigameChoice()
    {
        if (State.Choice is null || session.Host.PendingMinigame?.Command is not FieldMinigameCommand { Game: 3 })
        {
            return false;
        }
        if (!Ff7AutoPlayChoiceWorkflow.ObserveOwnedMinigameChoice(session.Capture(), State))
        {
            throw new InvalidDataException("The native minigame choice lost its admitted execution owner.");
        }
        return true;
    }

    internal static Ff7AutoPlayActiveEvent ObserveSpatialScriptActivity(Ff7AutoPlayActiveEvent active, IReadOnlyList<Ff7NativeScriptActivity> activities)
    {
        if (active.DirectEffectOwner is { } effect)
        {
            foreach (Ff7NativeScriptActivity activity in activities)
            {
                if (!active.Accepted && activity.HostOperation is { } operation && MatchesDirectSpatialEffect(effect, operation.Request))
                {
                    active = active with { Accepted = true };
                }
                if (active.Accepted && !active.Returned && activity.Boundary is { } boundary && boundary.Entity == effect.Entity && boundary.Priority == effect.Priority && effect.ExitProgramCounters.Contains(boundary.ProgramCounter))
                {
                    active = active with { Returned = true };
                }
            }
            return active;
        }
        if (active.SpatialOwner is not { } expected)
        {
            return active;
        }
        foreach (Ff7NativeScriptActivity activity in activities)
        {
            if (!active.Accepted && activity.Dispatch is { Accepted: true } dispatch && dispatch.SourceEntity == active.Entity && dispatch.SourcePriority == expected.SourcePriority && dispatch.SourceProgramCounter == expected.SourceProgramCounter && dispatch.TargetEntity == expected.TargetEntity && dispatch.TargetScript == expected.TargetScript && dispatch.TargetPriority == expected.TargetPriority && dispatch.RequestMode == expected.RequestMode)
            {
                active = active with { Accepted = true };
            }
            if (active.Accepted && !active.Returned && activity.Return is { } returned && returned.Entity == expected.TargetEntity && returned.Priority == expected.TargetPriority)
            {
                active = active with { Returned = true };
            }
        }
        return active;
    }

    internal static bool MatchesDirectSpatialEffect(FieldSpatialEffectOwner expected, FieldHostRequest request)
    {
        return request.Entity == expected.Entity && request.Priority == expected.Priority && request.ProgramCounter == expected.ProgramCounter && request.Command == expected.Command;
    }

    internal static bool IsOwnedTransition(Ff7NativeTickObservation observation, Ff7AutoPlayActiveEvent active)
    {
        if (IsDirectSpatialTarget(active))
        {
            return IsDirectSpatialTransition(observation, active);
        }
        return active.Accepted && (!active.Returned || active.ExecutionProvenance is not null) && active.Generation == observation.Generation && observation.Transition is not null && observation.TransitionOwner is { } owner && owner.Command == observation.Transition && observation.TransitionOwnership is { } proof && proof.RootEntity == active.Entity && proof.RootScript == active.Script && proof.OwnerEntity == owner.Entity && proof.OwnerPriority == owner.Priority;
    }

    private Ff7AutoplayInput ChooseLineInput(Ff7AutoplayDriver driver, Ff7AutoPlayTarget target, bool run = true)
    {
        Ff7Line line = session.Host.Process.Lines.Single(line => line.Entity == target.Entity);
        if (target.Kind == Ff7AutoPlayTargetKind.LineTalk)
        {
            return driver.TalkToLine(target.Entity, run);
        }
        Ff7AutoPlayActiveEvent active = State.Active!;
        if (target.Kind == Ff7AutoPlayTargetKind.LineLeave)
        {
            if (line.Touch || active.LinePhase != 0)
            {
                return driver.LeaveLine(target.Entity, run);
            }
            return driver.ApproachLine(target.Entity, Ff7AutoPlayTargetKind.LineTouch, run);
        }
        if (target.Kind == Ff7AutoPlayTargetKind.LineEnter && active.LinePhase == 0 && line.Touch)
        {
            return driver.LeaveLine(target.Entity, run);
        }
        return driver.ApproachLine(target.Entity, target.Kind, run);
    }

    internal static Ff7AutoplayInput PrepareLinePhase(Ff7AutoPlayState state, Ff7AutoplayDriver driver, Ff7AutoPlayActiveEvent active, Func<Ff7AutoplayInput> prepare, Action publish)
    {
        Ff7AutoplayInput input = prepare();
        if (!driver.PlanningPending)
        {
            if (state.Active != active)
            {
                throw new InvalidOperationException("The native line owner changed while preparing its input.");
            }
            state.Active = active with { LinePhase = 1 };
            publish();
        }
        return input;
    }

    private bool DetectMovementCycle(Ff7AutoPlayActiveEvent active)
    {
        string identity = $"{active.NavigationKey}:{active.EventId}:{active.Generation}:{ReadTalkContactMovementIdentity(active)}";
        if (movementEvent != identity)
        {
            movementEvent = identity;
            movementSampleTick = -1;
            recentPositions.Clear();
        }
        Ff7Actor? actor = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (actor is null)
        {
            return false;
        }
        string legIdentity = $"{identity}:{active.ConditionFingerprint}";
        if (ordinaryLegIdentity != legIdentity)
        {
            ordinaryLegIdentity = legIdentity;
            ordinaryLegStart = (actor.X4096, actor.Y4096, actor.Z4096);
        }
        return ObserveMovementCycle(recentPositions, actor, session.Host.Process.Tick, ref movementSampleTick);
    }

    internal static bool ObserveMovementCycle(Queue<(int X, int Y, int Z, int Triangle)> positions, Ff7Actor actor, long tick, ref long previousTick)
    {
        if (tick == previousTick)
        {
            return false;
        }
        previousTick = tick;
        positions.Enqueue((actor.X4096, actor.Y4096, actor.Z4096, actor.Triangle));
        while (positions.Count > 96)
        {
            positions.Dequeue();
        }
        return positions.Count == 96 && positions.Distinct().Count() <= 8;
    }

    private static int TargetOrder(Ff7AutoPlayTarget target) => target.Kind == Ff7AutoPlayTargetKind.Gateway ? 10000 + target.GatewayIndex!.Value : target.Entity * 8 + target.Script;
    private short Story() => session.World is { } world ? unchecked((short)(world.ReadPersistentByte(0) | world.ReadPersistentByte(1) << 8)) : session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);

    private void Publish()
    {
        session.AutoPlayState = State.Clone();
    }

    public void Dispose()
    {
        if (summitRecoveryHost is not null)
        {
            summitRecoveryHost.NativeOperationExecuted -= ObserveSummitRecoveryOperation;
        }
        CancelNativePlanning();
        CancelWorldBoardingPlanning();
        CancelDestinationBoardingSearches();
        CancelWorldModePlanning();
        session.NativeInteractionScriptRequested -= ObserveEffectAliasAdmission;
        session.InteractionScriptRequested -= ObserveInteraction;
        session.NativeTickCompleted -= ObserveTick;
        session.NativeFieldDeparting -= ObserveNativeFieldDeparture;
        session.WorldBattleStarting -= SaveWorldBattleBoundary;
        session.WorldFieldDeparting -= ObserveWorldFieldDeparture;
        if (observedWorld is not null)
        {
            observedWorld.NativeMeshDispatched -= ObserveWorldDispatch;
            observedWorld.NativeHostOperationObserved -= ObserveWorldHostOperation;
        }
    }

    private void SaveWorldBattleBoundary()
    {
        if (checkpointBeforeWorldBattle is null)
        {
            return;
        }
        Publish();
        checkpointBeforeWorldBattle(session.Capture());
    }
}
