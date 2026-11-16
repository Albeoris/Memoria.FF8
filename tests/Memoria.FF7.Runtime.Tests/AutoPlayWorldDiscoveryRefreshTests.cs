using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldDiscoveryRefreshTests
{
    public static void Run()
    {
        CheckSettledContext();
        CheckGlobalContext();
        CheckRefreshDiscoveryAdmission();
        Console.WriteLine("PASS settled world discovery context, typed global invalidation and remote refresh admission.");
    }

    private static void CheckSettledContext()
    {
        Ff7AutoPlayState state = new();
        state.WorldVisits["world"] = new()
        {
            DiscoveryComplete = true,
            DiscoveryContextFingerprint = "settled-context",
            GlobalFingerprint = "settled-global",
            ObservedChunks = [new(2, 3)],
            PhysicalGuards = ["physical"],
            ObservedPhysicalGuards = new() { ["physical"] = "observed" },
            SourceProgressEntries = ["consumed-source"]
        };
        state.WorldVisits["other-world"] = new() { DiscoveryComplete = true, DiscoveryContextFingerprint = "other-context" };
        Ff7AutoPlayWorldVisitState visited = state.WorldVisits["world"];
        string before = JsonSerializer.Serialize(state);
        Check(!Ff7AutoPlayPolicy.ObserveWorldDiscoveryContext(visited, "transient-context", ready: false, globalFingerprint: "transient-global"), "An unready world allocation admitted a transient discovery context.");
        Check(JsonSerializer.Serialize(state) == before, "Unready discovery changed stable catalog completeness, global dependencies or recorded native evidence.");
        Check(!Ff7AutoPlayPolicy.ObserveWorldDiscoveryContext(visited, "settled-context", ready: true, globalFingerprint: "settled-global") && visited.DiscoveryComplete, "Settled reentry invalidated an unchanged complete world catalog.");
        Check(JsonSerializer.Serialize(state) == before, "Stable ready discovery changed recorded source facts.");
        Check(Ff7AutoPlayPolicy.ObserveWorldDiscoveryContext(visited, "changed-model-context", ready: true, globalFingerprint: "settled-global") && !visited.DiscoveryComplete && visited.DiscoveryContextFingerprint == "changed-model-context", "A real settled model/topology change retained an obsolete complete world catalog.");
        Check(visited.GlobalFingerprint == "settled-global" && visited.ObservedChunks.SequenceEqual([new Ff7AutoPlayWorldChunk(2, 3)]) && visited.PhysicalGuards.SetEquals(["physical"]) && visited.ObservedPhysicalGuards["physical"] == "observed" && visited.SourceProgressEntries.SetEquals(["consumed-source"]), "Context invalidation altered native source observations, execution receipts or collected global predicates.");
        Check(state.WorldVisits["other-world"].DiscoveryComplete && state.WorldVisits["other-world"].DiscoveryContextFingerprint == "other-context", "One world's settled context invalidated another world's catalog.");
        Check(!state.Clone().WorldVisits["world"].DiscoveryComplete, "Cold restoration discarded the pending real discovery refresh.");
    }

    private static void CheckGlobalContext()
    {
        Ff7AutoPlayWorldVisitState visited = new() { DiscoveryComplete = true, DiscoveryContextFingerprint = "context", GlobalFingerprint = "collected-global" };
        Check(Ff7AutoPlayPolicy.ObserveWorldDiscoveryContext(visited, "context", globalFingerprint: "changed-global") && !visited.DiscoveryComplete, "Changed known global predicates did not invalidate the settled world catalog.");
        Check(visited.GlobalFingerprint == "collected-global", "Discovery context admission overwrote typed global facts before their catalog reads were collected.");
        visited.DiscoveryComplete = true;
        Check(!Ff7AutoPlayPolicy.ObserveWorldDiscoveryContext(visited, "context") && visited.DiscoveryComplete, "An absent optional current global fingerprint fabricated a global change.");
        Ff7AutoPlayWorldVisitState legacy = new() { DiscoveryComplete = true, DiscoveryContextFingerprint = "context" };
        Check(!Ff7AutoPlayPolicy.ObserveWorldDiscoveryContext(legacy, "context", globalFingerprint: "newly-collected") && legacy.DiscoveryComplete && legacy.GlobalFingerprint is null, "Legacy absent global metadata was treated as a known changed predicate or eagerly rewritten.");
        Check(Ff7AutoPlayPolicy.ObserveWorldDiscoveryContext(new(), "first-ready-context"), "The optional default-ready API failed to admit a first settled context.");
    }

    private static void CheckRefreshDiscoveryAdmission()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        AutoPlayEvent local = new("unselected-local", AutoPlayEventKind.Interaction, ConditionSignature: "unconsumed");
        AutoPlayEvent exit = new("travel-exit", AutoPlayEventKind.Transition, ConditionSignature: "travel");
        planner.Observe(new("world", "world-program"), [local, exit]);
        state.WorldVisits["world"] = new() { ObservedChunks = [new(0, 0)] };
        AutoPlayPlanStep refresh = new("world", exit, true, new("foreign", "travel", false, []), RefreshVisitKey: "foreign-navigation", Goal: new("foreign-navigation"));
        string before = JsonSerializer.Serialize(state);
        Check(Ff7AutoPlayPolicy.ShouldDiscoverWorldChunk(state, "world", refresh, hasQueuedChunks: true), "A remote refresh departed an incomplete local catalog because a different unselected local candidate existed.");
        Check(!Ff7AutoPlayPolicy.ShouldDiscoverWorldChunk(state, "world", refresh, hasQueuedChunks: false), "A remote refresh invented queued discovery after its catalog queue was exhausted.");
        Check(!Ff7AutoPlayPolicy.ShouldDiscoverWorldChunk(state, "world", new("world", local, false), hasQueuedChunks: true), "Catalog completion blocked the actual selected local native interaction.");
        Check(!Ff7AutoPlayPolicy.ShouldDiscoverWorldChunk(state, "world", refresh with { RefreshVisitKey = null }, hasQueuedChunks: true), "Ordinary event travel lost the existing local native frontier admission rule.");
        Check(JsonSerializer.Serialize(state) == before, "Reading discovery admission changed coverage, receipts or another source's catalog state.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
