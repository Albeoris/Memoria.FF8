using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeIntermediatePushPrerequisiteTests
{
    private sealed record Fixture(Ff7AutoPlayState State, Ff7AutoPlayObservation Observation, Ff7AutoPlayTarget Goal, Ff7AutoPlayTarget Candidate);

    internal static void Run()
    {
        foreach (Ff7AutoPlayTargetKind kind in new[] { Ff7AutoPlayTargetKind.LineTouch, Ff7AutoPlayTargetKind.LinePush })
        {
            Fixture source = Create(kind);
            string before = JsonSerializer.Serialize(source);
            Check(!Eligible(source), "An unproved intermediate gateway acquired blocking-LINE prerequisite authority.");
            Check(Eligible(source, proved: true), "A current proved intermediate native hop lost its fresh actual blocking LINE.");
            Check(!Eligible(source, proved: true, blockers: []), "An unrelated LINE acquired prerequisite authority from the intermediate proof.");
            Fixture direct = Create(kind, direct: true);
            Check(Eligible(direct), "The existing direct destination prerequisite contract changed.");
            FieldExternalScriptAdmission admission = new(source.Candidate.Entity, source.Candidate.Script, 1, true, true);
            Check(Ff7AutoplayDriver.IsStartedNavigationLinePrerequisite(source.Candidate, admission, true), "A real owned started Touch/Push prerequisite was not admitted.");
            Check(!Ff7AutoplayDriver.IsStartedNavigationLinePrerequisite(source.Candidate, admission with { Started = false }, true) && !Ff7AutoplayDriver.IsStartedNavigationLinePrerequisite(source.Candidate, admission with { Priority = 6 }, true) && !Ff7AutoplayDriver.IsStartedNavigationLinePrerequisite(source.Candidate, admission, false), "An unstarted, wrong-priority or unowned dispatch fabricated prerequisite admission.");
            Check(before == JsonSerializer.Serialize(source), "Prerequisite qualification mutated native source identity or coverage.");
            foreach (Func<Fixture, Fixture> invalidate in new Func<Fixture, Fixture>[]
            {
                value => value with { Candidate = value.Candidate with { Kind = Ff7AutoPlayTargetKind.LineCross } },
                value => value with { Candidate = value.Candidate with { ConditionFingerprint = "changed" } },
                value => value with { Candidate = value.Candidate with { AnalysisComplete = false } },
                value => value with { Candidate = value.Candidate with { MayChangeNavigation = false } },
                value => value with { Candidate = value.Candidate with { ChangesStory = true } },
                value => value with { Candidate = value.Candidate with { ChangesField = true } },
                value => value with { Goal = value.Goal with { Kind = Ff7AutoPlayTargetKind.ActorTalk } },
                value => value with { Goal = value.Goal with { Id = "different-goal" } },
                value => value with { Goal = value.Goal with { ConditionFingerprint = "changed-goal" } },
                value => value with { Goal = value.Goal with { DestinationField = null } },
                value => value with { Goal = value.Goal with { GatewayIndex = null } },
                value => value with { Observation = value.Observation with { Generation = 2 } },
                value => value with { Observation = value.Observation with { NavigationKey = "foreign" } }
            })
            {
                Fixture invalid = invalidate(Create(kind));
                invalid = invalid with { Observation = invalid.Observation with { Events = [invalid.Goal, invalid.Candidate] } };
                Check(!Eligible(invalid, proved: true), "A changed source, target, guard or unsupported kind inherited an intermediate-hop proof.");
            }
            foreach (Action<Fixture> invalidate in new Action<Fixture>[]
            {
                value => value.State.NavigationGoal = new("local", "another", "goal-condition"),
                value => value.State.NavigationGoal = null,
                value => value.State.DestinationField = null,
                value => value.State.Active = value.State.Active! with { Accepted = true },
                value => value.State.Choice = new("choice", null),
                value => value.State.PendingRestore = new("before"),
                value => Coverage(value).Available = false,
                value => Ff7AutoPlayPolicy.CreatePlanner(value.State).Completed("visit", value.Candidate.Id, "visit", observedStoryChanged: false, entrySignature: value.Candidate.ConditionFingerprint, entrySignatureAuthoritative: true, targetId: value.Candidate.Id)
            })
            {
                Fixture invalid = Create(kind);
                invalidate(invalid);
                Check(!Eligible(invalid, proved: true), "Consumed or unavailable work, changed intent, or an existing choice/restore debt was ignored.");
            }
            Ff7AutoPlayActiveEvent completed = new("visit", source.Candidate.Id, source.Candidate.ConditionFingerprint, 1, source.Candidate.Entity, source.Candidate.Script, kind, null, 0, "before", NavigationKey: "local", Accepted: true, Returned: true) { NavigationPrerequisiteGoal = source.State.NavigationGoal };
            source.State.Active = completed;
            Check(Ff7AutoPlayPolicy.IsOwnedNavigationPrerequisiteCompletion(source.State, completed, source.Observation), "A real completed intermediate prerequisite lost its previously authorized intent.");
            Check(!Ff7AutoPlayPolicy.IsOwnedNavigationPrerequisiteCompletion(source.State, completed with { Returned = false }, source.Observation) && !Ff7AutoPlayPolicy.IsOwnedNavigationPrerequisiteCompletion(source.State, completed with { NavigationPrerequisiteGoal = null }, source.Observation), "An unreturned or unauthorized activation fabricated prerequisite completion.");
        }
        Console.WriteLine("Intermediate native gateway prerequisites require a proved current hop and preserve actual Touch/Push ownership.");
    }

    private static Fixture Create(Ff7AutoPlayTargetKind kind, bool direct = false)
    {
        Ff7AutoPlayState state = new() { DestinationField = 600 };
        AutoPlayEvent goal = new("gateway", AutoPlayEventKind.Transition, ConditionSignature: "goal-condition");
        AutoPlayEvent line = new("line", AutoPlayEventKind.Interaction, ConditionSignature: "line-condition");
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.Observe(new("visit", "program"), [goal, line]);
        planner.ObserveNavigation("visit", "local", [goal, line]);
        state.NavigationGoal = new("local", goal.Id, goal.ConditionSignature);
        ushort destination = direct ? (ushort)600 : (ushort)650;
        state.Active = new("visit", goal.Id, goal.ConditionSignature!, 1, -1, -1, Ff7AutoPlayTargetKind.Gateway, destination, 0, "before", NavigationKey: "local");
        Ff7AutoPlayTarget target = new(goal.Id, -1, -1, Ff7AutoPlayTargetKind.Gateway, goal.ConditionSignature!, false, default, GatewayIndex: 0, DestinationField: destination);
        Ff7AutoPlayTarget candidate = new(line.Id, 4, kind == Ff7AutoPlayTargetKind.LinePush ? 2 : 4, kind, line.ConditionSignature!, false, default, MayChangeNavigation: true);
        return new(state, new("visit", "program", 1, [target, candidate], true, true) { NavigationKey = "local" }, target, candidate);
    }

    private static AutoPlayEventCoverage<Ff7AutoPlayDeferral> Coverage(Fixture source) => source.State.Coverage.Visits.Single().Events.Single(item => item.Event.Id == source.Candidate.Id);
    private static bool Eligible(Fixture source, bool proved = false, IReadOnlyCollection<int>? blockers = null) => Ff7AutoPlayPolicy.IsCurrentBlockingNavigationLine(source.State, source.Observation, source.Goal, source.Candidate, blockers ?? [4], provedIntermediateHop: proved);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
