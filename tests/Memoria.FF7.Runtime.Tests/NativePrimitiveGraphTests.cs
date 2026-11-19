using System.Text.Json;
using Memoria.FF7.Runtime;

internal static class NativePrimitiveGraphTests
{
    private sealed record State(int X, int Y, int Tick, bool Run);
    private sealed record Step(string Before, Ff7AutoplayInput Input, string After);

    internal static void Run()
    {
        Ff7AutoplayInput[] inputs = Ff7AutoplayDriver.ReadNativeMovementInputs().ToArray();
        Check(inputs.Length == 16 && inputs.Distinct().Count() == 16 && inputs.All(input => input.Held == 0 && !input.Talk && (input.Horizontal != 0 || input.Vertical != 0)), "The native movement basis dropped or invented a directional action.");
        foreach (bool run in new[] { true, false })
        {
            Check(inputs.Where(input => input.Run == run).Select(input => (input.Horizontal, input.Vertical)).ToHashSet().SetEquals(new[] { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) }), "A native run/walk mode lost a legal axis direction.");
        }
        State source = new(0, 0, 0, true);
        using Ff7NativeStateGraph<State, Step> graph = new(source, Key(source));
        Ff7NativeGraphNode<State, Step>? winner = null;
        while (graph.TryBeginExpansion(out var candidate))
        {
            Ff7NativeGraphNode<State, Step> node = candidate!;
            State current = node.State!;
            if (current.X == 1 && current.Y == 0)
            {
                winner = node;
                graph.CompleteExpansion(node);
                break;
            }
            foreach (Ff7AutoplayInput input in inputs.Prepend(default))
            {
                State? next = Callback(current, input);
                if (next is not null)
                {
                    graph.AddSuccessor(node, next, Key(next), [new(Key(current), input, Key(next))]);
                }
            }
            graph.CompleteExpansion(node);
        }
        Check(winner is not null, "The retained graph could not choose a sideways move omitted by the direct goal guide.");
        Step[] trace = graph.ReadTrace(winner!);
        Check(trace.Length == 3 && trace[0].Input is { Horizontal: 0, Vertical: -1, Run: false } && trace[1].Input is { Horizontal: 1, Vertical: 0, Run: true } && trace[2].Input is { Horizontal: 0, Vertical: 1, Run: false }, "A within-triangle direction/mode change was deferred to an old macro endpoint.");
        Check(trace.Zip(trace.Skip(1)).All(pair => pair.First.After == pair.Second.Before), "One-callback successors lost contiguous native replay lineage.");
        Ff7NativePrimitiveReuse reuse = new();
        Ff7AutoplayInput safe = new(1, 0, Run: true);
        Ff7AutoplayInput unsafeContact = new(0, 1, Run: true);
        reuse.Remember(safe, [(11, 4), (13, 5)]);
        Check(reuse.CanSkip(safe, 12, 5) && !reuse.CanSkip(safe, 11, 4) && !reuse.CanSkip(safe, 13, 5), "Safe ordinary input was not reused exactly, or skipped a started/still-queued scoped owner.");
        Check(!reuse.CanSkip(unsafeContact, 12, 5) && !reuse.CanSkip(safe with { Run = false }, 12, 5) && !reuse.CanSkip(safe with { Held = 1 }, 12, 5), "Unsafe/unexecuted input or another mode/button borrowed ordinary safety.");
        Check(!new Ff7NativePrimitiveReuse().CanSkip(safe, 12, 5), "An ordinary callback's safety escaped its exact parent scope.");
        Console.WriteLine("Native primitive basis, sideways detour, per-callback direction/mode change and trace checks passed.");
    }

    private static State? Callback(State source, Ff7AutoplayInput input)
    {
        return (source.X, source.Y, input.Horizontal, input.Vertical, input.Run) switch
        {
            (0, 0, 0, -1, false) => new(0, -1, source.Tick + 1, false),
            (0, -1, 1, 0, true) => new(1, -1, source.Tick + 1, true),
            (1, -1, 0, 1, false) => new(1, 0, source.Tick + 1, false),
            (_, _, 0, 0, _) => source with { Tick = source.Tick + 1 },
            _ => null
        };
    }

    private static string Key(State state) => JsonSerializer.Serialize(state);

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
