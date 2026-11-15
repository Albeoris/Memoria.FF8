using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeComponentProbeTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_COMPONENT_PROBE_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original component probe: configure resources and an external standalone learned-route fixture.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(stream);
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7AutoPlayState state = session.AutoPlayState!.Clone();
        Ff7AutoPlayActiveEvent active = state.Active ?? throw new InvalidDataException("The fixture requires an attributed source transition.");
        Check(active.Accepted && active.TransitionOwned && active.NavigationKey is not null && active.NavigationProbeKey is not null, "The fixture lacks an exact executed source-component proof.");
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = state.Coverage.Visits.Single(visit => visit.Visit.Key == active.VisitId).Events.Single(item => item.Event.Id == active.EventId);
        Check(coverage.Completed && coverage.Conditions!.ConsumedEntries.Contains(active.ConditionFingerprint) && coverage.Event.ConditionSignature == active.ConditionFingerprint, "The source entry signature is not stable and already consumed.");
        string probePrefix = $"{active.NavigationKey}|{active.EventId}|";
        Check(active.NavigationProbeKey!.StartsWith(probePrefix, StringComparison.Ordinal) && state.NavigationProbes.Contains(active.NavigationProbeKey), "The native source probe does not match its saved component and contact.");
        Ff7AutoPlayTarget target = new(active.EventId, active.Entity, active.Script, active.Kind, active.ConditionFingerprint, false, default, DestinationField: active.DestinationField, MayChangeNavigation: true, EntryInputContract: active.NavigationProbeKey![probePrefix.Length..], ChangesField: true);
        Ff7AutoPlayObservation observation = new(active.VisitId, state.Coverage.Visits.Single(visit => visit.Visit.Key == active.VisitId).Visit.Region, active.Generation, [target], true, true) { NavigationKey = active.NavigationKey! };
        string progressReceipts = JsonSerializer.Serialize(new { coverage.Completed, coverage.Conditions, coverage.Routes });
        Ff7AutoPlayPolicy.ObserveNavigationProbes(state, observation);
        Check(!state.Coverage.NavigationVisits.Single(visit => visit.Key == active.NavigationKey).RouteRediscovery[active.EventId], "An exact prior native probe did not normalize the legacy component cache.");
        Check(progressReceipts == JsonSerializer.Serialize(new { coverage.Completed, coverage.Conditions, coverage.Routes }), "Component observation changed executed conditions or historical routes.");
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        string navigationKey = active.NavigationKey ?? throw new InvalidDataException("The component fixture requires its recorded navigation key.");
        string topology = navigationKey[..(navigationKey.LastIndexOf(':') + 1)];
        AutoPlayPlanStep? step = planner.PlanNavigation(active.NavigationKey!, isCurrent: visit => visit.VisitKey != active.VisitId || visit.Key.StartsWith(topology, StringComparison.Ordinal), allowRemoteConditionRoutes: true);
        Check(step is { IsTravel: false } && step.Event.Id != active.EventId && !state.Coverage.Visits.Single(visit => visit.Visit.Key == active.VisitId).Events.Single(item => item.Event.Id == step.Event.Id).Completed, "Normal shared planning replayed a proved optional contact before an unconsumed native frontier.");
        Check(before == JsonSerializer.Serialize(session.Capture()), "Read-only component planning changed native PCs, progress, RNG or source metadata.");
        Ff7AutoPlayState cold = state.Clone();
        Check(Ff7AutoPlayPolicy.CreatePlanner(cold).PlanNavigation(active.NavigationKey!, isCurrent: visit => visit.VisitKey != active.VisitId || visit.Key.StartsWith(topology, StringComparison.Ordinal), allowRemoteConditionRoutes: true)?.Event.Id == step!.Event.Id, "Cold restoration lost the normalized component planning decision.");
        CheckNativeNavigationPublication(installation, envelope);
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Component observation changed its external standalone source.");
    }

    private static void CheckNativeNavigationPublication(Ff7Installation installation, CombinedSessionSave envelope)
    {
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        Ff7AutoPlayActiveEvent incoming = session.AutoPlayState!.Active!;
        int sourceMap = int.Parse(incoming.VisitId.Split(':')[1], System.Globalization.CultureInfo.InvariantCulture);
        int arrivalMap = session.MapId;
        long arrivalGeneration = session.FieldGeneration;
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Native entrance setup requested an unrelated choice checkpoint."));
        Ff7AutoplayDriver driver = new(session);
        bool retiredArrivalPhantom = false;
        bool returned = false;
        bool stoppedAtPhysicalBoundary = false;
        bool selectedUnconsumed = false;
        int unavailableObservations = 0;
        for (int callback = 0; callback < 512; callback++)
        {
            Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
            string navigationBefore = JsonSerializer.Serialize(policy.State.Coverage.NavigationVisits);
            string physicalBefore = PhysicalReceipts(policy.State);
            Ff7AutoplayInput input;
            try
            {
                input = policy.ChooseInput(driver);
            }
            catch (NotSupportedException error) when (error.Message == "No reachable unconsumed native event remains in the observed frontier.")
            {
                Check(returned && session.MapId == sourceMap && observation.CanAcceptInput, "Native initialization was mistaken for an exhausted physical frontier.");
                stoppedAtPhysicalBoundary = true;
                break;
            }
            if (!observation.CanAcceptInput)
            {
                unavailableObservations++;
                Check(navigationBefore == JsonSerializer.Serialize(policy.State.Coverage.NavigationVisits), "An input-unavailable native callback replaced valid component frontiers with transient scene facts.");
                Check(physicalBefore == PhysicalReceipts(policy.State), "An input-unavailable native callback published transient physical availability as an unconsumed frontier.");
            }
            else if (session.MapId == arrivalMap && observation.Events.All(target => target.Id != policy.State.NavigationGoal?.EventId))
            {
                AutoPlayNavigationVisit? navigation = policy.State.Coverage.NavigationVisits.SingleOrDefault(visit => visit.Key == observation.NavigationKey);
                Check(navigation is not null && navigation.Events.Select(item => item.Id).Order().SequenceEqual(observation.Events.Where(target => target.AnalysisComplete).Select(target => target.Id).Order()), "A valid native observation failed to retire obsolete arrival-component events.");
                retiredArrivalPhantom = true;
            }
            if (session.FieldGeneration != arrivalGeneration)
            {
                Check(session.MapId == sourceMap, "Normal doorway return entered an unrelated native field.");
                returned = true;
            }
            if (returned && policy.State.Active is { Accepted: false } next)
            {
                Check(next.EventId != incoming.EventId, "Normal policy replayed the proved optional doorway toward a transient arrival-component frontier.");
                selectedUnconsumed = !policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == next.VisitId).Events.Single(item => item.Event.Id == next.EventId).Completed;
                if (selectedUnconsumed)
                {
                    break;
                }
            }
            driver.Apply(input);
            session.AdvanceTick(advanceClock: false);
        }
        Check(unavailableObservations > 0 && retiredArrivalPhantom && returned && (stoppedAtPhysicalBoundary || selectedUnconsumed), "Normal native entrance and return did not bound the transient component frontier without a forced goal.");
    }

    private static string PhysicalReceipts(Ff7AutoPlayState state)
    {
        return JsonSerializer.Serialize(new
        {
            state.NavigationGuards,
            Events = state.Coverage.Visits.SelectMany(visit => visit.Events.Where(item => item.AvailabilityFingerprint is not null || item.Unavailable || item.UnavailableAttempts.Count > 0).Select(item => new { Visit = visit.Visit.Key, item.Event.Id, item.Unavailable, item.UnavailableVariants, item.AvailabilityFingerprint, item.UnavailableAttempts }))
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
