using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private string? validatedNavigationPrerequisite;

    internal static bool IsKnownBlockingNavigationActor(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget goal, Ff7AutoPlayTarget candidate, Ff7Actor? actor, IReadOnlyCollection<int> pushedEntities)
    {
        if (state.Active is not { } active || !MatchesActiveNativeTarget(active, observation, goal) || !CanContinueOwnedTravel(state, observation) || state.NativeRoute is not null || candidate == goal || !observation.Events.Contains(candidate) || candidate.Kind != Ff7AutoPlayTargetKind.ActorTalk || !candidate.AnalysisComplete || !candidate.MayChangeNavigation || candidate.ChangesStory || candidate.ChangesField || actor is not { EntityBound: true, PlayerControlled: false, CollisionEnabled: true, TalkEnabled: true, CollisionRadius: > 0 } || actor.Entity != candidate.Entity || !pushedEntities.Contains(actor.Entity))
        {
            return false;
        }
        var coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == candidate.Id);
        return coverage is { Available: true, BlockedReason: null, PolicyDeferred: false } && coverage.Event.ConditionSignature == candidate.ConditionFingerprint && coverage.Conditions?.HasExecutedEntry(candidate.ConditionFingerprint) == true && !HasCompletedNavigationPrerequisite(state, observation.VisitId, state.NavigationGoal!, candidate, actor);
    }

    private static string NavigationPrerequisiteKey(string visit, AutoPlayTravelGoal goal, string target) => $"{visit}|{target}|{goal.NavigationKey}|{goal.EventId}";

    internal static string ReadNavigationColliderState(Ff7Actor actor) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { actor.Entity, actor.EntityBound, actor.Model, actor.PlayerControlled, actor.Visible, actor.CollisionEnabled, actor.CollisionRadius, actor.TalkEnabled, actor.TalkRadius, actor.X4096, actor.Y4096, actor.Z4096, actor.Triangle })));

    private static bool HasCompletedNavigationPrerequisite(Ff7AutoPlayState state, string visit, AutoPlayTravelGoal goal, Ff7AutoPlayTarget target, Ff7Actor actor)
    {
        return state.NavigationPrerequisiteCompletions.TryGetValue(NavigationPrerequisiteKey(visit, goal, target.Id), out var completed) && completed.Condition == target.ConditionFingerprint && completed.GoalCondition == goal.Conditions && completed.ColliderState == ReadNavigationColliderState(actor);
    }

    internal static void RecordNavigationPrerequisiteCompletion(Ff7AutoPlayState state, Ff7AutoPlayActiveEvent active, Ff7Actor actor)
    {
        if (active.NavigationPrerequisiteGoal is { } goal && active.Accepted && active.Returned && actor.Entity == active.Entity)
        {
            state.NavigationPrerequisiteCompletions[NavigationPrerequisiteKey(active.VisitId, goal, active.EventId)] = new(active.ConditionFingerprint, goal.Conditions, ReadNavigationColliderState(actor));
        }
    }

    internal static bool IsOwnedNavigationPrerequisiteCompletion(Ff7AutoPlayState state, Ff7AutoPlayActiveEvent active, Ff7AutoPlayObservation observation)
    {
        return active is { Accepted: true, Returned: true, NavigationPrerequisiteGoal: { } goal } && active.Generation == observation.Generation && active.VisitId == observation.VisitId && state.NavigationGoal == goal && ((active.Kind is Ff7AutoPlayTargetKind.LineTouch or Ff7AutoPlayTargetKind.LinePush) && IsCurrentDestinationPrerequisiteGoal(state, observation, goal) || active.Kind == Ff7AutoPlayTargetKind.ActorTalk && state.TravelCommitment is { } commitment && commitment.Goal == goal && commitment.NavigationKey == active.NavigationKey && CanContinueOwnedTravel(state, observation with { NavigationKey = commitment.NavigationKey }));
    }

    private static bool IsCurrentDestinationPrerequisiteGoal(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, AutoPlayTravelGoal goal, bool provedIntermediateHop = false)
    {
        return state.DestinationField is ushort destination && goal.NavigationKey == observation.NavigationKey && observation.Events.Any(target => target.Id == goal.EventId && target.ConditionFingerprint == goal.Conditions && target.AnalysisComplete && (TargetDestinations(target).Contains(destination) || target is { Kind: Ff7AutoPlayTargetKind.Gateway, GatewayIndex: not null, DestinationField: > 0 and < short.MaxValue } && (provedIntermediateHop || state.Active?.NavigationPrerequisiteGoal == goal)));
    }

    internal static bool IsCurrentBlockingNavigationLine(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget goal, Ff7AutoPlayTarget candidate, IReadOnlyCollection<int> blockingLines, bool provedIntermediateHop = false)
    {
        if (state.Active is not { } active || !MatchesActiveNativeTarget(active, observation, goal) || state.NavigationGoal is not { } intent || !IsCurrentDestinationPrerequisiteGoal(state, observation, intent, provedIntermediateHop) || intent.EventId != goal.Id || intent.Conditions != goal.ConditionFingerprint || state.NativeRoute is not null || state.PendingRestore is not null || state.Choice is not null || candidate == goal || !observation.Events.Contains(candidate) || candidate.Kind is not (Ff7AutoPlayTargetKind.LineTouch or Ff7AutoPlayTargetKind.LinePush) || candidate is not { AnalysisComplete: true, MayChangeNavigation: true, ChangesStory: false, ChangesField: false } || !blockingLines.Contains(candidate.Entity))
        {
            return false;
        }
        var coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == candidate.Id);
        return coverage is { Available: true, BlockedReason: null, PolicyDeferred: false } && coverage.Event.ConditionSignature == candidate.ConditionFingerprint && (coverage.Conditions is { } conditions ? !conditions.HasExecutedEntry(candidate.ConditionFingerprint) : !coverage.Completed);
    }

    private Ff7AutoPlayTarget[] ReadKnownNavigationPrerequisites(Ff7AutoPlayObservation observation, Ff7AutoPlayTarget goal)
    {
        return ReadKnownNavigationPrerequisitesForState(observation, goal, State);
    }

    private Ff7AutoPlayTarget[] ReadKnownNavigationPrerequisitesForState(Ff7AutoPlayObservation observation, Ff7AutoPlayTarget goal, Ff7AutoPlayState qualification)
    {
        int[] lines = Ff7AutoplayDriver.ReadNativeGoalLineBlockers(session, goal, observation.Events);
        bool provedIntermediateHop = false;
        if (lines.Length != 0 && qualification.DestinationField is ushort destination && goal.Kind == Ff7AutoPlayTargetKind.Gateway && !TargetDestinations(goal).Contains(destination))
        {
            Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            var (hop, _) = goal.DestinationField is >= 1 and <= 64
                ? FindFieldDestinationAdvice(qualification, observation with { Events = [goal] }, destination, field => field is >= 1 and <= 64 ? session.ReadAutoPlayWorldConnections(field) : session.ReadAutoPlayFieldConnections(field), candidate => candidate == goal)
                : FindFieldEntryDestinationAdvice(qualification, observation with { Events = [goal] }, destination, player, session.Host.Process.BlockedTriangles, field => session.IsKnownAutoPlayDestination(field) ? session.ReadAutoPlayFieldTopology(field) : null, candidate => candidate == goal);
            provedIntermediateHop = hop == goal;
        }
        Ff7AutoPlayTarget[] linePrerequisites = lines.SelectMany(entity => observation.Events.Where(candidate => candidate.Entity == entity && IsCurrentBlockingNavigationLine(qualification, observation, goal, candidate, lines, provedIntermediateHop))).ToArray();
        int[] blockers;
        try
        {
            blockers = Ff7AutoplayDriver.ReadNativeGoalBlockers(session, goal, observation.Events);
        }
        catch (Ff7AutoPlayNavigationUnavailableException) when (linePrerequisites.Length != 0)
        {
            blockers = [];
        }
        return observation.Events.Where(candidate => IsKnownBlockingNavigationActor(qualification, observation, goal, candidate, session.Host.Process.Actors.SingleOrDefault(actor => actor.EntityBound && actor.Entity == candidate.Entity), blockers)).Concat(linePrerequisites).Distinct().ToArray();
    }

    private bool TryBeginBlockingPushPrerequisite(Ff7AutoPlayObservation observation, Ff7AutoPlayTarget goal)
    {
        if (State.DestinationField is null || State.Active is not { Kind: Ff7AutoPlayTargetKind.Gateway, Accepted: false, NavigationPrerequisiteGoal: null } active || !MatchesActiveNativeTarget(active, observation, goal))
        {
            return false;
        }
        Ff7AutoPlayTarget? prerequisite = ReadKnownNavigationPrerequisites(observation, goal).FirstOrDefault(target => target.Kind == Ff7AutoPlayTargetKind.LinePush && target.EntryInputContract is null);
        if (prerequisite is null)
        {
            return false;
        }
        SelectNavigationPrerequisite(observation, active, prerequisite);
        return true;
    }

    private bool TryReorderUnacceptedNavigationPrerequisite(Ff7AutoPlayObservation observation, Ff7AutoPlayTarget current)
    {
        if (State.Active is not { Kind: Ff7AutoPlayTargetKind.LinePush, Accepted: false, ExecutionProvenance: null, NavigationPrerequisiteGoal: { } intent } active || State.NativeRoute is not null || State.Choice is not null || State.PendingRestore is not null || State.NavigationGoal != intent || !MatchesActiveNativeTarget(active, observation, current) || observation.Events.SingleOrDefault(target => target.Id == intent.EventId && target.ConditionFingerprint == intent.Conditions && target.Kind == Ff7AutoPlayTargetKind.Gateway) is not { } goal)
        {
            return false;
        }
        string context = $"{active.Generation}:{active.VisitId}:{active.EventId}:{active.ConditionFingerprint}:{intent}";
        if (validatedNavigationPrerequisite == context)
        {
            return false;
        }
        validatedNavigationPrerequisite = context;
        Ff7AutoPlayState qualification = new()
        {
            Active = new(active.VisitId, goal.Id, goal.ConditionFingerprint, active.Generation, goal.Entity, goal.Script, goal.Kind, goal.DestinationField, active.BeforeStory, active.BeforeWitness, NavigationKey: active.NavigationKey),
            Coverage = State.Coverage,
            DestinationField = State.DestinationField,
            NavigationGoal = intent
        };
        Ff7AutoPlayTarget? first = ReadKnownNavigationPrerequisitesForState(observation, goal, qualification).FirstOrDefault(target => target.Kind == Ff7AutoPlayTargetKind.LinePush && target.EntryInputContract is null);
        if (first is null || first == current)
        {
            return false;
        }
        SelectNavigationPrerequisite(observation, active, first);
        return true;
    }

    private void SelectNavigationPrerequisite(Ff7AutoPlayObservation observation, Ff7AutoPlayActiveEvent active, Ff7AutoPlayTarget prerequisite)
    {
        var visit = State.Coverage.Visits.Single(item => item.Visit.Key == active.VisitId);
        var previous = visit.Events.Single(item => item.Event.Id == active.EventId);
        if (previous.Conditions?.ActiveEntry == active.ConditionFingerprint)
        {
            previous.Conditions.ActiveEntry = null;
        }
        var selected = visit.Events.Single(item => item.Event.Id == prerequisite.Id);
        selected.Conditions ??= new();
        selected.Conditions.ActiveEntry = prerequisite.ConditionFingerprint;
        State.Active = new(observation.VisitId, prerequisite.Id, prerequisite.ConditionFingerprint, observation.Generation, prerequisite.Entity, prerequisite.Script, prerequisite.Kind, prerequisite.DestinationField, active.BeforeStory, active.BeforeWitness, NavigationKey: observation.NavigationKey) { NavigationPrerequisiteGoal = State.NavigationGoal };
        CancelNativePlanning("native-blocking-push-prerequisite");
        State.Status = "Preparing the proved native Push prerequisite for the current route hop";
        Publish();
    }

    private bool TryInstallNavigationPrerequisite(Ff7AutoPlayActiveEvent active, AutoPlayTravelGoal? goal, Ff7AutoPlayObservation observation, Ff7AutoPlayNativeRoute proof, string source)
    {
        if (proof.NavigationPrerequisite is not { } prerequisite || observation.Events.SingleOrDefault(target => MatchesActiveNativeTarget(active, observation, target)) is not { } target || !ReadKnownNavigationPrerequisites(observation, target).Contains(prerequisite) || !TryInstallActiveNativeRoute(State, active, goal, observation, proof, source))
        {
            return false;
        }
        var old = State.Coverage.Visits.Single(visit => visit.Visit.Key == active.VisitId).Events.Single(item => item.Event.Id == active.EventId);
        if (old.Conditions?.ActiveEntry == active.ConditionFingerprint)
        {
            old.Conditions.ActiveEntry = null;
        }
        var selected = State.Coverage.Visits.Single(visit => visit.Visit.Key == active.VisitId).Events.Single(item => item.Event.Id == prerequisite.Id);
        selected.Conditions ??= new();
        selected.Conditions.ActiveEntry = prerequisite.ConditionFingerprint;
        State.Active = new(observation.VisitId, prerequisite.Id, prerequisite.ConditionFingerprint, observation.Generation, prerequisite.Entity, prerequisite.Script, prerequisite.Kind, prerequisite.DestinationField, active.BeforeStory, active.BeforeWitness, NavigationKey: observation.NavigationKey) { NavigationPrerequisiteGoal = goal };
        State.NativeRoute = proof with { EventId = prerequisite.Id, ConditionFingerprint = prerequisite.ConditionFingerprint, NavigationPrerequisite = null };
        return true;
    }
}
