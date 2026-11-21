using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class NativeNavigationPrerequisiteTests
{
    private sealed record Fixture(Ff7AutoPlayState State, Ff7AutoPlayObservation Observation, Ff7AutoPlayTarget Goal, Ff7AutoPlayTarget Candidate, Ff7Actor Actor);

    internal static void Run()
    {
        Fixture valid = Create();
        string before = JsonSerializer.Serialize(valid);
        Check(Eligible(valid), "A genuinely executed navigation interaction at the actual blocking actor was excluded.");
        Check(before == JsonSerializer.Serialize(valid), "Prerequisite qualification mutated source or coverage.");

        foreach (Func<Fixture, Fixture> invalidate in new Func<Fixture, Fixture>[]
        {
            fixture => fixture with { Observation = fixture.Observation with { VisitId = "other" } },
            fixture => fixture with { Observation = fixture.Observation with { NavigationKey = "other" } },
            fixture => fixture with { Observation = fixture.Observation with { Generation = 2 } },
            fixture => fixture with { Goal = fixture.Goal with { ConditionFingerprint = "changed" } },
            fixture => fixture with { Candidate = fixture.Candidate with { ConditionFingerprint = "changed" } },
            fixture => fixture with { Candidate = fixture.Candidate with { MayChangeNavigation = false } },
            fixture => fixture with { Candidate = fixture.Candidate with { AnalysisComplete = false } },
            fixture => fixture with { Candidate = fixture.Candidate with { ChangesStory = true } },
            fixture => fixture with { Candidate = fixture.Candidate with { ChangesField = true } },
            fixture => fixture with { Candidate = fixture.Candidate with { Kind = Ff7AutoPlayTargetKind.ActorContact } },
            fixture => fixture with { Actor = fixture.Actor with { Entity = 99 } },
            fixture => fixture with { Actor = fixture.Actor with { EntityBound = false } },
            fixture => fixture with { Actor = fixture.Actor with { PlayerControlled = true } },
            fixture => fixture with { Actor = fixture.Actor with { TalkEnabled = false } },
            fixture => fixture with { Actor = fixture.Actor with { CollisionEnabled = false } },
            fixture => fixture with { Actor = fixture.Actor with { CollisionRadius = 0 } }
        })
        {
            Fixture invalid = invalidate(Create());
            invalid = invalid with { Observation = invalid.Observation with { Events = [invalid.Goal, invalid.Candidate] } };
            Check(!Eligible(invalid), "An unrelated, stale, unsupported or nonblocking interaction became a prerequisite.");
        }
        Check(!Eligible(valid, []), "An actor not reported by native movement became a blocker.");
        Check(!Ff7AutoPlayPolicy.IsKnownBlockingNavigationActor(valid.State, valid.Observation, valid.Goal, valid.Candidate, null, [valid.Actor.Entity]), "A missing actual actor became a prerequisite.");

        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            fixture => fixture.State.Active = fixture.State.Active! with { Accepted = true },
            fixture => fixture.State.TravelCommitment = null,
            fixture => fixture.State.NavigationGoal = new("remote", "different", "fresh"),
            fixture => fixture.State.NativeRoute = new("goal", "goal-condition", 1, [], "after"),
            fixture => Coverage(fixture).Conditions = null,
            fixture => Coverage(fixture).Conditions!.ConsumedEntries.Clear(),
            fixture => Coverage(fixture).Available = false,
            fixture => Coverage(fixture).BlockedReason = "blocked",
            fixture => Coverage(fixture).PolicyDeferred = true,
            fixture => Coverage(fixture).Event = Coverage(fixture).Event with { ConditionSignature = "changed" }
        })
        {
            Fixture invalid = Create();
            invalidate(invalid);
            Check(!Eligible(invalid), "An unexecuted, unavailable or no-longer-owned prerequisite was reused.");
        }
        CheckCompletionMemo();
        Console.WriteLine("Known native navigation blocker qualification and source/owner/condition fences passed.");
    }

    private static void CheckCompletionMemo()
    {
        Fixture fixture = Create();
        Ff7AutoPlayActiveEvent prerequisite = new("visit", fixture.Candidate.Id, fixture.Candidate.ConditionFingerprint, 1, fixture.Candidate.Entity, fixture.Candidate.Script, fixture.Candidate.Kind, null, 0, "before", NavigationKey: "local") { NavigationPrerequisiteGoal = fixture.State.NavigationGoal };
        Ff7AutoPlayPolicy.RecordNavigationPrerequisiteCompletion(fixture.State, prerequisite, fixture.Actor);
        Check(Eligible(fixture), "An unaccepted prerequisite fabricated a completion memo.");
        Ff7AutoPlayPolicy.RecordNavigationPrerequisiteCompletion(fixture.State, prerequisite with { Accepted = true }, fixture.Actor);
        Check(Eligible(fixture), "An outstanding native prerequisite fabricated a return memo.");
        Ff7AutoPlayPolicy.RecordNavigationPrerequisiteCompletion(fixture.State, prerequisite with { Accepted = true, Returned = true }, fixture.Actor);
        Check(!Eligible(fixture), "A genuinely returned prerequisite was repeated without any collider or intent change.");
        Check(Eligible(fixture with { Actor = fixture.Actor with { X4096 = 4096 } }), "A changed native collider reused an old completion memo.");
        Check(Eligible(fixture with { Actor = fixture.Actor with { CollisionRadius = 31 } }), "A changed native collision radius reused an old completion memo.");
        AutoPlayTravelGoal refreshed = fixture.State.NavigationGoal! with { Conditions = "fresh-again" };
        var remote = fixture.State.Coverage.NavigationVisits.Single(visit => visit.Key == "remote");
        fixture.State.Coverage.NavigationVisits[fixture.State.Coverage.NavigationVisits.IndexOf(remote)] = remote with { Events = remote.Events.Select(item => item with { ConditionSignature = refreshed.Conditions }).ToArray() };
        fixture.State.NavigationGoal = refreshed;
        fixture.State.TravelCommitment = new(refreshed, "local", null);
        Check(Eligible(fixture), "A different unfinished ultimate condition reused an old prerequisite memo.");
    }

    private static Fixture Create()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent goal = new("goal", AutoPlayEventKind.Transition, ConditionSignature: "goal-condition");
        AutoPlayEvent actor = new("actor", AutoPlayEventKind.Interaction, ConditionSignature: "actor-condition");
        AutoPlayEvent remote = new("remote-event", AutoPlayEventKind.Interaction, ConditionSignature: "fresh");
        planner.Observe(new("visit", "program"), [goal, actor, remote]);
        planner.ObserveNavigation("visit", "local", [goal, actor]);
        planner.ObserveNavigation("visit", "remote", [remote]);
        planner.Completed("visit", actor.Id, "visit", observedStoryChanged: false, entrySignature: actor.ConditionSignature, entrySignatureAuthoritative: true, targetId: actor.Id);
        state.Active = new("visit", goal.Id, goal.ConditionSignature!, 1, 3, 2, Ff7AutoPlayTargetKind.LineTouch, null, 0, "before", NavigationKey: "local");
        state.NavigationGoal = new("remote", remote.Id, remote.ConditionSignature);
        state.TravelCommitment = new(state.NavigationGoal, "local", null);
        Ff7AutoPlayTarget target = new(goal.Id, 3, 2, Ff7AutoPlayTargetKind.LineTouch, goal.ConditionSignature!, false, default);
        Ff7AutoPlayTarget candidate = new(actor.Id, 12, 1, Ff7AutoPlayTargetKind.ActorTalk, actor.ConditionSignature!, false, default, MayChangeNavigation: true);
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [target, candidate], true, true) { NavigationKey = "local" };
        return new(state, observation, target, candidate, new() { Slot = 4, Entity = 12, Model = 0, CollisionRadius = 30 });
    }

    private static AutoPlayEventCoverage<Ff7AutoPlayDeferral> Coverage(Fixture fixture)
    {
        return fixture.State.Coverage.Visits.Single().Events.Single(item => item.Event.Id == "actor");
    }

    private static bool Eligible(Fixture fixture, IReadOnlyCollection<int>? pushed = null)
    {
        return Ff7AutoPlayPolicy.IsKnownBlockingNavigationActor(fixture.State, fixture.Observation, fixture.Goal, fixture.Candidate, fixture.Actor, pushed ?? [fixture.Actor.Entity]);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
