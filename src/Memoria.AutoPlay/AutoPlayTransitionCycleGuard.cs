namespace Memoria.AutoPlay;

public sealed class AutoPlayTransitionCycleGuard<T> where T : notnull
{
    private readonly Queue<T> history = new();

    public void Reset()
    {
        history.Clear();
    }

    public bool Observe(T state)
    {
        history.Enqueue(state);
        if (history.Count > 7)
        {
            history.Dequeue();
        }
        if (history.Count != 7)
        {
            return false;
        }
        T[] states = history.ToArray();
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        return !comparer.Equals(states[0], states[1])
            && Enumerable.Range(2, 5).All(index => comparer.Equals(states[index], states[index % 2]));
    }
}
