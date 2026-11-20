using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private readonly Dictionary<string, (string Context, Ff7AutoPlayTransportEntry? Entry)> transportEntries = new(StringComparer.Ordinal);

    internal static bool IsIntrinsicTransportGoal(Ff7AutoPlayState state, string visit, AutoPlayEvent target)
    {
        return !state.TransportRegions.TryGetValue(TransportKey(visit, target.Id), out Ff7AutoPlayTransportRegion? region) || region is null || region.Condition != target.ConditionSignature || region.Region is not { Length: 64 } || region.CompletedRegion != region.Region || region.CompletedCondition is not { Length: 64 } || string.IsNullOrEmpty(region.DestinationVisit) || !HasCompletedTransportOutcomes(state, region.ChoiceKey);
    }

    private static bool HasCompletedTransportOutcomes(Ff7AutoPlayState state, string? key)
    {
        AutoPlayChoiceCoverage? choice = key is null ? null : state.Coverage.Choices.SingleOrDefault(item => item.Key == key);
        return choice is { Finished: true, ActiveOption: null, OptionCount: > 0 } && choice.PolicyDeferredOptions.Count == 0 && Enumerable.Range(choice.FirstOption, choice.OptionCount).All(choice.CompletedOptions.Contains);
    }

    internal static bool IsReusableTransportRoute(Ff7AutoPlayState state, string visit, AutoPlayEvent target, AutoPlayEventRoute route)
    {
        return !IsIntrinsicTransportGoal(state, visit, target) && state.TransportRegions.TryGetValue(TransportKey(visit, target.Id), out Ff7AutoPlayTransportRegion? region) && route.ConditionSignature == region.CompletedCondition && route.TargetId == target.Id && route.ObservedStoryChanged == false && (route.DestinationVisitKey == region.DestinationVisit || route.DestinationVisitKey.StartsWith(region.DestinationVisit + ":nav:", StringComparison.Ordinal));
    }

    private static string TransportKey(string visit, string target) => visit + "|" + target;

    private void ObserveTransportRegions(Ff7AutoPlayObservation observation)
    {
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.AnalysisComplete && target.PossibleDestinationFields.Length > 0 && target.Kind is Ff7AutoPlayTargetKind.ActorTalk or Ff7AutoPlayTargetKind.ActorContact))
        {
            string key = TransportKey(observation.VisitId, target.Id);
            Ff7AutoPlayTransportRegion? learned = State.TransportRegions.GetValueOrDefault(key);
            string? completed = learned?.CompletedRegion;
            Ff7Actor? actor = session.Host.Process.Actors.SingleOrDefault(actor => actor.EntityBound && actor.Entity == target.Entity);
            string role = ReadTransportRole(actor, target);
            FieldVmSnapshot vm = session.Machine.Capture();
            string source = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            {
                Allocation = session.Machine.ReadScriptAnalysisAllocationFingerprint(),
                Entries = observation.Events.Select(item => new { item.Entity, item.Script, item.Kind }),
                Roots = vm.Entities.Select(item => new { item.ProgramCounter, item.Priority, item.SavedProgramCounters, item.SyncStates, item.SyncOwners }),
                vm.PendingRequests
            })));
            string context = $"{observation.Generation}:{target.ConditionFingerprint}:{role}:{source}";
            if (!transportEntries.TryGetValue(key, out var cached) || cached.Context != context)
            {
                Ff7AutoPlayTransportEntry? entry = ReadTransportEntry(session, observation, target, role);
                cached = (context, entry);
                transportEntries[key] = cached;
            }
            if (cached.Entry is not null || completed is not null)
            {
                State.TransportRegions[key] = new(target.ConditionFingerprint, cached.Entry?.Region ?? string.Empty, completed, learned?.CompletedCondition, learned?.DestinationVisit, learned?.ChoiceKey);
            }
        }
    }

    private Ff7AutoPlayTransportEntry? ReadCurrentTransportEntry(Ff7AutoPlayObservation observation, string id)
    {
        return transportEntries.TryGetValue(TransportKey(observation.VisitId, id), out var entry) ? entry.Entry : null;
    }

    private static string ReadTransportRole(Ff7Actor? actor, Ff7AutoPlayTarget target)
    {
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { target.Kind, actor?.Slot, actor?.Entity, actor?.Model, actor?.EntityBound, actor?.PlayerControlled, actor?.PartyCharacter, actor?.TalkEnabled, actor?.CollisionEnabled })));
    }

    private static Ff7AutoPlayTransportEntry? ReadTransportEntry(Ff7Session source, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target, string role)
    {
        if (!source.Host.Process.Actors.Any(actor => actor.EntityBound && !actor.PlayerControlled && actor.Entity == target.Entity) || target.EntryInputContract is not null)
        {
            return null;
        }
        FieldNavigationEntryPoint[] entries = observation.Events.Where(item => item.Entity >= 0 && item.Script >= 0).Select(item => new FieldNavigationEntryPoint(item.Entity, item.Script)).Distinct().ToArray();
        FieldTransportRegionProof proof = source.Machine.ProveTransportRegion(target.Entity, target.Script, source.Progress.Gil, entries);
        if (!proof.Complete || proof.Region is null || proof.Transition is null || proof.ChoiceSite?.Entity != target.Entity)
        {
            return null;
        }
        string region = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { proof.Region, Role = role })));
        return new(region, proof.Cost, source.Progress.Gil, proof.Transition.Map, proof.ChoiceSite.ProgramCounter);
    }

    internal static bool CanReobserveTransportChoice(Ff7Session source, Ff7AutoPlayState state, Ff7AutoPlayActiveEvent active, Ff7Dialogue dialogue)
    {
        if (source.World is not null || source.Battle is not null)
        {
            return false;
        }
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(source).Observe();
        Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(item => item.Id == active.EventId && item.Entity == active.Entity && item.Script == active.Script && item.ConditionFingerprint == active.ConditionFingerprint);
        if (target is null)
        {
            return false;
        }
        Ff7Actor? actor = source.Host.Process.Actors.SingleOrDefault(actor => actor.EntityBound && actor.Entity == target.Entity);
        Ff7AutoPlayTransportEntry? fresh = ReadTransportEntry(source, observation, target, ReadTransportRole(actor, target));
        return HasAuthenticatedTransportChoice(state, active, observation, source.Machine.Capture(), dialogue, source.Progress.Gil, fresh);
    }

    internal static bool HasAuthenticatedTransportChoice(Ff7AutoPlayState state, Ff7AutoPlayActiveEvent active, Ff7AutoPlayObservation observation, FieldVmSnapshot vm, Ff7Dialogue dialogue, uint gil, Ff7AutoPlayTransportEntry? fresh)
    {
        if (state.Active != active || state.Choice is not null || state.PendingRestore is not null || state.NativeRoute is not null || !active.Accepted || active.Returned || active.TransitionOwned || active.CompletionRecorded || active.Generation != observation.Generation || vm.ProgramIdentity != observation.ProgramIdentity || active.VisitId != observation.VisitId || active.NavigationKey != observation.NavigationKey || active.TravelRoute is not { } route || active.TravelSelections is { Count: > 0 } || active.TransportEntry is not { } admitted || fresh is not { ChoiceProgramCounter: int pc } || fresh.Region != admitted.Region || fresh.Cost != admitted.Cost || fresh.BeforeGil != gil || admitted.BeforeGil != gil || fresh.DestinationField != active.DestinationField || fresh.DestinationField != admitted.DestinationField || dialogue.Completed || !dialogue.ChoiceReady || dialogue.FirstChoice is null || dialogue.LastChoice is null || (uint)active.Entity >= vm.Entities.Length)
        {
            return false;
        }
        if (!observation.Events.Any(target => target.Id == active.EventId && target.Entity == active.Entity && target.Script == active.Script && target.Kind == active.Kind && target.AnalysisComplete && target.ConditionFingerprint == active.ConditionFingerprint))
        {
            return false;
        }
        FieldEntitySnapshot owner = vm.Entities[active.Entity];
        FieldHostRequest? request = (vm.PendingRequests ?? []).SingleOrDefault(item => item.Id == dialogue.RequestId);
        if (owner.Priority != 1 || owner.ProgramCounter != pc || request is not { Priority: 1, Command: FieldDialogueCommand { FirstChoice: not null } command } || request.Entity != active.Entity || request.ProgramCounter != pc || command.Window != dialogue.Window || command.Text != dialogue.Text || command.FirstChoice != dialogue.FirstChoice || command.LastChoice != dialogue.LastChoice)
        {
            return false;
        }
        if (!state.TransportRegions.TryGetValue(TransportKey(active.VisitId, active.EventId), out Ff7AutoPlayTransportRegion? learned) || learned is null || learned.Region != fresh.Region || learned.CompletedRegion != fresh.Region)
        {
            return false;
        }
        AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(item => item.Visit.Key == active.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == active.EventId);
        return coverage is { Completed: true } && coverage.Event.ConditionSignature == active.ConditionFingerprint && coverage.Conditions?.ActiveEntry == active.ConditionFingerprint && route.ConditionSignature is string historical && coverage.Conditions.HasExecutedEntry(historical) && IsReusableTransportRoute(state, active.VisitId, coverage.Event, route) && coverage.Routes.Any(recorded => recorded.ConditionSignature == route.ConditionSignature && recorded.DestinationVisitKey == route.DestinationVisitKey && recorded.TargetId == route.TargetId && recorded.ObservedStoryChanged == route.ObservedStoryChanged && (recorded.Selections is null ? route.Selections is null : route.Selections is not null && recorded.Selections.SequenceEqual(route.Selections)));
    }

    private void LearnTransportCompletion(Ff7AutoPlayActiveEvent active, Ff7AutoPlayObservation observation, bool changedField)
    {
        if (!changedField || !active.Accepted || !active.TransitionOwned || active.DestinationField != session.MapId || active.BeforeStory != Story())
        {
            return;
        }
        string key = TransportKey(active.VisitId, active.EventId);
        Ff7AutoPlayTransportRegion? observed = State.TransportRegions.GetValueOrDefault(key);
        if (observed is { CompletedRegion: not null } && observed.CompletedCondition == active.ConditionFingerprint && observed.DestinationVisit == observation.VisitId)
        {
            return;
        }
        Ff7AutoPlayTransportEntry? entry = active.TransportEntry ?? ReadRetainedTransportEntry(active);
        if (entry is null || entry.DestinationField != session.MapId || entry.Cost == 0 || entry.BeforeGil < session.Progress.Gil || entry.BeforeGil - session.Progress.Gil != entry.Cost)
        {
            return;
        }
        RecordTransportRegion(State, active, entry, observation.VisitId);
        Publish();
    }

    internal static void RecordTransportRegion(Ff7AutoPlayState state, Ff7AutoPlayActiveEvent active, Ff7AutoPlayTransportEntry entry, string destinationVisit)
    {
        string key = TransportKey(active.VisitId, active.EventId);
        Ff7AutoPlayTransportRegion? observed = state.TransportRegions.GetValueOrDefault(key);
        string? actualChoice = state.Choice is { BeforeActive: { } before } choice && before.VisitId == active.VisitId && before.EventId == active.EventId && before.Generation == active.Generation && before.ConditionFingerprint == active.ConditionFingerprint ? choice.Key : null;
        string? completedChoice = observed?.CompletedRegion == entry.Region && HasCompletedTransportOutcomes(state, observed.ChoiceKey) ? observed.ChoiceKey : actualChoice;
        state.TransportRegions[key] = new(observed?.Condition ?? active.ConditionFingerprint, observed?.Region ?? entry.Region, entry.Region, active.ConditionFingerprint, destinationVisit, completedChoice);
    }

    private Ff7AutoPlayTransportEntry? ReadRetainedTransportEntry(Ff7AutoPlayActiveEvent active)
    {
        if (State.Choice is not { BeforeActive: { Accepted: true } before } choice || before.Generation != active.Generation || before.VisitId != active.VisitId || before.EventId != active.EventId || before.ConditionFingerprint != active.ConditionFingerprint || State.Coverage.Choices.SingleOrDefault(item => item.Key == choice.Key)?.BeforeCheckpoint is not string path)
        {
            return null;
        }
        try
        {
            using FileStream input = File.OpenRead(path);
            Ff7SessionSave? saved = CombinedSessionCodec.Read(input).Ff7;
            if (saved is null || saved.FieldGeneration != active.Generation)
            {
                return null;
            }
            using Ff7Session source = session.ReadOnlyNavigationSnapshot(saved);
            Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(source).Observe();
            Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(target => target.Id == active.EventId && target.Entity == active.Entity && target.Script == active.Script && target.ConditionFingerprint == active.ConditionFingerprint);
            if (observation.VisitId != active.VisitId || target is null)
            {
                return null;
            }
            Ff7Actor? actor = source.Host.Process.Actors.SingleOrDefault(actor => actor.EntityBound && actor.Entity == target.Entity);
            return ReadTransportEntry(source, observation, target, ReadTransportRole(actor, target));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            return null;
        }
    }
}
