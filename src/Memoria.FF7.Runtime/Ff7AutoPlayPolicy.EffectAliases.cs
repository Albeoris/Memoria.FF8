using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private FieldExternalScriptAdmission? currentInteractionAdmission;

    private void ObserveEffectAliasAdmission(FieldExternalScriptAdmission admission)
    {
        currentInteractionAdmission = admission;
        if (!admission.Accepted || !admission.Started || admission.Priority != 1 || State.NativeRoute is not null || State.Choice is not null || State.PendingRestore is not null || State.Active is not { Accepted: false, Returned: false, TransitionOwned: false, CompletionRecorded: false } planned || planned.Entity != admission.Entity || planned.Script == admission.Script || planned.Generation != session.FieldGeneration || planned.TravelRoute is not null || planned.SpatialOwner is not null || planned.DirectEffectOwner is not null)
        {
            return;
        }
        if ((uint)planned.Script >= 32 || (uint)admission.Script >= 32 || (uint)admission.Entity >= session.Field.Scripts.Entities.Count)
        {
            return;
        }
        int expectedEntry = session.Field.Scripts.Entities[planned.Entity].ScriptOffsets[planned.Script];
        int actualEntry = session.Field.Scripts.Entities[admission.Entity].ScriptOffsets[admission.Script];
        if (expectedEntry != actualEntry)
        {
            return;
        }
        Ff7AutoPlayObservation observation = ObservePlanner();
        Ff7AutoPlayTarget? expected = observation.Events.SingleOrDefault(target => target.Id == planned.EventId);
        Ff7AutoPlayTarget? actual = observation.Events.SingleOrDefault(target => target.Entity == admission.Entity && target.Script == admission.Script && target.Kind is >= Ff7AutoPlayTargetKind.ActorTalk and <= Ff7AutoPlayTargetKind.LineLeave);
        if (expected is null || actual is null || expected.EffectFingerprint is null || actual.EffectFingerprint is null || planned.VisitId != observation.VisitId || (uint)planned.Script >= 32 || (uint)admission.Script >= 32)
        {
            return;
        }
        if (actual.EntryInputContract is not null && !scriptedInput.IsEntryInputSatisfied(actual.Entity, actual.Script))
        {
            return;
        }
        Ff7AutoPlayActiveEvent? adopted = AdoptEffectAlias(planned, expected, actual, admission, expectedEntry, actualEntry);
        if (adopted is null)
        {
            return;
        }
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> abandoned = State.Coverage.Visits.Single(visit => visit.Visit.Key == planned.VisitId).Events.Single(item => item.Event.Id == planned.EventId);
        if (abandoned.Conditions?.ActiveEntry == planned.ConditionFingerprint)
        {
            abandoned.Conditions.ActiveEntry = null;
        }
        State.Active = adopted;
        Publish();
    }

    internal static Ff7AutoPlayActiveEvent? AdoptEffectAlias(Ff7AutoPlayActiveEvent planned, Ff7AutoPlayTarget expected, Ff7AutoPlayTarget actual, FieldExternalScriptAdmission admission, int expectedEntry, int actualEntry)
    {
        if (planned.Accepted || planned.Returned || planned.TransitionOwned || planned.CompletionRecorded || !admission.Accepted || !admission.Started || admission.Priority != 1 || planned.EventId != expected.Id || planned.ConditionFingerprint != expected.ConditionFingerprint || planned.Entity != expected.Entity || expected.Entity != actual.Entity || admission.Entity != actual.Entity || admission.Script != actual.Script || expectedEntry != actualEntry || expected.EffectFingerprint is null || expected.EffectFingerprint != actual.EffectFingerprint || !expected.AnalysisComplete || !actual.AnalysisComplete || !HasCompatibleEffectAliasInput(expected, actual))
        {
            return null;
        }
        return planned with { EventId = actual.Id, Script = actual.Script, Kind = actual.Kind, ConditionFingerprint = actual.ConditionFingerprint, NavigationProbeKey = null, EntryAvailabilityFingerprint = null, TravelRoute = null, TravelSelections = null, ExecutionProvenance = null };
    }

    internal static bool HasCompatibleEffectAliasInput(Ff7AutoPlayTarget expected, Ff7AutoPlayTarget actual)
    {
        return expected.EntryInputContract is null && actual.EntryInputContract is null || expected.EntryInputContract is not null && expected.EntryInputContract == actual.EntryInputContract && expected.EffectFingerprint?.StartsWith("effect-entry-input-dialogue:1:", StringComparison.Ordinal) == true;
    }

    internal static bool HasConsumedEffectAlias(Ff7AutoPlayState state, string visitId, AutoPlayEvent observed)
    {
        if (observed.EffectFingerprint is null || observed.ConditionSignature is null)
        {
            return false;
        }
        AutoPlayVisitCoverage<Ff7AutoPlayDeferral>? visit = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == visitId);
        if (visit is null || visit.Events.SingleOrDefault(item => item.Event.Id == observed.Id)?.Conditions?.HasExecutedEntry(observed.ConditionSignature) == true)
        {
            return false;
        }
        if (observed.EffectFingerprint.StartsWith("native-field-transition:", StringComparison.Ordinal))
        {
            if (!Ff7AutoPlayDiscovery.TryReadDirectTransitionEffect(observed.EffectFingerprint, out int map))
            {
                return false;
            }
            string destination = "ff7:" + map.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":";
            return visit.Events.Any(item => item.Event.Id != observed.Id && item.Completed && item.Event.EffectFingerprint == observed.EffectFingerprint && item.Event.ConditionSignature is string condition && item.Conditions?.HasExecutedEntry(condition) == true && item.Routes.Any(route => route.ConditionSignature == condition && route.TargetId == item.Event.Id && route.DestinationVisitKey.StartsWith(destination, StringComparison.Ordinal) && route.ObservedStoryChanged == false && (route.Selections is null || route.Selections.Count == 0)));
        }
        return visit.Events.Any(item => item.Event.Id != observed.Id && item.Completed && item.Event.EffectFingerprint == observed.EffectFingerprint && item.Event.ConditionSignature is string condition && item.Conditions?.HasExecutedEntry(condition) == true && (item.ObservedConditionSignature == condition || item.Routes.Any(route => route.ConditionSignature == condition)));
    }

    private void RetireConsumedEffectAliasGoal()
    {
        if (State.NavigationGoal is not { EventId: string eventId } goal)
        {
            return;
        }
        AutoPlayNavigationVisit? navigation = State.Coverage.NavigationVisits.SingleOrDefault(visit => visit.Key == goal.NavigationKey);
        AutoPlayEvent? target = navigation?.Events.SingleOrDefault(target => target.Id == eventId);
        if (navigation is not null && target is not null && (goal.Conditions is null || goal.Conditions == target.ConditionSignature) && HasConsumedEffectAlias(State, navigation.VisitKey, target))
        {
            State.NavigationGoal = null;
        }
    }
}
