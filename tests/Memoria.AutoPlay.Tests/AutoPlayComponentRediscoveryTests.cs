using System.Text.Json;
using Memoria.AutoPlay;
using Deferral = AutoPlayPlannerTests.Deferral;

internal static class AutoPlayComponentRediscoveryTests
{
    public static void Run()
    {
        CheckChoiceIdentityConnectivity();
        AutoPlayPlanner<Deferral> planner = new();
        AutoPlayEvent door = new("door", AutoPlayEventKind.Interaction, Order: 0, ConditionSignature: "entry");
        AutoPlayEvent frontier = new("frontier", AutoPlayEventKind.Interaction, Order: 1, ConditionSignature: "fresh");
        planner.Observe(new("source", "resource"), [door, frontier]);
        planner.ObserveNavigation("source", "proven", [door, frontier]);
        planner.ObserveNavigation("source", "transient", [door, frontier]);
        planner.Observe(new("annex", "other-resource"), []);
        planner.ObserveNavigation("annex", "annex-nav", []);
        planner.Completed("source", door.Id, "annex", observedStoryChanged: false, selections: [new("expected", 1)], entrySignature: "entry", entrySignatureAuthoritative: true);
        AutoPlayEventCoverage<Deferral> coverage = planner.State.Visits.Single(visit => visit.Visit.Key == "source").Events.Single(item => item.Event.Id == door.Id);
        AutoPlayEventRoute route = coverage.Routes.Single();
        AutoPlayNavigationVisit proven = planner.State.NavigationVisits.Single(visit => visit.Key == "proven");
        AutoPlayNavigationVisit transient = planner.State.NavigationVisits.Single(visit => visit.Key == "transient");
        transient.RouteRediscovery[door.Id] = true;
        planner.RecordNavigationRoute("proven", "annex-nav", door.Id, route);
        Check(!proven.RouteRediscovery[door.Id] && transient.RouteRediscovery[door.Id], "Executing a proved source route cleared another component's pending observation.");
        coverage.RouteNeedsRediscovery = true;
        Check(planner.PlanNavigation("proven")?.Event.Id == frontier.Id, "A transient component's legacy shared flag reopened the already proved source.");
        Check(planner.PlanNavigation("transient")?.Event.Id == door.Id, "An unproved component lost its required connectivity execution.");
        planner.ObserveNavigation("source", "proven", [door, frontier]);
        Check(!planner.State.NavigationVisits.Single(visit => visit.Key == "proven").RouteRediscovery[door.Id], "Repeated native observation discarded a component receipt.");
        planner = new(JsonSerializer.Deserialize<AutoPlayPlannerState<Deferral>>(JsonSerializer.Serialize(planner.State))!);
        Check(planner.PlanNavigation("proven")?.Event.Id == frontier.Id && planner.PlanNavigation("transient")?.Event.Id == door.Id, "Cold restoration mixed proved and unproved component facts.");
        planner.MarkRouteNeedsRediscovery("source", door.Id);
        Check(planner.PlanNavigation("proven")?.Event.Id == door.Id, "An explicit global invalidation was suppressed by a component receipt.");
        planner = new(JsonSerializer.Deserialize<AutoPlayPlannerState<Deferral>>(JsonSerializer.Serialize(planner.State))!);
        Check(planner.PlanNavigation("proven")?.Event.Id == door.Id, "Cold restoration lost explicit global invalidation.");
        planner.Completed("source", door.Id, "annex", observedStoryChanged: false, selections: [new("expected", 1)], entrySignature: "entry", entrySignatureAuthoritative: true);
        planner.RecordNavigationRoute("proven", "annex-nav", door.Id, route);
        Check(planner.PlanNavigation("proven")?.Event.Id == frontier.Id, "Native completion failed to resolve explicit invalidation for its proved source.");
        planner.RetireTravelRoute("source", door.Id, route, "changed-prompt", 0, 2, []);
        Check(planner.PlanNavigation("proven")?.Event.Id == door.Id, "Retiring an authenticated travel route was hidden by a component receipt.");
        coverage = planner.State.Visits.Single(visit => visit.Visit.Key == "source").Events.Single(item => item.Event.Id == door.Id);
        coverage.GlobalRouteRediscovery = false;
        Check(planner.PlanNavigation("proven")?.Event.Id == door.Id, "An older explicit route-retirement receipt lost global invalidation when its new marker was absent.");
        AutoPlayNavigationVisit legacy = JsonSerializer.Deserialize<AutoPlayNavigationVisit>("{\"Key\":\"legacy\",\"VisitKey\":\"source\",\"Events\":[]}")!;
        Check(legacy.RouteRediscovery.Count == 0, "An older navigation save acquired invented component receipts.");
        planner.ObserveNavigation("source", "proven", [door with { ConditionSignature = "changed" }, frontier]);
        Check(!planner.State.NavigationVisits.Single(visit => visit.Key == "proven").RouteRediscovery.ContainsKey(door.Id), "A changed entry condition inherited an older component receipt.");
        planner.State.NavigationVisits.Single(visit => visit.Key == "proven").RouteRediscovery[door.Id] = false;
        planner.ObserveNavigation("source", "proven", [door with { ConditionSignature = "changed", Variants = ["another-native-entry"] }, frontier]);
        Check(!planner.State.NavigationVisits.Single(visit => visit.Key == "proven").RouteRediscovery.ContainsKey(door.Id), "A changed native variant contract inherited an older component receipt.");
    }

    private static void CheckChoiceIdentityConnectivity()
    {
        AutoPlayPlanner<Deferral> planner = new();
        AutoPlayEvent greeting = new("greeting", AutoPlayEventKind.Interaction, ConditionSignature: "entry");
        planner.Observe(new("source", "resource"), [greeting]);
        planner.ObserveNavigation("source", "current", [greeting]);
        planner.ObserveNavigation("source", "alternate", [greeting]);
        planner.ObserveNavigation("source", "historical", [greeting]);
        planner.Observe(new("annex", "other-resource"), []);
        planner.ObserveNavigation("annex", "annex-nav", []);
        AutoPlayEventRoute Complete(string destination, string choice, int option)
        {
            AutoPlayChoiceSelection[] selections = [new(choice, option)];
            planner.Completed("source", greeting.Id, destination, observedStoryChanged: false, selections: selections, entrySignature: "entry", entrySignatureAuthoritative: true, targetId: greeting.Id);
            return planner.State.Visits.Single(visit => visit.Visit.Key == "source").Events.Single().Routes.Single(route => route.DestinationVisitKey == destination && route.Selections!.SequenceEqual(selections));
        }
        AutoPlayEventRoute olderSelf = Complete("source", "older-choice", 2);
        planner.RecordNavigationRoute("historical", "historical", greeting.Id, olderSelf);
        AutoPlayEventRoute olderAlternate = Complete("source", "older-choice", 3);
        planner.RecordNavigationRoute("historical", "alternate", greeting.Id, olderAlternate);
        Check(planner.PlanNavigation("current")?.Event.Id == greeting.Id, "Historical completion suppressed an unproved source's connectivity probe.");
        AutoPlayEventRoute currentSelf = Complete("source", "current-choice", 2);
        planner.RecordNavigationRoute("current", "current", greeting.Id, currentSelf);
        AutoPlayEventRoute currentAlternate = Complete("source", "current-choice", 3);
        planner.RecordNavigationRoute("current", "alternate", greeting.Id, currentAlternate);
        planner.RecordNavigationRoute("alternate", "alternate", greeting.Id, currentSelf);
        planner.RecordNavigationRoute("alternate", "current", greeting.Id, currentAlternate);
        Check(planner.PlanNavigation("current") is null, "Historical choice identities repeatedly reopened already proved current connectivity.");
        planner = new(JsonSerializer.Deserialize<AutoPlayPlannerState<Deferral>>(JsonSerializer.Serialize(planner.State))!);
        Check(planner.PlanNavigation("current") is null, "Cold restoration reopened connectivity solely for an older choice identity.");
        AutoPlayEventRoute annex = Complete("annex", "older-choice", 4);
        planner.RecordNavigationRoute("historical", "annex-nav", greeting.Id, annex);
        Check(planner.PlanNavigation("current")?.Event.Id == greeting.Id, "A same-visit receipt suppressed a genuine missing connection to another destination.");
        AutoPlayEventRoute currentAnnex = Complete("annex", "current-choice", 4);
        planner.RecordNavigationRoute("current", "annex-nav", greeting.Id, currentAnnex);
        planner.RecordNavigationRoute("alternate", "annex-nav", greeting.Id, currentAnnex);
        Check(planner.PlanNavigation("current") is null, "A newly proved destination still required historical choice replay.");
        AutoPlayEvent changed = greeting with { ConditionSignature = "changed" };
        planner.Observe(new("source", "resource"), [changed]);
        planner.ObserveNavigation("source", "current", [changed]);
        Check(planner.PlanNavigation("current")?.Event.Id == greeting.Id, "A proved older predicate suppressed a genuinely unconsumed entry.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
