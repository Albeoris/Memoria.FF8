namespace Memoria.Battle;

internal static class BoundedBattleAdvance
{
    public static TResult Run<TCandidate, TResult>(TCandidate candidate, int maxSteps, Func<TCandidate, int, TResult?> advance, Action<TCandidate, TResult> publish, string timeoutMessage)
        where TResult : struct
    {
        for (int step = 1; step <= maxSteps; step++)
        {
            if (advance(candidate, step) is TResult result)
            {
                publish(candidate, result);
                return result;
            }
        }

        throw new TimeoutException(timeoutMessage);
    }
}
