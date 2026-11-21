using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
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
        return active is { Accepted: true, Returned: true, NavigationPrerequisiteGoal: { } goal } && active.Kind == Ff7AutoPlayTargetKind.ActorTalk && active.Generation == observation.Generation && active.VisitId == observation.VisitId && state.NavigationGoal == goal && state.TravelCommitment is { } commitment && commitment.Goal == goal && commitment.NavigationKey == active.NavigationKey && CanContinueOwnedTravel(state, observation with { NavigationKey = commitment.NavigationKey });
    }

    private Ff7AutoPlayTarget[] ReadKnownNavigationPrerequisites(Ff7AutoPlayObservation observation, Ff7AutoPlayTarget goal)
    {
        int[] blockers = Ff7AutoplayDriver.ReadNativeGoalBlockers(session, goal, observation.Events);
        return observation.Events.Where(candidate => IsKnownBlockingNavigationActor(State, observation, goal, candidate, session.Host.Process.Actors.SingleOrDefault(actor => actor.EntityBound && actor.Entity == candidate.Entity), blockers)).ToArray();
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
