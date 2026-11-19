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
    private string? movementEvent;

    public Ff7AutoPlayPolicy(Ff7Session session, Func<string, Ff7SessionSave, string> writeChoiceCheckpoint, Action<Ff7SessionSave>? checkpointBeforeWorldBattle = null)
    {
        this.session = session;
        this.checkpointBeforeWorldBattle = checkpointBeforeWorldBattle;
        State = session.AutoPlayState?.Clone() ?? new();
        State.Validate();
        RestoreUnscopedMovementFailures(State);
        planner = CreatePlanner(State, (visit, target) => NativeDestinationPhase(State, nativeDestinationHints.TryGetValue((visit.Key, target.Id), out int destination) ? destination : null));
        discovery = new(session);
        choices = new(session, planner, State, writeChoiceCheckpoint);
        scriptedInput = new(session);
        session.InteractionScriptRequested += ObserveInteraction;
        session.NativeTickCompleted += ObserveTick;
        session.NativeFieldDeparting += ObserveNativeFieldDeparture;
        session.WorldBattleStarting += SaveWorldBattleBoundary;
        session.WorldFieldDeparting += ObserveWorldFieldDeparture;
        ObserveWorldAllocation();
        Publish();
    }

    public Ff7AutoPlayState State { get; }
    public string WorkProgress => $"{State.FrontierProgressKey}:world-catalogs:{worldCatalogs.Count}:native-planning:{nativePlanningWork}";
    public string Status => State.Status ?? "Discovering native events";

    internal static AutoPlayPlanner<Ff7AutoPlayDeferral> CreatePlanner(Ff7AutoPlayState state, Func<AutoPlayVisit, AutoPlayEvent, int>? phaseOrder = null)
    {
        return new(state.Coverage, phaseOrder: phaseOrder, candidateProgressionOrder: (visit, target, candidate) =>
        {
            AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(entry => entry.Visit.Key == visit.Key)?.Events.SingleOrDefault(entry => entry.Event.Id == target.Id);
            if (state.DestinationField is not null && state.NavigationGoal is { EventId: not null } goal && goal.EventId == target.Id && (goal.Conditions is null || goal.Conditions == target.ConditionSignature) && state.Coverage.NavigationVisits.Any(navigation => navigation.Key == goal.NavigationKey && navigation.VisitKey == visit.Key))
            {
                return -3;
            }
            if (coverage?.Completed != true)
            {
                bool attempted = coverage?.UnavailableAttempts.Any(attempt => attempt.SourceConditionSignature == target.ConditionSignature && (target.Variants is { Count: > 0 } variants ? variants : [target.Id]).Contains(attempt.TargetId ?? target.Id, StringComparer.Ordinal)) == true;
                return attempted ? 0 : target.ChangesStory ? -2 : -1;
            }
            return candidate.RouteNeedsRediscovery ? 0 : Math.Max(1, coverage.Conditions?.ConsumedEntries.Count ?? 1);
        }, observationsCoverWholeVisit: true);
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
        return ChooseInput(driver, new Ff7NativePlanningQuantum());
    }

    private Ff7AutoplayInput ChooseInput(Ff7AutoplayDriver driver, Ff7NativePlanningQuantum quantum)
    {
        nativePlanningQuantum = quantum;
        if (nativePlanning is not null && !CanResumeNativePlanning())
        {
            CancelNativePlanning();
        }
        bool nativeProofCompleted = false;
        string? completedSourceTask = null;
        AutoPlayPlanStep? resumedNativePlan = null;
        AutoPlayEventAvailability? resumedNativeAvailability = null;
        if (nativePlanning is not null)
        {
            completedSourceTask = nativePlanningTaskEvent;
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
            nativeProofCompleted = completedSourceTask is null;
        }
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
        if (State.NativeRoute is not null && resumedNativePlan is null)
        {
            TrackControllableFrontierProgress();
            return ReadNativeRouteInput();
        }
        if (session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is { Ladder.Completed: false } climbing)
        {
            TrackControllableFrontierProgress();
            return driver.Steer(new(climbing.X4096 / 4096f, climbing.Y4096 / 4096f, climbing.Z4096 / 4096f));
        }
        if (scriptedInput.ChooseInput() is Ff7AutoplayInput nativeInput)
        {
            TrackControllableFrontierProgress();
            State.Status = $"Native input {scriptedInput.CurrentDemand?.Identity}";
            return nativeInput;
        }
        Ff7AutoPlayObservation observation = ObservePlanner();
        if (!observation.CanAcceptInput)
        {
            return default;
        }
        if (State.DestinationField == session.MapId)
        {
            State.DestinationField = null;
        }
        TrackControllableFrontierProgress();
        driver.ConfigureSpatialBoundaries(observation.Events);
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
                State.NavigationGuards[entry.Key] = guard with { ObservedFingerprint = physicalGuards[entry.Key], MovementSourceInside = guard.MovementFailure is null ? null : ReadMovementSourceInside(guard.MovementFailure, session.Host.Process.Actors.Single(actor => actor.PlayerControlled)) };
            }
            if (target is not null && guard.SourceTriangle is int triangle && (uint)triangle < session.Field.Walkmesh.Count)
            {
                if (guard.NavigationKey == observation.NavigationKey)
                {
                    if (currentFailure)
                    {
                        planner.MarkUnavailable(observation.VisitId, target.Id, new(target.ConditionFingerprint, observation.VisitId, entry.Key, physicalGuards[entry.Key], target.Id));
                    }
                }
            }
        }
        Dictionary<string, IReadOnlyDictionary<string, string>?> componentGuards = new(StringComparer.Ordinal) { [observation.NavigationKey] = physicalGuards };
        AutoPlayNavigationVisit currentNavigation = State.Coverage.NavigationVisits.Single(visit => visit.Key == observation.NavigationKey);
        foreach (var item in State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Where(item => item.Available))
        {
            AutoPlayEvent observed = currentNavigation.Events.Single(target => target.Id == item.Event.Id);
            AutoPlayEventAvailability availability = ReadNavigationAvailability(currentNavigation, observed, observation, driver, componentGuards)!;
            item.Unavailable = availability.Unavailable;
            item.UnavailableVariants = availability.UnavailableVariants;
            item.AvailabilityFingerprint = availability.Fingerprint;
        }
        if (State.Active is { } active && (active.Returned || active.Generation != observation.Generation))
        {
            bool changedField = active.Generation != observation.Generation;
            bool nativeCompletion = active.Accepted && active.Returned || changedField && (active.TransitionOwned && active.Kind is not (Ff7AutoPlayTargetKind.WorldMesh or Ff7AutoPlayTargetKind.WorldModel) || active.Kind == Ff7AutoPlayTargetKind.Gateway && active.DestinationField == session.MapId || IsSpatialTransitionCompletion(active, session.MapId) || IsAcceptedWorldDeparture(active, State.WorldDispatch, observation.Generation, session.MapId));
            if (nativeCompletion)
            {
                CompleteActive(observation, active, changedField);
                if (State.PendingRestore is not null)
                {
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
        Ff7AutoplayInput? admittedApproachInput = null;
        Ff7AutoPlayNavigationUnavailableException? admittedApproachFailure = null;
        if (DeferUnavailableActiveNavigation(State, observation, (active, target) =>
        {
            try
            {
                bool continues = TryContinueAdmittedCycleApproach(active, target, observation, driver, physicalGuards, out Ff7AutoplayInput input);
                if (continues)
                {
                    admittedApproachInput = input;
                }
                return continues;
            }
            catch (Ff7AutoPlayNavigationUnavailableException absence)
            {
                admittedApproachFailure = absence;
                return true;
            }
        }))
        {
            Publish();
        }
        if (driver.PlanningPending)
        {
            return default;
        }
        if (State.Active is null)
        {
            if (State.Choice is not null && choices.CompleteAtControlReturn())
            {
                Publish();
                return default;
            }
            if (State.NativeRevalidation is { } pendingTask && (State.DestinationField is not null || !observation.Events.Any(target => target.Id == pendingTask.EventId && target.ConditionFingerprint == pendingTask.Condition && target.AnalysisComplete) || !State.NavigationGuards.Values.Any(guard => guard.NavigationKey == pendingTask.NavigationKey && guard.EventId == pendingTask.EventId && guard.MovementFailure is not null)))
            {
                State.NativeRevalidation = null;
                Publish();
            }
            if (CancelReleasedSourceRevalidation(State, observation, physicalGuards))
            {
                Publish();
            }
            if (!nativeProofCompleted && HasArrivedSourceRevalidation(State, observation))
            {
                if (!Ff7AutoplayDriver.HasNativeRouteClockContract(session))
                {
                    throw new NotSupportedException("The arrived native source cannot be revalidated without its exact clock contract.");
                }
                completedSourceTask = State.NativeRevalidation!.EventId;
                resumedNativePlan = TryProveNativeNavigation(observation, driver, physicalGuards, componentGuards);
                if (driver.PlanningPending)
                {
                    return default;
                }
                nativeProofCompleted = false;
            }
            bool CanSelectDestination(Ff7AutoPlayTarget target)
            {
                AutoPlayEvent native = currentNavigation.Events.Single(item => item.Id == target.Id);
                return ReadNavigationAvailability(currentNavigation, native, observation, driver, componentGuards)?.Unavailable != true;
            }
            string? destinationReason = ResolveFieldDestinationGoal(State, observation, session.ReadAutoPlayFieldConnections, CanSelectDestination, CanUnblockDestinationGateway, session.ReadAutoPlayWorldConnections);
            Ff7NativeSourceRevalidation[] revalidations = State.DestinationField is null ? ReadPendingSourceRevalidations(State, observation) : [];
            string? refreshSource = revalidations.FirstOrDefault()?.NavigationKey;
            bool NeedsSourceRefresh(AutoPlayVisit visit) => revalidations.Any(item => item.NavigationKey == visit.Key) || NeedsVisitRefresh(visit);
            bool continuingRefresh = State.NavigationGoal is { EventId: null } goal && revalidations.Any(item => item.NavigationKey == goal.NavigationKey);
            AutoPlayPlanStep? next = resumedNativePlan;
            if (!continuingRefresh)
            {
                next ??= planner.PlanNavigation(observation.NavigationKey, needsRefresh: NeedsVisitRefresh, preferredGoal: State.NavigationGoal, readAvailability: (navigation, target) => ReadNavigationAvailability(navigation, target, observation, driver, componentGuards));
                next ??= planner.PlanNavigation(observation.NavigationKey, needsRefresh: NeedsVisitRefresh, allowRemoteConditionRoutes: true, preferredGoal: State.NavigationGoal, readAvailability: (navigation, target) => ReadNavigationAvailability(navigation, target, observation, driver, componentGuards));
            }
            if (refreshSource is not null)
            {
                next ??= planner.PlanNavigation(observation.NavigationKey, needsRefresh: NeedsSourceRefresh, refreshNavigationKey: refreshSource, allowRemoteConditionRoutes: true, preferredGoal: new(refreshSource), readAvailability: (navigation, target) => ReadNavigationAvailability(navigation, target, observation, driver, componentGuards));
            }
            if (!IsDestinationPlan(State, next, observation.NavigationKey))
            {
                next = null;
            }
            if (next is null && !nativeProofCompleted)
            {
                next = TryProveNativeNavigation(observation, driver, physicalGuards, componentGuards, completedSourceTask);
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
                State.NativeRevalidation ??= new(selectedDebt.NavigationKey, selectedDebt.EventId, selectedDebt.Condition, selectedDebt.ReceiptKey);
            }
            planner.BeginEvent(observation.VisitId, target.Id);
            RetainDestinationGoal(next, observation.NavigationKey, target.ConditionFingerprint);
            string? availability = State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == target.Id).AvailabilityFingerprint;
            State.Active = new(observation.VisitId, target.Id, target.ConditionFingerprint, observation.Generation, target.Entity, target.Script, target.Kind, target.DestinationField, Story(), session.CaptureEventProgressWitness(), NavigationKey: observation.NavigationKey, NavigationProbeKey: target.MayChangeNavigation ? NavigationProbeKey(observation.NavigationKey, target) : null, EntryAvailabilityFingerprint: availability, SpatialDestinations: target.SpatialDestinations, SpatialOwner: target.SpatialOwner, DirectEffectOwner: target.DirectEffectOwner, TravelRoute: next.Route, TravelSelections: next.Route is null ? null : [], EntryGuardFingerprints: physicalGuards.Where(entry => State.NavigationGuards.TryGetValue(entry.Key, out var guard) && guard.NavigationKey == observation.NavigationKey && guard.EventId == target.Id).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal));
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
        if (DetectMovementCycle(current))
        {
            Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            int margin = checked(player.CollisionRadius * 4096);
            Ff7AutoPlayMovementFailure failure = new(recentPositions.Min(position => position.X) - margin, recentPositions.Max(position => position.X) + margin, recentPositions.Min(position => position.Y) - margin, recentPositions.Max(position => position.Y) + margin);
            string key = ReadMovementFailureGuardKey(observation.NavigationKey, current.EventId, "movement-cycle", failure);
            State.NavigationGuards[key] = new(current.VisitId, selectedTarget.Entity, [], selectedTarget.Id, observation.NavigationKey, player.Triangle, failure, driver.CaptureSpatialBoundaryTriangles(), MovementSourceInside: ReadMovementSourceInside(failure, player));
            string fingerprint = ReadMovementFailureFingerprint(driver.ReadPhysicalAvailabilityFingerprint(selectedTarget), failure, session.Host.Process.Actors);
            planner.MarkUnavailable(current.VisitId, current.EventId, new(selectedTarget.ConditionFingerprint, current.VisitId, key, fingerprint, current.EventId));
            State.Active = null;
            State.Status = "Replanning after a native navigation cycle";
            Publish();
            return default;
        }
        try
        {
            if (admittedApproachFailure is not null)
            {
                throw admittedApproachFailure;
            }
            if (admittedApproachInput is Ff7AutoplayInput retainedInput)
            {
                return retainedInput;
            }
            return driver.ChooseTargetInput(selectedTarget, observation.Events, run => PrepareEntryInput(selectedTarget, ChooseOrdinaryTargetInput(driver, selectedTarget, run)));
        }
        catch (Ff7AutoPlayNavigationUnavailableException absence)
        {
            string key = ReadMovementFailureGuardKey(observation.NavigationKey, current.EventId, "physical", absence.MovementFailure, absence.BlockingEntities);
            Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            State.NavigationGuards[key] = new(current.VisitId, selectedTarget.Entity, absence.BlockingEntities.ToArray(), selectedTarget.Id, observation.NavigationKey, player.Triangle, absence.MovementFailure, driver.CaptureSpatialBoundaryTriangles(), MovementSourceInside: absence.MovementFailure is null ? null : ReadMovementSourceInside(absence.MovementFailure, player));
            string fingerprint = ReadMovementFailureFingerprint(driver.ReadPhysicalAvailabilityFingerprint(selectedTarget, absence.BlockingEntities), absence.MovementFailure, session.Host.Process.Actors, absence.BlockingEntities);
            planner.MarkUnavailable(current.VisitId, current.EventId, new(selectedTarget.ConditionFingerprint, current.VisitId, key, fingerprint, current.EventId));
            State.Active = null;
            State.Status = "Deferred a native contact until its physical availability changes";
            Publish();
            return default;
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

    private void CompleteActive(Ff7AutoPlayObservation observation, Ff7AutoPlayActiveEvent active, bool changedField)
    {
        Ff7AutoPlayActiveEvent completed = RefreshCompletedGatewayTravel(observation, active);
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
            RecordControlReturn(planner, active, observation, navigationReceipt, exit, progressChanged, active.TravelRoute is null ? choices.Selections : active.TravelSelections ?? []);
            if (navigationReceipt && active.NavigationKey is string sourceKey)
            {
                planner.RecordNavigationRoute(sourceKey, observation.NavigationKey, active.EventId, new(observation.VisitId, active.ConditionFingerprint, progressChanged, active.TravelRoute is null ? choices.Selections : active.TravelSelections ?? [], active.EventId), new(sourceKey, active.EventId, active.ConditionFingerprint, active.EventId));
            }
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
        State.Status = "Native event completed; selecting the next frontier";
        Publish();
    }

    internal static bool IsNavigationGoalCompleted(AutoPlayTravelGoal goal, Ff7AutoPlayActiveEvent active, string destinationKey)
    {
        return goal.EventId is null ? goal.NavigationKey == destinationKey : goal.NavigationKey == active.NavigationKey && goal.EventId == active.EventId && (goal.Conditions is null || goal.Conditions == active.ConditionFingerprint);
    }

    private void ObserveInteraction(int entity, int script, bool accepted)
    {
        if (accepted && State.Active is { } active && active.Generation == session.FieldGeneration && active.Entity == entity && active.Script == script && scriptedInput.IsEntryInputSatisfied(entity, script))
        {
            Ff7AutoPlayObservation observation = ObservePlanner();
            string signature = observation.Events.SingleOrDefault(target => target.Id == active.EventId)?.ConditionFingerprint ?? active.ConditionFingerprint;
            string? availability = State.Coverage.Visits.Single(visit => visit.Visit.Key == active.VisitId).Events.Single(item => item.Event.Id == active.EventId).AvailabilityFingerprint;
            planner.BeginEvent(active.VisitId, active.EventId);
            State.Active = active with { Accepted = true, ConditionFingerprint = signature, BeforeStory = Story(), BeforeWitness = session.CaptureEventProgressWitness(), NavigationKey = observation.NavigationKey, EntryAvailabilityFingerprint = availability };
            Publish();
        }
    }

    private Ff7AutoPlayObservation ObservePlanner()
    {
        if (session.World is { } world)
        {
            return ObserveWorldPlanner(world);
        }
        Ff7AutoPlayObservation observation = discovery.Observe();
        bool nativeCatalogChanged = ReplanForNewNativeTargets(State, observation);
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.Kind == Ff7AutoPlayTargetKind.Gateway && target.DestinationField is not null))
        {
            nativeDestinationHints[(observation.VisitId, target.Id)] = target.DestinationField!.Value;
        }
        AutoPlayEvent[] events = observation.Events.Where(target => target.AnalysisComplete).Select(target => new AutoPlayEvent(target.Id, target.Kind == Ff7AutoPlayTargetKind.Gateway ? AutoPlayEventKind.Transition : AutoPlayEventKind.Interaction, ChangesStory: target.ChangesStory, Order: TargetOrder(target), ConditionSignature: target.ConditionFingerprint)).ToArray();
        planner.Observe(new(observation.VisitId, observation.ProgramIdentity), events);
        if (observation.CanAcceptInput)
        {
            planner.ObserveNavigation(observation.VisitId, observation.NavigationKey, events);
            ObserveNavigationProbes(State, observation);
        }
        ObserveVisitGuard(observation);
        if (nativeCatalogChanged)
        {
            Publish();
        }
        return observation;
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
        state.NavigationGoal = null;
        state.Status = "Replanning the unaccepted route after native events became available";
        return true;
    }

    internal static string NavigationProbeKey(string navigationKey, Ff7AutoPlayTarget target)
    {
        return $"{navigationKey}|{target.Id}|{target.EntryInputContract}";
    }

    private void ObserveTick(Ff7NativeTickObservation observation)
    {
        ObserveNativeRouteBoundary();
        if (State.Active is not { } active || active.Generation != observation.Generation)
        {
            return;
        }
        bool spatialChanged = false;
        if (active.SpatialOwner is not null || active.DirectEffectOwner is not null)
        {
            Ff7AutoPlayActiveEvent observed = ObserveSpatialScriptActivity(active, observation.ScriptActivity ?? []);
            spatialChanged = observed != active;
            active = observed;
            State.Active = active;
        }
        bool returned = active.SpatialOwner is null && active.DirectEffectOwner is null && active.Accepted && observation.ScriptReturns.Any(owner => owner.Entity == active.Entity && owner.Priority == 1);
        bool transition = IsOwnedTransition(observation, active);
        if (returned || transition || spatialChanged)
        {
            State.Active = active with { Returned = active.Returned || returned, TransitionOwned = active.TransitionOwned || transition };
            Publish();
        }
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
        return active is { Accepted: true, Returned: false } && active.Generation == observation.Generation && observation.Transition is not null && observation.TransitionOwner is { } owner && owner.Command == observation.Transition && observation.TransitionOwnership is { } proof && proof.RootEntity == active.Entity && proof.RootScript == active.Script && proof.OwnerEntity == owner.Entity && proof.OwnerPriority == owner.Priority;
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
                if (active.LinePhase == 0)
                {
                    State.Active = active with { LinePhase = 1 };
                    Publish();
                }
                return driver.LeaveLine(target.Entity, run);
            }
            return driver.ApproachLine(target.Entity, Ff7AutoPlayTargetKind.LineTouch, run);
        }
        if (target.Kind == Ff7AutoPlayTargetKind.LineEnter && active.LinePhase == 0 && line.Touch)
        {
            return driver.LeaveLine(target.Entity, run);
        }
        if (target.Kind == Ff7AutoPlayTargetKind.LineEnter && !line.Touch && active.LinePhase == 0)
        {
            State.Active = active with { LinePhase = 1 };
            Publish();
        }
        return driver.ApproachLine(target.Entity, target.Kind, run);
    }

    private bool DetectMovementCycle(Ff7AutoPlayActiveEvent active)
    {
        string identity = $"{active.NavigationKey}:{active.EventId}:{active.Generation}";
        if (movementEvent != identity)
        {
            movementEvent = identity;
            recentPositions.Clear();
        }
        Ff7Actor? actor = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (actor is null)
        {
            return false;
        }
        recentPositions.Enqueue((actor.X4096, actor.Y4096, actor.Z4096, actor.Triangle));
        while (recentPositions.Count > 96)
        {
            recentPositions.Dequeue();
        }
        return recentPositions.Count == 96 && recentPositions.Distinct().Count() <= 8;
    }

    private static int TargetOrder(Ff7AutoPlayTarget target) => target.Kind == Ff7AutoPlayTargetKind.Gateway ? 10000 + target.GatewayIndex!.Value : target.Entity * 8 + target.Script;
    private short Story() => session.World is { } world ? unchecked((short)(world.ReadPersistentByte(0) | world.ReadPersistentByte(1) << 8)) : session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);

    private void Publish()
    {
        session.AutoPlayState = State.Clone();
    }

    public void Dispose()
    {
        CancelNativePlanning();
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
