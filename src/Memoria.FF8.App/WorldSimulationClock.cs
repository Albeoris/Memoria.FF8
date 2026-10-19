namespace Memoria.FF8;

/// <summary>Fixed native world callbacks and the fractional phase of their independent presentation.</summary>
internal sealed class WorldSimulationClock
{
    private const double StepSeconds = 1d / WorldMapScene.SimulationFps;
    private double accumulatedSeconds;

    public float Fraction => (float)Math.Clamp(accumulatedSeconds / StepSeconds, 0, 1);

    public void Accumulate(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        accumulatedSeconds += elapsed.TotalSeconds;
    }

    public bool TryStep()
    {
        if (accumulatedSeconds + 1e-9 < StepSeconds)
        {
            return false;
        }
        accumulatedSeconds = Math.Max(0, accumulatedSeconds - StepSeconds);
        return true;
    }

    public void Reset()
    {
        accumulatedSeconds = 0;
    }
}
