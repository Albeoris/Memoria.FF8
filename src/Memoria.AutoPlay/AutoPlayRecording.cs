namespace Memoria.AutoPlay;

/// <summary>Bounds the common recent-action recording without retaining per-frame capture archives.</summary>
public static class AutoPlayRecording
{
    public static string LimitActionText(string action)
    {
        return action.Length > 512 ? action[..512] : action;
    }

    public static void AppendRecent<T>(List<T> actions, T action, int capacity = 32)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        actions.Add(action);
        if (actions.Count > capacity)
        {
            actions.RemoveRange(0, actions.Count - capacity);
        }
    }
}
