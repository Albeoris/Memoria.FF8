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
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Component observation changed its external standalone source.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
