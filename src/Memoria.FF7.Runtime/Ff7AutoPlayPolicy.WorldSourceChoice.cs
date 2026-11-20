using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    internal static bool TryDetachReturnedWorldChoiceSource(Ff7AutoPlayState state, int sourceMap, long generation, Ff7WorldProcess world, FieldVmSnapshot source, Ff7FieldProcess process, Func<string, bool> checkpointExists)
    {
        if (!CanDetachReturnedWorldChoiceSource(state, sourceMap, generation, world, source, process, checkpointExists))
        {
            return false;
        }
        state.Active = null;
        return true;
    }

    internal static bool CanDetachReturnedWorldChoiceSource(Ff7AutoPlayState state, int sourceMap, long generation, Ff7WorldProcess world, FieldVmSnapshot source, Ff7FieldProcess process, Func<string, bool> checkpointExists)
    {
        if (state.Active is not { Accepted: true, Returned: true, TransitionOwned: false, TravelRoute: null, SpatialOwner: null, DirectEffectOwner: null, TransportEntry: null } active || active.Kind is < Ff7AutoPlayTargetKind.ActorTalk or > Ff7AutoPlayTargetKind.LineLeave || active.Generation != generation || active.VisitId != $"ff7:{sourceMap}:{source.ProgramIdentity}" || active.Script is < 0 or > 31 || (uint)active.Entity >= source.Entities.Length || state.Choice is not { } choice || state.PendingRestore is not null || state.NativeRoute is not null || active.TravelSelections is { Count: > 0 } || !HasCompletedFieldWorldHandoff(world, source, process))
        {
            return false;
        }
        FieldEntitySnapshot returned = source.Entities[active.Entity];
        if (returned.Priority != 7 || returned.SavedProgramCounters.Any(pc => pc != 0) || (source.PendingRequests ?? []).Any(request => request.Entity == active.Entity))
        {
            return false;
        }
        var coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == active.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == active.EventId);
        if (coverage?.Conditions is not { ActiveEntry: null } conditions || !conditions.HasExecutedEntry(active.ConditionFingerprint) || choice.ContinuationActive is not { Accepted: true, Returned: true, CompletionRecorded: true } continuation || !MatchesRoot(continuation))
        {
            return false;
        }
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (Ff7AutoPlayChoiceState? current = choice; current is not null; current = current.Parent)
        {
            if (!seen.Add(current.Key) || current.BeforeActive is not { Accepted: true, Returned: false } before || !MatchesRoot(before) || current.OwnerIdentity is not { Length: 64 } identity || !identity.All(Uri.IsHexDigit))
            {
                return false;
            }
            var branch = state.Coverage.Choices.SingleOrDefault(item => item.Key == current.Key);
            if (branch is null || branch.Finished || branch.ActiveOption is not int option || option < branch.FirstOption || (long)option >= (long)branch.FirstOption + branch.OptionCount || branch.CompletedOptions.Contains(option) || branch.PolicyDeferredOptions.Contains(option) || branch.BeforeCheckpoint is not string beforePath || !checkpointExists(beforePath))
            {
                return false;
            }
            if (ReferenceEquals(current, choice) && (branch.ContinuationCheckpoint is not string primary || !checkpointExists(primary)))
            {
                return false;
            }
        }
        return true;

        bool MatchesRoot(Ff7AutoPlayActiveEvent other)
        {
            return other.VisitId == active.VisitId && other.EventId == active.EventId && other.ConditionFingerprint == active.ConditionFingerprint && other.Generation == active.Generation && other.Entity == active.Entity && other.Script == active.Script && other.Kind == active.Kind && other.NavigationKey == active.NavigationKey && other.NavigationProbeKey == active.NavigationProbeKey && other.BeforeStory == active.BeforeStory && other.BeforeWitness == active.BeforeWitness && other.TravelRoute is null && other.SpatialOwner is null && other.DirectEffectOwner is null && other.TransportEntry is null;
        }
    }
}
