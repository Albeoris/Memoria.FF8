using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class ActiveNativePlanningTests
{
    internal static void Run()
    {
        CheckFairPositive();
        CheckNegativeAndCancellation();
        CheckOwnerAndGoalAuthentication();
        CheckDistinctNativeCycleSamples();
        Console.WriteLine("Active physical/native continuation fairness checks passed.");
    }

    private static (Ff7AutoPlayState State, Ff7AutoPlayActiveEvent Active, Ff7AutoPlayObservation Observation, Ff7AutoPlayNativeRoute Proof) Fixture()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent item = new("line", AutoPlayEventKind.Interaction, ConditionSignature: "condition");
        planner.Observe(new("visit", "region"), [item]);
        planner.ObserveNavigation("visit", "nav", [item]);
        Ff7AutoPlayActiveEvent active = new("visit", "line", "condition", 1, 2, 3, Ff7AutoPlayTargetKind.LineCross, null, 1, "witness", NavigationKey: "nav");
        state.Active = active;
        state.NavigationGoal = new("nav", "line", "condition");
        Ff7AutoPlayTarget target = new("line", 2, 3, Ff7AutoPlayTargetKind.LineCross, "condition", false, default);
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [target], true, true) { NavigationKey = "nav" };
        Ff7AutoPlayNativeRoute proof = new("line", "condition", 1, [new(new('A', 64), new(1, 0))], new('B', 64));
        return (state, active, observation, proof);
    }

    private static void CheckFairPositive()
    {
        var fixture = Fixture();
        var state = fixture.State;
        string before = JsonSerializer.Serialize(state);
        string intent = Ff7AutoPlayPolicy.ReadNativePlanningIntent(state);
        int nativeWork = 0;
        int physicalWork = 0;
        bool disposed = false;
        IEnumerable<AutoPlayPlanStep?> Search()
        {
            try
            {
                for (int step = 0; step < 4; step++)
                {
                    nativeWork++;
                    yield return null;
                }
                Check(Ff7AutoPlayPolicy.TryInstallActiveNativeRoute(state, fixture.Active, state.NavigationGoal, fixture.Observation, fixture.Proof, new('A', 64)), "The complete same-owner native witness was not installed.");
            }
            finally
            {
                disposed = true;
            }
        }
        using Ff7NativePlanningCursor cursor = new(Search().GetEnumerator());
        bool nativeTurn = true;
        while (!cursor.Completed)
        {
            if (Ff7AutoPlayPolicy.TakeNativePlanningTurn(true, ref nativeTurn))
            {
                cursor.Resume(new(1, TimeSpan.MaxValue));
            }
            else
            {
                physicalWork++;
                state.Status = "Physical search is still pending";
                Check(intent == Ff7AutoPlayPolicy.ReadNativePlanningIntent(state) && intent == Ff7AutoPlayPolicy.ReadNativePlanningIntent(state.Clone()), "A same-source observational publication cancelled the retained native job.");
                Check(state.NativeRoute is null && ReferenceEquals(state.Active, fixture.Active), "Pending work replaced the selected native owner.");
            }
        }
        Check(nativeWork == 4 && physicalWork == 4 && disposed && state.NativeRoute == fixture.Proof, "Physical work or complete native proof was starved across planning turns.");
        state.NativeRoute = null;
        state.Status = null;
        Check(before == JsonSerializer.Serialize(state), "A successful continuation changed coverage, guards, entry availability or the selected goal/owner.");
    }

    private static void CheckNegativeAndCancellation()
    {
        int nativeWork = 0;
        int physicalWork = 0;
        bool disposed = false;
        IEnumerable<AutoPlayPlanStep?> Negative()
        {
            try
            {
                nativeWork++;
                yield return null;
                nativeWork++;
            }
            finally
            {
                disposed = true;
            }
        }
        using Ff7NativePlanningCursor cursor = new(Negative().GetEnumerator());
        bool nativeTurn = true;
        while (!cursor.Completed)
        {
            if (Ff7AutoPlayPolicy.TakeNativePlanningTurn(true, ref nativeTurn))
            {
                cursor.Resume(new(1, TimeSpan.MaxValue));
            }
            else
            {
                physicalWork++;
            }
        }
        physicalWork++;
        Check(nativeWork == 2 && physicalWork == 2 && disposed && cursor.Result is null, "A terminal negative native search starved the ordinary physical continuation.");
        disposed = false;
        using (Ff7NativePlanningCursor cancelled = new(Negative().GetEnumerator()))
        {
            cancelled.Resume(new(1, TimeSpan.MaxValue));
        }
        Check(disposed, "Cancelling a pending active proof retained its detached search.");
    }

    private static void CheckOwnerAndGoalAuthentication()
    {
        var fixture = Fixture();
        foreach (Ff7AutoPlayActiveEvent replacement in new[] { fixture.Active with { Entity = 3 }, fixture.Active with { Script = 4 }, fixture.Active with { Generation = 2 }, fixture.Active with { ConditionFingerprint = "changed" }, fixture.Active with { Kind = Ff7AutoPlayTargetKind.LineEnter }, fixture.Active with { NavigationKey = "other" }, fixture.Active with { Accepted = true } })
        {
            var state = Fixture().State;
            state.Active = replacement;
            Check(!Ff7AutoPlayPolicy.TryInstallActiveNativeRoute(state, fixture.Active, fixture.State.NavigationGoal, fixture.Observation, fixture.Proof, new('A', 64)) && state.NativeRoute is null, "A changed native owner accepted another approach's proof.");
        }
        foreach (Ff7AutoPlayNativeRoute proof in new[] { fixture.Proof with { EventId = "other" }, fixture.Proof with { ConditionFingerprint = "other" }, fixture.Proof with { Generation = 2 }, fixture.Proof with { Cursor = 1 }, fixture.Proof with { Steps = [] } })
        {
            Check(!Ff7AutoPlayPolicy.TryInstallActiveNativeRoute(fixture.State, fixture.Active, fixture.State.NavigationGoal, fixture.Observation, proof, new('A', 64)), "An incompatible native prefix was installed.");
        }
        Check(!Ff7AutoPlayPolicy.TryInstallActiveNativeRoute(fixture.State, fixture.Active, new("elsewhere"), fixture.Observation, fixture.Proof, new('A', 64)), "A replaced original goal accepted the old proof.");
        Check(!Ff7AutoPlayPolicy.TryInstallActiveNativeRoute(fixture.State, fixture.Active, fixture.State.NavigationGoal, fixture.Observation, fixture.Proof, new('C', 64)), "A divergent exact native source accepted a stale proof.");
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(fixture.State);
        planner.Completed("visit", "line", entrySignature: "condition", entrySignatureAuthoritative: true);
        Check(!Ff7AutoPlayPolicy.TryInstallActiveNativeRoute(fixture.State, fixture.Active, fixture.State.NavigationGoal, fixture.Observation, fixture.Proof, new('A', 64)), "A consumed self-goal was manufactured by a positive physical capability.");
    }

    private static void CheckDistinctNativeCycleSamples()
    {
        Queue<(int X, int Y, int Z, int Triangle)> positions = new();
        Ff7Actor actor = new() { Slot = 0, Entity = 0, Model = 0, Triangle = 1 };
        long previous = -1;
        for (int pending = 0; pending < 1000; pending++)
        {
            Check(!Ff7AutoPlayPolicy.ObserveMovementCycle(positions, actor, 1, ref previous), "Planning-only decisions fabricated a native movement cycle.");
        }
        Check(positions.Count == 1, "Repeated planning sampled the same native pose more than once.");
        for (int tick = 2; tick < 96; tick++)
        {
            Check(!Ff7AutoPlayPolicy.ObserveMovementCycle(positions, actor, tick, ref previous), "A real movement cycle was declared before its existing native sample count.");
        }
        Check(Ff7AutoPlayPolicy.ObserveMovementCycle(positions, actor, 96, ref previous), "A genuine repeated pose across96native callbacks escaped the unchanged cycle detector.");
    }

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
}
