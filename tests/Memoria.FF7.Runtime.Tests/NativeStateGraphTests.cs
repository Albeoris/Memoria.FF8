using System.Text.Json;
using Memoria.FF7.Runtime;

internal static class NativeStateGraphTests
{
    internal static void Run()
    {
        CheckRetainedWaitAndDetour();
        CheckLastSafePrefixAndTrace();
        CheckExactIdentity();
        CheckPositiveCostAndReopening();
        CheckCancellationOwnership();
        Console.WriteLine("Native state graph checks passed.");
    }

    private sealed record Snapshot(string Place, long Tick = 0, int Progress = 0, uint Rng = 1, int Debt = 0, string Owner = "free");
    private sealed record Step(string Before, string Input, string After);

    private static string Key(Snapshot state)
    {
        return JsonSerializer.Serialize(state);
    }

    private static Step Callback(Snapshot before, string input, Snapshot after)
    {
        return new(Key(before), input, Key(after));
    }

    private static Ff7NativeGraphNode<Snapshot, Step> Begin(Ff7NativeStateGraph<Snapshot, Step> graph)
    {
        Check(graph.TryBeginExpansion(out var node) && node?.State is not null, "A retained native snapshot disappeared before expansion.");
        return node!;
    }

    private static void CheckRetainedWaitAndDetour()
    {
        Snapshot source = new("start");
        using Ff7NativeStateGraph<Snapshot, Step> graph = new(source, Key(source));
        List<string> expanded = [];
        Ff7NativeGraphNode<Snapshot, Step>? winner = null;
        while (graph.TryBeginExpansion(out var node))
        {
            var current = node!;
            Snapshot state = current.State!;
            expanded.Add(state.Place);
            void Add(Snapshot next, string input, float advisory)
            {
                graph.AddSuccessor(current, next, Key(next), [Callback(state, input, next)], advisory);
            }
            switch (state.Place)
            {
                case "start":
                    Add(new("direct-hazard", 1), "east", 0);
                    Add(new("early-detour-hazard", 1), "north", 1);
                    Add(new("waited", 1), "wait", 2);
                    break;
                case "waited":
                    Add(new("detour", 2), "north", 1);
                    break;
                case "detour":
                    Add(new("goal", 3), "east", 0);
                    break;
                case "goal":
                    winner = current;
                    break;
            }
            graph.CompleteExpansion(current);
            if (winner is not null)
            {
                break;
            }
        }
        Check(expanded.IndexOf("direct-hazard") < expanded.IndexOf("waited"), "The authored direct greedy branch was not actually explored before the delayed alternative.");
        Check(expanded.Contains("early-detour-hazard") && winner is not null, "Failed immediate branches discarded the retained WAIT and detour route.");
        Step[] trace = graph.ReadTrace(winner!);
        Check(trace.Select(step => step.Input).SequenceEqual(["wait", "north", "east"]) && winner!.Cost == 3, "A winning retained route inherited a failed sibling's input history or cost.");
        CheckTrace(source, trace, winner!.Key);
    }

    private static void CheckLastSafePrefixAndTrace()
    {
        Snapshot source = new("start");
        Snapshot safe1 = new("corridor", 1);
        Snapshot safe2 = new("junction", 2);
        Snapshot hazard = new("unselected-owner", 3, Owner: "incidental");
        using Ff7NativeStateGraph<Snapshot, Step> graph = new(source, Key(source));
        var root = Begin(graph);
        List<Step> prefix = [];
        Snapshot lastSafe = source;
        foreach (var transition in new[] { (State: safe1, Hazard: false), (State: safe2, Hazard: false), (State: hazard, Hazard: true) })
        {
            if (transition.Hazard)
            {
                break;
            }
            prefix.Add(Callback(lastSafe, "east", transition.State));
            lastSafe = transition.State;
        }
        var junction = graph.AddSuccessor(root, lastSafe, Key(lastSafe), prefix)!;
        prefix[0] = Callback(source, "corrupted-caller-buffer", hazard);
        graph.CompleteExpansion(root);
        Check(root.State is null && ReferenceEquals(junction.State, safe2), "Completing a parent retained its save or released its unexpanded child.");
        Check(graph.VisitedCount == 2 && junction.Cost == 2, "The failed macro discarded its last safe prefix or admitted the hazardous endpoint.");
        Check(ReferenceEquals(Begin(graph), junction), "The last safe macro prefix was not available for branching.");
        Snapshot goal = new("goal", 3);
        var winner = graph.AddSuccessor(junction, goal, Key(goal), [Callback(safe2, "north", goal)])!;
        graph.CompleteExpansion(junction);
        Step[] trace = graph.ReadTrace(winner);
        Check(junction.State is null && trace.Select(step => step.Input).SequenceEqual(["east", "east", "north"]), "Released parent snapshots or a mutated input buffer corrupted the immutable parent trace.");
        CheckTrace(source, trace, winner.Key);
    }

    private static void CheckExactIdentity()
    {
        Snapshot source = new("root");
        Snapshot baseline = new("same-pose", 1, 1, 7, 1, "root");
        Snapshot[] distinct = [baseline, baseline with { Tick = 2 }, baseline with { Progress = 2 }, baseline with { Rng = 8 }, baseline with { Debt = 2 }, baseline with { Owner = "root>child:returned" }];
        using Ff7NativeStateGraph<Snapshot, Step> graph = new(source, Key(source));
        var root = Begin(graph);
        foreach (Snapshot state in distinct)
        {
            Check(graph.AddSuccessor(root, state, Key(state), [Callback(source, "wait", state)]) is not null, "Different native clock, progress, RNG, debt or owner facts were merged by the graph.");
        }
        Snapshot exactDuplicate = baseline with { };
        Check(graph.AddSuccessor(root, exactDuplicate, Key(exactDuplicate), [Callback(source, "wait", exactDuplicate)]) is null, "An identical supplied native-and-owner identity was duplicated at equal cost.");
        Check(graph.FrontierCount == distinct.Length && graph.VisitedCount == distinct.Length + 1, "The exact-key frontier lost distinct native futures.");
        graph.CompleteExpansion(root);
    }

    private static void CheckPositiveCostAndReopening()
    {
        Snapshot source = new("start");
        Snapshot middle = new("middle", 1);
        Snapshot shared = new("shared", 3);
        using Ff7NativeStateGraph<Snapshot, Step> graph = new(source, Key(source));
        var root = Begin(graph);
        Throws<ArgumentException>(() => graph.AddSuccessor(root, source, Key(source), []), "A zero-callback edge entered the graph.");
        var costly = graph.AddSuccessor(root, shared, Key(shared), [Callback(source, "east", middle), Callback(middle, "wait", middle), Callback(middle, "east", shared)])!;
        graph.AddSuccessor(root, middle, Key(middle), [Callback(source, "north", middle)]);
        graph.CompleteExpansion(root);
        var shortParent = Begin(graph);
        Check(shortParent.Key == Key(middle), "Positive callback cost did not rank the cheaper parent first.");
        var reopened = graph.AddSuccessor(shortParent, shared, Key(shared), [Callback(middle, "east", shared)])!;
        graph.CompleteExpansion(shortParent);
        Check(ReferenceEquals(Begin(graph), reopened) && reopened.Cost == 2, "A cheaper exact-state path failed to reopen its state.");
        graph.CompleteExpansion(reopened);
        Check(!graph.TryBeginExpansion(out _) && costly.State is null, "A stale costly queue entry was expanded or retained its snapshot after reopening.");
        CheckTrace(source, graph.ReadTrace(reopened), reopened.Key);
    }

    private static void CheckCancellationOwnership()
    {
        Snapshot source = new("start");
        Snapshot child = new("queued", 1);
        Ff7NativeStateGraph<Snapshot, Step> graph = new(source, Key(source));
        var active = Begin(graph);
        var queued = graph.AddSuccessor(active, child, Key(child), [Callback(source, "wait", child)])!;
        Throws<InvalidOperationException>(() => graph.TryBeginExpansion(out _), "Two native snapshots were opened for expansion simultaneously.");
        graph.Dispose();
        Check(active.State is null && queued.State is null && graph.FrontierCount == 0 && graph.VisitedCount == 0, "Cancellation retained an active or queued native snapshot.");
        Throws<ObjectDisposedException>(() => graph.TryBeginExpansion(out _), "A cancelled graph resumed native expansion.");
        Check(graph.ReadTrace(queued).Length == 1, "Releasing snapshot ownership damaged an already authenticated parent trace.");
        graph.Dispose();
    }

    private static void CheckTrace(Snapshot source, IReadOnlyList<Step> steps, string expectedAfter)
    {
        string previous = Key(source);
        foreach (Step step in steps)
        {
            Check(step.Before == previous, "A parent trace crossed native states without a contiguous authenticated callback.");
            previous = step.After;
        }
        Check(previous == expectedAfter, "A parent trace ended at a different native state.");
    }

    private static void Throws<T>(Action action, string reason) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException(reason);
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
