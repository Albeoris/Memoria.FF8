namespace Memoria.AutoPlay;

/// <summary>Captures immutable game state before asking a codec to atomically replace its standalone checkpoint.</summary>
public static class AutoPlayCheckpoint
{
    public static void Save<T>(string path, Func<T> capture, Action<string, T> save)
    {
        T snapshot = capture();
        save(path, snapshot);
    }
}
