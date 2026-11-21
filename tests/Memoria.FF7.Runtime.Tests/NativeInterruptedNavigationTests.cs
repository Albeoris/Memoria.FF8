using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeInterruptedNavigationTests
{
    internal static void Run()
    {
        CheckReturnedSource();
        CheckHistoricalInterruption();
        Console.WriteLine("Returned-source generation and scoped interrupted local-navigation advice guards passed.");
    }

    private static void CheckReturnedSource()
    {
        Ff7AutoPlayActiveEvent active = new("visit", "line", "condition", 1, 4, 2, Ff7AutoPlayTargetKind.LinePush, null, 0, "before", Accepted: true, Returned: true, NavigationKey: "lower");
        string before = JsonSerializer.Serialize(active);
        Check(Ff7AutoPlayPolicy.CanCompleteReturnedSource(active, 1, false), "The actual same-generation returned source could not complete.");
        Check(!Ff7AutoPlayPolicy.CanCompleteReturnedSource(active, 2, false) && !Ff7AutoPlayPolicy.CanCompleteReturnedSource(active, 0, false), "A different field generation erased outstanding native source work.");
        Check(!Ff7AutoPlayPolicy.CanCompleteReturnedSource(active, 1, true), "A returned parent erased its actual outstanding child.");
        Check(!Ff7AutoPlayPolicy.CanCompleteReturnedSource(active with { Accepted = false }, 1, false) && !Ff7AutoPlayPolicy.CanCompleteReturnedSource(active with { Returned = false }, 1, false), "An unadmitted or unreturned source received native completion credit.");
        Check(!Ff7AutoPlayPolicy.CanCompleteReturnedSource(active with { TransitionOwned = true }, 2, false), "The returned-source arm substituted for separate authenticated transition accounting.");
        Check(before == JsonSerializer.Serialize(active), "Completion qualification changed native return or transition ownership.");
    }

    private static void CheckHistoricalInterruption()
    {
        const string visit = "ff7:100:program";
        const string foreign = "ff7:687:other-program";
        Ff7AutoPlayTarget producer = new("line", 4, 2, Ff7AutoPlayTargetKind.LinePush, "condition", false, default, MayChangeNavigation: true)
        {
            LadderLandings = [new(1, 10, new(320, 20, 0, 1, 1, 1, 0, 1), true)]
        };
        Ff7AutoPlayTarget gateway = new("gateway", -1, -1, Ff7AutoPlayTargetKind.Gateway, "gateway-condition", false, default, GatewayIndex: 0, DestinationField: 200, ChangesField: true);
        Ff7AutoPlayObservation observation = new(visit, "program", 1, [producer, gateway], true, true) { NavigationKey = "lower" };
        Ff7AutoPlayState state = new() { DestinationField = 600 };
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent[] events = [new(producer.Id, AutoPlayEventKind.Interaction, ConditionSignature: producer.ConditionFingerprint), new(gateway.Id, AutoPlayEventKind.Transition, ConditionSignature: gateway.ConditionFingerprint)];
        planner.Observe(new(visit, "program"), events);
        planner.Observe(new(foreign, "other-program"), []);
        planner.ObserveNavigation(visit, "lower", events);
        planner.ObserveNavigation(foreign, "foreign-nav", []);
        planner.Completed(visit, producer.Id, foreign, observedStoryChanged: false, entrySignature: producer.ConditionFingerprint, entrySignatureAuthoritative: true, targetId: producer.Id);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = state.Coverage.Visits.Single(item => item.Visit.Key == visit).Events.Single(item => item.Event.Id == producer.Id);
        Check(!Ff7AutoPlayPolicy.HasInterruptedLocalNavigationStep(state, observation, producer), "Completed metadata without an actual navigation receipt authorized rediscovery.");
        AutoPlayEventRoute historical = new(foreign, producer.ConditionFingerprint, false, [], producer.Id);
        coverage.Routes.Add(historical);
        planner.RecordNavigationRoute("lower", "foreign-nav", producer.Id, historical);
        string before = JsonSerializer.Serialize(state.Coverage);
        bool Interrupted(Ff7AutoPlayTarget candidate, Ff7AutoPlayObservation? current = null) => Ff7AutoPlayPolicy.HasInterruptedLocalNavigationStep(state, (current ?? observation) with { Events = [candidate, gateway] }, candidate);
        Check(Interrupted(producer), "An exact historic foreign interruption did not retain its current local producer advice.");
        Check(!Interrupted(producer, observation with { NavigationKey = "other" }), "A different current component inherited a historic interruption.");
        Check(!Interrupted(producer, observation with { VisitId = "ff7:101:program" }), "A different current field visit inherited the source navigation receipt.");
        Check(!Ff7AutoPlayPolicy.HasInterruptedLocalNavigationStep(state, observation with { Events = [gateway] }, producer), "An absent current producer was revived from history.");
        foreach (Ff7AutoPlayTarget invalid in new[]
        {
            producer with { ConditionFingerprint = "changed" },
            producer with { Id = "other" },
            producer with { AnalysisComplete = false },
            producer with { MayChangeNavigation = false },
            producer with { ChangesStory = true },
            producer with { ChangesField = true },
            producer with { Kind = Ff7AutoPlayTargetKind.ActorTalk },
            producer with { LadderLandings = [] }
        })
        {
            Check(!Interrupted(invalid), "Changed, incomplete, effectful or unsupported metadata revived a historical source.");
        }
        state.DestinationField = null;
        Check(!Interrupted(producer), "An interrupted local producer became intrinsic novelty without explicit travel intent.");
        state.DestinationField = 600;
        Check(before == JsonSerializer.Serialize(state.Coverage), "Interruption qualification erased routes, consumption or completion history.");

        PcWalkmeshTriangle[] mesh = [new(0, new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), -1, -1, -1), new(1, new(300, 0, 0), new(400, 0, 0), new(300, 100, 0), -1, -1, -1)];
        byte[] blocked = [0];
        Ff7Actor player = new() { Slot = 0, Entity = 1, Model = 0, Triangle = 0, X4096 = 20 << 12, Y4096 = 20 << 12, CollisionRadius = 10 };
        PcFieldGateway nativeGateway = new(new(320, 20, 0), new(330, 20, 0), 0, 0, 0, 200, 0, 0);
        Ff7Line[] lines = [new(0, 4, 20, 20, 0, 30, 20, 0)];
        bool CanSelect(Ff7AutoPlayTarget target) => target.Id == producer.Id;
        bool Establishes(Ff7AutoPlayTarget target, Ff7AutoPlayTarget selected) => selected.Id == gateway.Id && Ff7AutoPlayPolicy.CanEstablishFieldGatewayComponentRoute(mesh, blocked, player, nativeGateway, target, observation.Events, lines);
        string history = JsonSerializer.Serialize(new { coverage.Completed, coverage.Conditions, coverage.Routes, state.Coverage.NavigationRoutes });
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation, field => field == 200 ? [600] : [], CanSelect, Establishes);
        Check(state.NavigationGoal?.EventId == producer.Id && coverage.RouteNeedsRediscovery && coverage.GlobalRouteRediscovery, "Current geometry and native selection did not schedule the interrupted prerequisite for real rediscovery.");
        Check(history == JsonSerializer.Serialize(new { coverage.Completed, coverage.Conditions, coverage.Routes, state.Coverage.NavigationRoutes }), "Rediscovery rewrote historical routes or invented new native consumption.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
