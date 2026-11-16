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
        session.WorldBattleStarting += SaveWorldBattleBoundary;
        session.WorldFieldDeparting += ObserveWorldFieldDeparture;
        ObserveWorldAllocation();
        Publish();
    }

    public Ff7AutoPlayState State { get; }
    public string WorkProgress => $"{State.FrontierProgressKey}:world-catalogs:{worldCatalogs.Count}";
    public string Status => State.Status ?? "Discovering native events";

    internal static AutoPlayPlanner<Ff7AutoPlayDeferral> CreatePlanner(Ff7AutoPlayState state, Func<AutoPlayVisit, AutoPlayEvent, int>? phaseOrder = null)
    {
        return new(state.Coverage, phaseOrder: phaseOrder, candidateProgressionOrder: (visit, target, candidate) =>
        {
            AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(entry => entry.Visit.Key == visit.Key)?.Events.SingleOrDefault(entry => entry.Event.Id == target.Id);
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
        if (session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is { Ladder.Completed: false } climbing)
        {
            return driver.Steer(new(climbing.X4096 / 4096f, climbing.Y4096 / 4096f, climbing.Z4096 / 4096f));
        }
        if (scriptedInput.ChooseInput() is Ff7AutoplayInput nativeInput)
        {
            State.Status = $"Native input {scriptedInput.CurrentDemand?.Identity}";
            return nativeInput;
        }
        Ff7AutoPlayObservation observation = ObservePlanner();
        if (!observation.CanAcceptInput)
        {
            return default;
        }
        TrackFrontierProgress(State, Story());
        driver.ConfigureSpatialBoundaries(observation.Events);
        Dictionary<string, string> physicalGuards = new(StringComparer.Ordinal);
        Dictionary<string, string> validatedPhysicalGuards = new(StringComparer.Ordinal);
        foreach (var entry in State.NavigationGuards.Where(entry => entry.Value.VisitId == observation.VisitId).ToArray())
        {
            Ff7AutoPlayNavigationGuard guard = entry.Value;
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
                guard = RestorePhysicalGuard(guard, observation.NavigationKey, session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle);
                State.NavigationGuards[entry.Key] = guard;
            }
            if (target is not null && guard.SourceTriangle is int triangle && (uint)triangle < session.Field.Walkmesh.Count)
            {
                validatedPhysicalGuards[entry.Key] = driver.ReadPhysicalAvailabilityFingerprint(target, guard.BlockingEntities, triangle);
                validatedPhysicalGuards[entry.Key] = ReadMovementFailureFingerprint(validatedPhysicalGuards[entry.Key], guard.MovementFailure, session.Host.Process.Actors, guard.BlockingEntities);
                if (guard.NavigationKey == observation.NavigationKey)
                {
                    if (currentFailure)
                    {
                        planner.MarkUnavailable(observation.VisitId, target.Id, new(target.ConditionFingerprint, observation.VisitId, entry.Key, physicalGuards[entry.Key], target.Id));
                    }
                }
            }
        }
        planner.RefreshUnavailable(observation.VisitId, physicalGuards);
        if (State.Active is { } active && (active.Returned || active.Generation != observation.Generation))
        {
            bool changedField = active.Generation != observation.Generation;
            bool nativeCompletion = active.Accepted && active.Returned || changedField && (active.TransitionOwned && active.Kind != Ff7AutoPlayTargetKind.WorldMesh || active.Kind == Ff7AutoPlayTargetKind.Gateway && active.DestinationField == session.MapId || IsSpatialTransitionCompletion(active, session.MapId) || IsAcceptedWorldDeparture(active, State.WorldDispatch, observation.Generation, session.MapId));
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
            Dictionary<string, IReadOnlyDictionary<string, string>?> componentGuards = new(StringComparer.Ordinal);
            string topologyPrefix = observation.NavigationKey[..(observation.NavigationKey.LastIndexOf(':') + 1)];
            AutoPlayPlanStep? next = planner.PlanNavigation(observation.NavigationKey, needsRefresh: visit => NeedsVisitRefresh(visit) || NeedsNavigationRefresh(visit, observation, driver, validatedPhysicalGuards, componentGuards), isCurrent: vertex => vertex.VisitKey != observation.VisitId || vertex.Key.StartsWith(topologyPrefix, StringComparison.Ordinal), preferredGoal: State.NavigationGoal);
            if (next is null)
            {
                if (nativeReadiness.ShouldWait(session.FieldGeneration, observation.NavigationKey, session.Machine.Capture(), session.Host.Process, session.Field.Scripts.Data.Span, session.Machine.ReadCurrentInputDemands()))
                {
                    State.Status = "Waiting for existing native setup to finish";
                    Publish();
                    return default;
                }
                string reason = observation.AnalysisComplete ? "No reachable unconsumed native event remains in the observed frontier." : observation.SupportReason ?? "Native event analysis remains incomplete.";
                throw new NotSupportedException(reason);
            }
            nativeReadiness.Reset();
            Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(target => target.Id == next.Event.Id && target.AnalysisComplete);
            if (target is null)
            {
                throw new InvalidOperationException("The shared planner selected a target absent from the current native visit.");
            }
            planner.BeginEvent(observation.VisitId, target.Id);
            State.NavigationGoal = next.Goal;
            string? availability = State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == target.Id).AvailabilityFingerprint;
            State.Active = new(observation.VisitId, target.Id, target.ConditionFingerprint, observation.Generation, target.Entity, target.Script, target.Kind, target.DestinationField, Story(), session.CaptureEventProgressWitness(), NavigationKey: observation.NavigationKey, NavigationProbeKey: target.MayChangeNavigation ? NavigationProbeKey(observation.NavigationKey, target) : null, EntryAvailabilityFingerprint: availability, SpatialDestinations: target.SpatialDestinations);
            State.Status = $"{session.Field.Name}: {target.Id}";
            Publish();
        }
        Ff7AutoPlayActiveEvent current = State.Active!;
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
        int? selectedLineOwner = selectedTarget.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave ? selectedTarget.Entity : null;
        driver.ConfigureSpatialBoundaries(observation.Events, selectedTarget);
        driver.AvoidLinePush(observation.Events.Where(target => target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave && target.ChangesField && target.Entity != selectedLineOwner && !(selectedTarget.Kind == Ff7AutoPlayTargetKind.Gateway && selectedTarget.DestinationField == target.DestinationField) && RequiresMovementLineAvoidance(target, selectedTarget, session.Machine.Input)).Select(target => target.Entity).Distinct().ToArray());
        if (DetectMovementCycle(current))
        {
            Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            int margin = checked(player.CollisionRadius * 4096);
            Ff7AutoPlayMovementFailure failure = new(recentPositions.Min(position => position.X) - margin, recentPositions.Max(position => position.X) + margin, recentPositions.Min(position => position.Y) - margin, recentPositions.Max(position => position.Y) + margin);
            string key = ReadMovementFailureGuardKey(observation.NavigationKey, current.EventId, "movement-cycle", failure);
            State.NavigationGuards[key] = new(current.VisitId, selectedTarget.Entity, [], selectedTarget.Id, observation.NavigationKey, player.Triangle, failure);
            string fingerprint = ReadMovementFailureFingerprint(driver.ReadPhysicalAvailabilityFingerprint(selectedTarget), failure, session.Host.Process.Actors);
            planner.MarkUnavailable(current.VisitId, current.EventId, new(selectedTarget.ConditionFingerprint, current.VisitId, key, fingerprint, current.EventId));
            State.Active = null;
            State.Status = "Replanning after a native navigation cycle";
            Publish();
            return default;
        }
        try
        {
            return driver.ChooseTargetInput(selectedTarget, () =>
            {
                Ff7AutoplayInput input = selectedTarget.Kind switch
                {
                    Ff7AutoPlayTargetKind.ActorTalk => driver.TalkTo(selectedTarget.Entity),
                    Ff7AutoPlayTargetKind.ActorContact => driver.Contact(selectedTarget.Entity),
                    Ff7AutoPlayTargetKind.Gateway => driver.GatewayAt(selectedTarget.GatewayIndex!.Value),
                    Ff7AutoPlayTargetKind.AutonomousSpatial => driver.Approach(selectedTarget.Position, selectedTarget.Triangle),
                    _ => ChooseLineInput(driver, selectedTarget)
                };
                return PrepareEntryInput(selectedTarget, input);
            });
        }
        catch (Ff7AutoPlayNavigationUnavailableException absence)
        {
            string key = ReadMovementFailureGuardKey(observation.NavigationKey, current.EventId, "physical", absence.MovementFailure, absence.BlockingEntities);
            State.NavigationGuards[key] = new(current.VisitId, selectedTarget.Entity, absence.BlockingEntities.ToArray(), selectedTarget.Id, observation.NavigationKey, session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle, absence.MovementFailure);
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
        bool ready = input.Talk;
        if (target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave)
        {
            Ff7Line line = session.Host.Process.Lines.Last(line => line.Entity == target.Entity);
            ready = IsLineEntryReady(target.Kind, line, input.Talk);
        }
        if (ready && scriptedInput.ChooseEntryInput(target.Entity, target.Script) is Ff7AutoplayInput button)
        {
            return input with { Held = (ushort)(input.Held | button.Held) };
        }
        return input;
    }

    internal static bool IsSpatialTransitionCompletion(Ff7AutoPlayActiveEvent active, int actualMap)
    {
        return active.Kind == Ff7AutoPlayTargetKind.AutonomousSpatial && (!IsDirectSpatialTarget(active) || active.TransitionOwned) && (active.DestinationField == actualMap || active.SpatialDestinations?.Contains(actualMap) == true);
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
        string[] identity = active.EventId.Split(':');
        return identity is ["spatial", var entity, var pc, "direct", "triangle", _] && int.TryParse(entity, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int expectedEntity) && expectedEntity == active.Entity && int.TryParse(pc, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int programCounter) && programCounter == owner.ProgramCounter;
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
        if (!active.CompletionRecorded)
        {
            string? exit = changedField ? null : observation.Events.SingleOrDefault(target => target.Id == active.EventId)?.ConditionFingerprint;
            bool changedComponent = active.NavigationKey is string source && source != observation.NavigationKey;
            int completionStory = active.Kind == Ff7AutoPlayTargetKind.WorldMesh && State.WorldDispatch is { DepartureStory: int departureStory } ? departureStory : Story();
            bool progressChanged = SourceStoryChanged(active.Kind, active.BeforeStory, completionStory);
            if (active.Kind == Ff7AutoPlayTargetKind.WorldMesh && State.WorldDispatch?.DepartureStory is not null && State.WorldVisits.TryGetValue(active.VisitId, out Ff7AutoPlayWorldVisitState? worldVisit))
            {
                worldVisit.SourceProgressEntries.Add(active.EventId + ":" + active.ConditionFingerprint);
            }
            bool navigationProbe = !changedField && observation.Events.SingleOrDefault(target => target.Id == active.EventId)?.MayChangeNavigation == true;
            bool navigationReceipt = changedField || changedComponent || navigationProbe;
            RecordControlReturn(planner, active, observation, navigationReceipt, exit, progressChanged, choices.Selections);
            if (navigationReceipt && active.NavigationKey is string sourceKey)
            {
                planner.RecordNavigationRoute(sourceKey, observation.NavigationKey, active.EventId, new(observation.VisitId, active.ConditionFingerprint, progressChanged, choices.Selections, active.EventId), new(sourceKey, active.EventId, active.ConditionFingerprint, active.EventId));
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
        if (State.NavigationGoal is { } goal && goal.NavigationKey == observation.NavigationKey && (goal.EventId is null || goal.EventId == active.EventId))
        {
            State.NavigationGoal = null;
        }
        State.Status = "Native event completed; selecting the next frontier";
        Publish();
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
        bool spatialCatalogChanged = ReplanForNewSpatialTargets(State, observation);
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
        if (spatialCatalogChanged)
        {
            Publish();
        }
        return observation;
    }

    internal static bool ReplanForNewSpatialTargets(Ff7AutoPlayState state, Ff7AutoPlayObservation observation)
    {
        if (state.Active is not { Accepted: false, Returned: false, TransitionOwned: false, CompletionRecorded: false } active || active.Kind is Ff7AutoPlayTargetKind.AutonomousSpatial or Ff7AutoPlayTargetKind.WorldMesh || active.Generation != observation.Generation && active.Kind == Ff7AutoPlayTargetKind.Gateway || active.VisitId != observation.VisitId)
        {
            return false;
        }
        HashSet<string> known = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.Select(item => item.Event.Id).ToHashSet(StringComparer.Ordinal) ?? [];
        if (!observation.Events.Any(target => target.Kind == Ff7AutoPlayTargetKind.AutonomousSpatial && target.AnalysisComplete && !known.Contains(target.Id)))
        {
            return false;
        }
        state.Active = null;
        state.NavigationGoal = null;
        state.Status = "Replanning the unaccepted route after discovering native automatic spatial boundaries";
        return true;
    }

    internal static string NavigationProbeKey(string navigationKey, Ff7AutoPlayTarget target)
    {
        return $"{navigationKey}|{target.Id}|{target.EntryInputContract}";
    }

    private void ObserveTick(Ff7NativeTickObservation observation)
    {
        if (State.Active is not { } active || active.Generation != observation.Generation)
        {
            return;
        }
        bool returned = active.Accepted && observation.ScriptReturns.Any(owner => owner.Entity == active.Entity && owner.Priority == 1);
        bool transition = IsOwnedTransition(observation, active);
        if (returned || transition)
        {
            State.Active = active with { Returned = active.Returned || returned, TransitionOwned = active.TransitionOwned || transition };
            Publish();
        }
    }

    internal static bool IsOwnedTransition(Ff7NativeTickObservation observation, Ff7AutoPlayActiveEvent active)
    {
        if (IsDirectSpatialTarget(active))
        {
            return IsDirectSpatialTransition(observation, active);
        }
        return active is { Accepted: true, Returned: false } && active.Generation == observation.Generation && observation.Transition is not null && observation.TransitionOwner is { } owner && owner.Command == observation.Transition && observation.TransitionOwnership is { } proof && proof.RootEntity == active.Entity && proof.RootScript == active.Script && proof.OwnerEntity == owner.Entity && proof.OwnerPriority == owner.Priority;
    }

    private Ff7AutoplayInput ChooseLineInput(Ff7AutoplayDriver driver, Ff7AutoPlayTarget target)
    {
        Ff7Line line = session.Host.Process.Lines.Single(line => line.Entity == target.Entity);
        if (target.Kind == Ff7AutoPlayTargetKind.LineTalk)
        {
            return driver.TalkToLine(target.Entity);
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
                return driver.LeaveLine(target.Entity);
            }
            return driver.ApproachLine(target.Entity, Ff7AutoPlayTargetKind.LineTouch);
        }
        if (target.Kind == Ff7AutoPlayTargetKind.LineEnter && active.LinePhase == 0 && line.Touch)
        {
            return driver.LeaveLine(target.Entity);
        }
        if (target.Kind == Ff7AutoPlayTargetKind.LineEnter && !line.Touch && active.LinePhase == 0)
        {
            State.Active = active with { LinePhase = 1 };
            Publish();
        }
        return driver.ApproachLine(target.Entity, target.Kind);
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
        session.InteractionScriptRequested -= ObserveInteraction;
        session.NativeTickCompleted -= ObserveTick;
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
