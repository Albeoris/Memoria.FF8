using System.Diagnostics;

namespace Memoria.AutoPlay;

/// <summary>Runs the game adapter within the common interactive-host work budget.</summary>
public static class AutoPlayScheduler
{
    public static int Advance(int stepBudget, Func<bool> canAdvance, Action step, TimeSpan? wallBudget = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stepBudget, 1);
        TimeSpan budget = wallBudget ?? TimeSpan.FromMilliseconds(12);
        long started = Stopwatch.GetTimestamp();
        int advanced = 0;
        while (advanced < stepBudget && canAdvance())
        {
            step();
            advanced++;
            if (Stopwatch.GetElapsedTime(started) >= budget)
            {
                break;
            }
        }
        return advanced;
    }
}
