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
    private readonly Queue<(int X, int Y, int Z, int Triangle)> recentPositions = new();
    private string? movementEvent;

    public Ff7AutoPlayPolicy(Ff7Session session, Func<string, Ff7SessionSave, string> writeChoiceCheckpoint)
    {
        this.session = session;
        State = session.AutoPlayState?.Clone() ?? new();
        State.Validate();
        planner = CreatePlanner(State);
        discovery = new(session);
        choices = new(session, planner, State, writeChoiceCheckpoint);
        scriptedInput = new(session);
        session.InteractionScriptRequested += ObserveInteraction;
        session.NativeTickCompleted += ObserveTick;
        Publish();
    }

    public Ff7AutoPlayState State { get; }
    public string Status => State.Status ?? "Discovering native events";

    internal static AutoPlayPlanner<Ff7AutoPlayDeferral> CreatePlanner(Ff7AutoPlayState state)
    {
        return new(state.Coverage, progressionOrder: (visit, target) => state.Coverage.Visits.SingleOrDefault(entry => entry.Visit.Key == visit.Key)?.Events.Any(entry => entry.Event.Id == target.Id && entry.Completed && !entry.RouteNeedsRediscovery) == true ? 1 : 0);
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
        if (session.World is { } world)
        {
            if (!world.Process.MovementEnabled || world.Process.ControlLock != 0 || world.Process.MainState != 1)
            {
                return default;
            }
            throw new NotSupportedException("Automatic world exploration requires a native world frontier observation adapter.");
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
        Dictionary<string, string> physicalGuards = new(StringComparer.Ordinal);
        foreach (var entry in State.NavigationGuards.Where(entry => entry.Value.VisitId == observation.VisitId))
        {
            Ff7AutoPlayNavigationGuard guard = entry.Value;
            if (guard.EventId is not string eventId)
            {
                physicalGuards[entry.Key] = driver.ReadPhysicalAvailabilityFingerprint(guard.Entity, guard.BlockingEntities);
                continue;
            }
            Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(target => target.Id == eventId);
            physicalGuards[entry.Key] = target is null ? $"{observation.ProgramIdentity}:absent:{eventId}" : driver.ReadPhysicalAvailabilityFingerprint(target, guard.BlockingEntities);
        }
        planner.RefreshUnavailable(observation.VisitId, physicalGuards);
        if (!observation.CanAcceptInput)
        {
            return default;
        }
        if (State.Active is { } active && (active.Returned || active.Generation != observation.Generation))
        {
            bool changedField = active.Generation != observation.Generation;
            bool nativeCompletion = active.Accepted && active.Returned || changedField && (active.TransitionOwned || active.Kind == Ff7AutoPlayTargetKind.Gateway && active.DestinationField == session.MapId);
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
        if (State.Active is null)
        {
            AutoPlayPlanStep? next = planner.PlanNavigation(observation.NavigationKey, needsRefresh: NeedsNavigationRefresh, allowRemoteConditionRoutes: true);
            if (next is null)
            {
                string reason = observation.AnalysisComplete ? "No reachable unconsumed native event remains in the observed frontier." : observation.SupportReason ?? "Native event analysis remains incomplete.";
                throw new NotSupportedException(reason);
            }
            Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(target => target.Id == next.Event.Id && target.AnalysisComplete);
            if (target is null)
            {
                throw new InvalidOperationException("The shared planner selected a target absent from the current native visit.");
            }
            planner.BeginEvent(observation.VisitId, target.Id);
            State.Active = new(observation.VisitId, target.Id, target.ConditionFingerprint, observation.Generation, target.Entity, target.Script, target.Kind, target.DestinationField, Story(), session.CaptureEventProgressWitness(), NavigationKey: observation.NavigationKey);
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
        if (DetectMovementCycle(current))
        {
            planner.Block(current.VisitId, current.EventId, "Physical navigation repeated a bounded native position cycle.", AutoPlayBlockKind.Walking);
            State.Active = null;
            State.Status = "Replanning after a native navigation cycle";
            Publish();
            return default;
        }
        try
        {
            Ff7AutoplayInput input = selectedTarget.Kind switch
            {
                Ff7AutoPlayTargetKind.ActorTalk => driver.TalkTo(selectedTarget.Entity),
                Ff7AutoPlayTargetKind.ActorContact => driver.Contact(selectedTarget.Entity),
                Ff7AutoPlayTargetKind.Gateway => driver.GatewayAt(selectedTarget.GatewayIndex!.Value),
                _ => ChooseLineInput(driver, selectedTarget)
            };
            return PrepareEntryInput(selectedTarget, input);
        }
        catch (Ff7AutoPlayNavigationUnavailableException absence)
        {
            string key = observation.NavigationKey + ":" + current.EventId + ":physical";
            State.NavigationGuards[key] = new(current.VisitId, selectedTarget.Entity, absence.BlockingEntities.ToArray(), selectedTarget.Id, observation.NavigationKey);
            string fingerprint = driver.ReadPhysicalAvailabilityFingerprint(selectedTarget, absence.BlockingEntities);
            planner.MarkUnavailable(current.VisitId, current.EventId, new(current.ConditionFingerprint, current.VisitId, key, fingerprint, current.EventId));
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
            Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            System.Numerics.Vector3 position = new(player.X4096 >> 12, player.Y4096 >> 12, player.Z4096 >> 12);
            int distance = Ff7Session.DistanceToSegment(new(line.X1, line.Y1, line.Z1), new(line.X2, line.Y2, line.Z2), position, out _);
            bool touching = distance >= 0 && distance < unchecked(player.CollisionRadius * player.CollisionRadius);
            ready = target.Kind == Ff7AutoPlayTargetKind.LineLeave ? line.Touch && !touching : touching;
        }
        if (ready && scriptedInput.ChooseEntryInput(target.Entity, target.Script) is Ff7AutoplayInput button)
        {
            return input with { Held = (ushort)(input.Held | button.Held) };
        }
        return input;
    }

    private void CompleteActive(Ff7AutoPlayObservation observation, Ff7AutoPlayActiveEvent active, bool changedField)
    {
        if (!active.CompletionRecorded)
        {
            string? exit = changedField ? null : observation.Events.SingleOrDefault(target => target.Id == active.EventId)?.ConditionFingerprint;
            bool changedComponent = active.NavigationKey is string source && source != observation.NavigationKey;
            bool navigationProbe = !changedField && observation.Events.SingleOrDefault(target => target.Id == active.EventId)?.MayChangeNavigation == true;
            bool navigationReceipt = changedField || changedComponent || navigationProbe;
            planner.Completed(active.VisitId, active.EventId, navigationReceipt ? observation.VisitId : null, exitSignature: exit, observedStoryChanged: active.BeforeStory != Story(), selections: choices.Selections, entrySignature: active.ConditionFingerprint, entrySignatureAuthoritative: true, targetId: active.EventId);
            if (navigationReceipt && active.NavigationKey is string sourceKey)
            {
                planner.RecordNavigationRoute(sourceKey, observation.NavigationKey, active.EventId, new(observation.VisitId, active.ConditionFingerprint, active.BeforeStory != Story(), choices.Selections, active.EventId), new(sourceKey, active.EventId, active.ConditionFingerprint, active.EventId));
            }
            State.Active = active with { CompletionRecorded = true };
        }
        if (choices.CompleteAtControlReturn())
        {
            Publish();
            return;
        }
        State.Active = null;
        State.Status = "Native event completed; selecting the next frontier";
        Publish();
    }

    private void ObserveInteraction(int entity, int script, bool accepted)
    {
        if (accepted && State.Active is { } active && active.Generation == session.FieldGeneration && active.Entity == entity && active.Script == script)
        {
            Ff7AutoPlayObservation observation = ObservePlanner();
            string signature = observation.Events.SingleOrDefault(target => target.Id == active.EventId)?.ConditionFingerprint ?? active.ConditionFingerprint;
            planner.BeginEvent(active.VisitId, active.EventId);
            State.Active = active with { Accepted = true, ConditionFingerprint = signature, BeforeStory = Story(), BeforeWitness = session.CaptureEventProgressWitness(), NavigationKey = observation.NavigationKey };
            Publish();
        }
    }

    private Ff7AutoPlayObservation ObservePlanner()
    {
        Ff7AutoPlayObservation observation = discovery.Observe();
        AutoPlayEvent[] events = observation.Events.Where(target => target.AnalysisComplete).Select(target => new AutoPlayEvent(target.Id, target.Kind == Ff7AutoPlayTargetKind.Gateway ? AutoPlayEventKind.Transition : AutoPlayEventKind.Interaction, ChangesStory: target.ChangesStory, Order: TargetOrder(target), ConditionSignature: target.ConditionFingerprint)).ToArray();
        planner.Observe(new(observation.VisitId, observation.ProgramIdentity), events);
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.AnalysisComplete && target.MayChangeNavigation && !target.ChangesStory))
        {
            AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(entry => entry.Event.Id == target.Id);
            if (coverage.Completed && !State.Coverage.NavigationRoutes.Any(route => route.SourceKey == observation.NavigationKey && route.EventId == target.Id && route.Route.ConditionSignature == target.ConditionFingerprint))
            {
                coverage.RouteNeedsRediscovery = true;
            }
        }
        planner.ObserveNavigation(observation.VisitId, observation.NavigationKey, events);
        return observation;
    }

    private void ObserveTick(Ff7NativeTickObservation observation)
    {
        if (State.Active is not { } active || active.Generation != observation.Generation)
        {
            return;
        }
        bool returned = active.Accepted && observation.ScriptReturns.Any(owner => owner.Entity == active.Entity && owner.Priority == 1);
        bool transition = active.Accepted && observation.TransitionOwner is { Priority: 1 } owner && owner.Entity == active.Entity;
        if (returned || transition)
        {
            State.Active = active with { Returned = active.Returned || returned, TransitionOwned = active.TransitionOwned || transition };
            Publish();
        }
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
        if (movementEvent != active.EventId)
        {
            movementEvent = active.EventId;
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
    private short Story() => session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);

    private void Publish()
    {
        session.AutoPlayState = State.Clone();
    }

    public void Dispose()
    {
        session.InteractionScriptRequested -= ObserveInteraction;
        session.NativeTickCompleted -= ObserveTick;
    }
}
