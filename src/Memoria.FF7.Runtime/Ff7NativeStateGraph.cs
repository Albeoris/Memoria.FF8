namespace Memoria.FF7.Runtime;

internal sealed class Ff7NativeGraphTrace<TStep>(Ff7NativeGraphTrace<TStep>? parent, TStep[] steps, long cost)
{
    internal Ff7NativeGraphTrace<TStep>? Parent { get; } = parent;
    internal TStep[] Steps { get; } = steps;
    internal long Cost { get; } = cost;
}

internal sealed class Ff7NativeGraphNode<TState, TStep>(TState state, string key, Ff7NativeGraphTrace<TStep> trace) where TState : class
{
    internal TState? State { get; set; } = state;
    internal string Key { get; } = key;
    internal long Cost => Trace.Cost;
    internal Ff7NativeGraphTrace<TStep> Trace { get; } = trace;
}

/// <summary>Retained exact-state graph with positive native-callback edge costs.</summary>
internal sealed class Ff7NativeStateGraph<TState, TStep> : IDisposable where TState : class
{
    private readonly PriorityQueue<Ff7NativeGraphNode<TState, TStep>, (double Score, long Order)> frontier = new();
    private readonly Dictionary<string, long> costs = new(StringComparer.Ordinal);
    private Ff7NativeGraphNode<TState, TStep>? active;
    private long order;
    private bool disposed;

    internal Ff7NativeStateGraph(TState root, string exactKey, float advisory = 0)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrEmpty(exactKey);
        CheckAdvisory(advisory);
        Ff7NativeGraphNode<TState, TStep> node = new(root, exactKey, new(null, [], 0));
        costs.Add(exactKey, 0);
        frontier.Enqueue(node, (advisory, order++));
    }

    internal int FrontierCount => frontier.Count;
    internal int VisitedCount => costs.Count;
    internal long ExpandedCount { get; private set; }

    internal bool TryBeginExpansion(out Ff7NativeGraphNode<TState, TStep>? node)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (active is not null)
        {
            throw new InvalidOperationException("Complete the current native graph expansion before restoring another node.");
        }
        while (frontier.TryDequeue(out node, out _))
        {
            if (costs[node.Key] == node.Cost)
            {
                active = node;
                ExpandedCount++;
                return true;
            }
            node.State = null;
        }
        node = null;
        return false;
    }

    internal Ff7NativeGraphNode<TState, TStep>? AddSuccessor(Ff7NativeGraphNode<TState, TStep> parent, TState state, string exactKey, IReadOnlyList<TStep> segment, float advisory = 0)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrEmpty(exactKey);
        if (!ReferenceEquals(active, parent))
        {
            throw new InvalidOperationException("A native successor must belong to the current expansion.");
        }
        if (segment.Count == 0)
        {
            throw new ArgumentException("A native graph edge must execute at least one real callback.", nameof(segment));
        }
        CheckAdvisory(advisory);
        long cost = checked(parent.Cost + segment.Count);
        if (costs.TryGetValue(exactKey, out long known) && known <= cost)
        {
            return null;
        }
        Ff7NativeGraphNode<TState, TStep> child = new(state, exactKey, new(parent.Trace, segment.ToArray(), cost));
        costs[exactKey] = cost;
        frontier.Enqueue(child, (cost + advisory, order++));
        return child;
    }

    internal void CompleteExpansion(Ff7NativeGraphNode<TState, TStep> node)
    {
        if (!ReferenceEquals(active, node))
        {
            throw new InvalidOperationException("The completed native graph node is not the current expansion.");
        }
        node.State = null;
        active = null;
    }

    internal TStep[] ReadTrace(Ff7NativeGraphNode<TState, TStep> node)
    {
        Stack<TStep[]> segments = new();
        for (Ff7NativeGraphTrace<TStep>? trace = node.Trace; trace is not null; trace = trace.Parent)
        {
            segments.Push(trace.Steps);
        }
        return segments.SelectMany(segment => segment).ToArray();
    }

    private static void CheckAdvisory(float advisory)
    {
        if (!float.IsFinite(advisory) || advisory < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(advisory));
        }
    }

    public void Dispose()
    {
        if (active is not null)
        {
            active.State = null;
            active = null;
        }
        foreach (var item in frontier.UnorderedItems)
        {
            item.Element.State = null;
        }
        frontier.Clear();
        costs.Clear();
        disposed = true;
    }
}
