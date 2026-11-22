using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class NativeSourceGoalTests
{
    internal static void Run()
    {
        Ff7AutoPlayTarget target = new("actor:1:script:1", 1, 1, Ff7AutoPlayTargetKind.ActorTalk, "condition", false, default);
        Ff7AutoPlayObservation observation = new("visit", "program", 3, [target], true, true) { NavigationKey = "component" };
        Ff7AutoPlaySourceGoal goal = Ff7AutoPlayPolicy.CreateSourceGoal(observation, target, 20);
        Check(Ff7AutoPlayPolicy.MatchesSourceGoal(goal, observation, target), "An exact currently observed source goal failed to match.");
        Check(Ff7AutoPlayPolicy.MatchesSourceGoal(Ff7AutoPlayPolicy.CreateSourceGoal(observation with { AnalysisComplete = false, Events = [target, target with { Id = "unrelated", Entity = 2, AnalysisComplete = false }] }, target, 20), observation, target), "An unrelated incomplete field target prevented exact complete-source selection.");
        foreach (Ff7AutoPlayObservation invalid in new[] { observation with { CanAcceptInput = false }, observation with { Events = [] } })
        {
            Reject(() => Ff7AutoPlayPolicy.CreateSourceGoal(invalid, target, 20));
        }
        foreach (Ff7AutoPlayTarget invalid in new[] { target with { Kind = Ff7AutoPlayTargetKind.Gateway }, target with { Entity = -1 }, target with { Script = 32 }, target with { AnalysisComplete = false } })
        {
            Reject(() => Ff7AutoPlayPolicy.CreateSourceGoal(observation with { Events = [invalid] }, invalid, 20));
        }
        Reject(() => Ff7AutoPlayPolicy.CreateSourceGoal(observation, target, -1));
        foreach (Ff7AutoPlayObservation invalid in new[]
        {
            observation with { VisitId = "other" },
            observation with { ProgramIdentity = "other" },
            observation with { Generation = 4 },
            observation with { NavigationKey = "other" },
            observation with { Events = [] }
        })
        {
            Check(!Ff7AutoPlayPolicy.MatchesSourceGoal(goal, invalid, target), "A foreign or absent native source inherited the exact goal.");
        }
        foreach (Ff7AutoPlayTarget invalid in new[]
        {
            target with { Id = "other" },
            target with { Entity = 2 },
            target with { Script = 2 },
            target with { Kind = Ff7AutoPlayTargetKind.LineTalk },
            target with { ConditionFingerprint = "other" }
        })
        {
            Check(!Ff7AutoPlayPolicy.MatchesSourceGoal(goal, observation with { Events = [invalid] }, invalid), "A changed target or condition inherited exact-source authority.");
        }
        Ff7AutoPlayActiveEvent active = new("visit", target.Id, "condition", 3, 1, 1, target.Kind, null, 0, "before", Accepted: true, Returned: true, NavigationKey: "component", CompletionRecorded: true);
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, "program", 20, 5) with { RootReturned = true } };
        Ff7AutoPlayState state = new() { SourceGoal = goal };
        string history = JsonSerializer.Serialize(state.Coverage);
        Check(Ff7AutoPlayPolicy.CompleteSourceGoal(state, active), "Actual source completion did not report its first completion.");
        Check(state.SourceGoal == goal with { Completed = true } && JsonSerializer.Serialize(state.Coverage) == history, "Actual source completion changed coverage or failed to complete its goal.");
        string complete = JsonSerializer.Serialize(state);
        Check(!Ff7AutoPlayPolicy.CompleteSourceGoal(state, active), "Repeated source completion reported new native work.");
        Check(complete == JsonSerializer.Serialize(state), "Repeated source completion changed accounting.");
        Ff7AutoPlayState retiredRoot = new() { SourceGoal = goal with { AcceptedEntry = 20 } };
        Check(Ff7AutoPlayPolicy.CompleteSourceGoal(retiredRoot, active with { ExecutionProvenance = active.ExecutionProvenance! with { Owners = [] } }), "A genuine recorded root entry was lost after its native owner returned.");
        Ff7AutoPlayState wrongAcceptedEntry = new() { SourceGoal = goal with { AcceptedEntry = 21 } };
        Check(!Ff7AutoPlayPolicy.CompleteSourceGoal(wrongAcceptedEntry, active), "A different admitted native entry completed the requested source.");
        foreach (Ff7AutoPlayActiveEvent invalid in new[]
        {
            active with { Accepted = false },
            active with { Returned = false },
            active with { CompletionRecorded = false },
            active with { Generation = 4 },
            active with { VisitId = "other" },
            active with { NavigationKey = "other" },
            active with { EventId = "other" },
            active with { ConditionFingerprint = "other" },
            active with { ExecutionProvenance = null },
            active with { ExecutionProvenance = active.ExecutionProvenance! with { RootReturned = false } },
            active with { ExecutionProvenance = active.ExecutionProvenance! with { ProgramIdentity = "other" } },
            active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, "program", 21, 5) with { RootReturned = true } }
        })
        {
            Ff7AutoPlayState pending = new() { SourceGoal = goal };
            string before = JsonSerializer.Serialize(new { pending, invalid });
            Check(!Ff7AutoPlayPolicy.CompleteSourceGoal(pending, invalid), "An unauthenticated native return reported completion.");
            Check(before == JsonSerializer.Serialize(new { pending, invalid }), "An unauthenticated return completed or mutated an exact source goal.");
        }
        Ff7AutoPlayState restored = JsonSerializer.Deserialize<Ff7AutoPlayState>(JsonSerializer.Serialize(state))!;
        Check(restored.SourceGoal == state.SourceGoal, "A cold restore lost exact source-goal completion.");
        Check(JsonSerializer.Deserialize<Ff7AutoPlayState>("{}")!.SourceGoal is null, "An old save acquired an invented source goal.");
        CheckConsumedSourceRearm();
        Console.WriteLine("Source goals bind exact observed identity and require actual authenticated completion without changing native history.");
    }

    private static void CheckConsumedSourceRearm()
    {
        Ff7AutoPlayState state = new();
        AutoPlayEvent source = new("requested-source", AutoPlayEventKind.Interaction, ConditionSignature: "exact-condition");
        AutoPlayEvent rival = new("fresh-competitor", AutoPlayEventKind.Interaction, ConditionSignature: "fresh-condition");
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage, candidateProgressionOrder: (_, target, _) => target.Id == rival.Id ? -1 : 0);
        planner.Observe(new("visit", "program"), [source, rival]);
        planner.ObserveNavigation("visit", "navigation", [source, rival]);
        planner.Completed("visit", source.Id, "visit", observedStoryChanged: false, entrySignature: source.ConditionSignature, entrySignatureAuthoritative: true, targetId: source.Id);
        var coverage = state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == source.Id);
        string History() => JsonSerializer.Serialize(new { coverage.Conditions, coverage.Routes, coverage.RetiredRoutes, state.Coverage.NavigationRoutes });
        string before = History();
        Check(coverage.Conditions!.ConsumedEntries.Contains(source.ConditionSignature!), "The rearm fixture did not start with a genuinely consumed source.");
        planner.MarkRouteNeedsRediscovery("visit", source.Id);
        AutoPlayTravelGoal goal = new("navigation", source.Id, source.ConditionSignature);
        state.SourceGoal = new("visit", "program", 1, "navigation", source.Id, source.ConditionSignature!, Ff7AutoPlayTargetKind.ActorTalk, 1, 1, 20);
        Check(planner.PlanNavigation("navigation", preferredGoal: goal)?.Event.Id == rival.Id, "The fixture did not preserve ordinary fresh-frontier competition.");
        AutoPlayPlanStep? exact = planner.PlanNavigation("navigation", preferredGoal: goal, strongPreferredGoal: true);
        Check(exact is { Event.Id: "requested-source" } && Ff7AutoPlayPolicy.IsSourceGoalPlan(state, exact), "A consumed exact source could not be selected for an explicit strong source goal.");
        AutoPlayPlanStep? fallback = planner.PlanNavigation("navigation", preferredGoal: goal, strongPreferredGoal: true, readAvailability: (_, target) => new(target.Id == source.Id, [], null));
        Check(fallback?.Event.Id == rival.Id && !Ff7AutoPlayPolicy.IsSourceGoalPlan(state, fallback), "An unavailable explicit source silently authorized an unrelated fresh fallback.");
        Check(Ff7AutoPlayPolicy.IsSourceGoalPlan(state, new("visit", source, false)), "An exact local source plan was rejected by the single-source fence.");
        Check(!Ff7AutoPlayPolicy.IsSourceGoalPlan(state, null) && !Ff7AutoPlayPolicy.IsSourceGoalPlan(state, new("visit", source with { ConditionSignature = "changed" }, false)), "An absent or stale-condition plan passed the single-source fence.");
        Check(coverage.RouteNeedsRediscovery && before == History(), "Source rearming or strong preference erased consumption or changed native route history.");
    }

    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }
        throw new InvalidOperationException("An unready, unsupported or absent native source acquired an exact goal.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
