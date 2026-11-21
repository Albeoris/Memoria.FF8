using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeBlockingLinePrerequisiteTests
{
    private sealed record Fixture(Ff7AutoPlayState State, Ff7AutoPlayObservation Observation, Ff7AutoPlayTarget Goal, Ff7AutoPlayTarget Candidate);

    internal static void Run()
    {
        Fixture valid = Create();
        string before = JsonSerializer.Serialize(valid);
        Check(Eligible(valid), "A fresh actual blocking LineTouch was excluded from a direct destination job without a travel commitment.");
        Check(before == JsonSerializer.Serialize(valid), "Qualification changed source state or coverage.");
        Check(!Eligible(valid, []), "An unrelated line became a navigation prerequisite.");
        foreach (Func<Fixture, Fixture> change in new Func<Fixture, Fixture>[]
        {
            fixture => fixture with { Candidate = fixture.Candidate with { ConditionFingerprint = "changed" } },
            fixture => fixture with { Candidate = fixture.Candidate with { Entity = 99 } },
            fixture => fixture with { Candidate = fixture.Candidate with { AnalysisComplete = false } },
            fixture => fixture with { Candidate = fixture.Candidate with { MayChangeNavigation = false } },
            fixture => fixture with { Candidate = fixture.Candidate with { ChangesField = true } },
            fixture => fixture with { Candidate = fixture.Candidate with { ChangesStory = true } },
            fixture => fixture with { Candidate = fixture.Candidate with { Kind = Ff7AutoPlayTargetKind.LineCross } },
            fixture => fixture with { Observation = fixture.Observation with { Generation = 2 } },
            fixture => fixture with { Observation = fixture.Observation with { NavigationKey = "other" } }
        })
        {
            Fixture invalid = change(Create());
            invalid = invalid with { Observation = invalid.Observation with { Events = [invalid.Goal, invalid.Candidate] } };
            Check(!Eligible(invalid), "A stale, unrelated or unsupported line acquired prerequisite eligibility.");
        }
        foreach (Action<Fixture> change in new Action<Fixture>[]
        {
            fixture => fixture.State.DestinationField = null,
            fixture => fixture.State.DestinationField = 601,
            fixture => fixture.State.NavigationGoal = null,
            fixture => Coverage(fixture).Available = false,
            fixture => Coverage(fixture).BlockedReason = "blocked",
            fixture => Coverage(fixture).PolicyDeferred = true,
            fixture => Ff7AutoPlayPolicy.CreatePlanner(fixture.State).Completed("visit", fixture.Candidate.Id, "visit", observedStoryChanged: false, entrySignature: fixture.Candidate.ConditionFingerprint, entrySignatureAuthoritative: true, targetId: fixture.Candidate.Id)
        })
        {
            Fixture invalid = Create();
            change(invalid);
            Check(!Eligible(invalid), "A consumed, unavailable or no-longer-requested line remained eligible.");
        }
        CheckAdmission(valid.Candidate);
        Ff7AutoPlayActiveEvent completed = new("visit", valid.Candidate.Id, valid.Candidate.ConditionFingerprint, 1, valid.Candidate.Entity, valid.Candidate.Script, valid.Candidate.Kind, null, 0, "before", NavigationKey: "local") { Accepted = true, Returned = true, NavigationPrerequisiteGoal = valid.State.NavigationGoal };
        Check(Ff7AutoPlayPolicy.IsOwnedNavigationPrerequisiteCompletion(valid.State, completed, valid.Observation), "A real completed direct-destination prerequisite lost its intent.");
        Check(!Ff7AutoPlayPolicy.IsOwnedNavigationPrerequisiteCompletion(valid.State, completed with { Returned = false }, valid.Observation), "An outstanding line fabricated a completion boundary.");
        Console.WriteLine("Native blocking line prerequisite checks passed.");
    }

    private static void CheckAdmission(Ff7AutoPlayTarget target)
    {
        FieldExternalScriptAdmission valid = new(target.Entity, target.Script, 1, true, true);
        Check(Ff7AutoplayDriver.IsStartedNavigationLinePrerequisite(target, valid, true), "An actual owned started line admission was rejected.");
        foreach (FieldExternalScriptAdmission invalid in new[] { valid with { Accepted = false }, valid with { Started = false }, valid with { Priority = 6 }, valid with { Entity = 99 }, valid with { Script = 99 } })
        {
            Check(!Ff7AutoplayDriver.IsStartedNavigationLinePrerequisite(target, invalid, true), "A queued, rejected or different admission authenticated a prerequisite.");
        }
        Check(!Ff7AutoplayDriver.IsStartedNavigationLinePrerequisite(target, valid, false), "An unowned admission authenticated a prerequisite.");
    }

    private static Fixture Create()
    {
        Ff7AutoPlayState state = new() { DestinationField = 600 };
        AutoPlayEvent goal = new("goal", AutoPlayEventKind.Transition, ConditionSignature: "goal-condition");
        AutoPlayEvent line = new("line", AutoPlayEventKind.Interaction, ConditionSignature: "line-condition");
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.Observe(new("visit", "program"), [goal, line]);
        planner.ObserveNavigation("visit", "local", [goal, line]);
        state.NavigationGoal = new("local", goal.Id, goal.ConditionSignature);
        state.Active = new("visit", goal.Id, goal.ConditionSignature!, 1, 3, 2, Ff7AutoPlayTargetKind.Gateway, 600, 0, "before", NavigationKey: "local");
        Ff7AutoPlayTarget target = new(goal.Id, 3, 2, Ff7AutoPlayTargetKind.Gateway, goal.ConditionSignature!, false, default, DestinationField: 600);
        Ff7AutoPlayTarget candidate = new(line.Id, 12, 4, Ff7AutoPlayTargetKind.LineTouch, line.ConditionSignature!, false, default, MayChangeNavigation: true);
        return new(state, new("visit", "program", 1, [target, candidate], true, true) { NavigationKey = "local" }, target, candidate);
    }

    private static AutoPlayEventCoverage<Ff7AutoPlayDeferral> Coverage(Fixture fixture)
    {
        return fixture.State.Coverage.Visits.Single().Events.Single(item => item.Event.Id == fixture.Candidate.Id);
    }

    private static bool Eligible(Fixture fixture, IReadOnlyCollection<int>? blockers = null)
    {
        return Ff7AutoPlayPolicy.IsCurrentBlockingNavigationLine(fixture.State, fixture.Observation, fixture.Goal, fixture.Candidate, blockers ?? [12]);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
