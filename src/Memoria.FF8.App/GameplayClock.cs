using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class GameplayClock
{
    private bool accelerated;
    private int fastForwardMultiplier = 3;
    private int battleSpeedPercent = 100;
    public int SpeedMultiplier => accelerated ? fastForwardMultiplier : 1;
    public TimeSpan Total { get; private set; }

    public void ToggleSpeed()
    {
        accelerated = !accelerated;
    }

    public void Configure(int fastForward, int battlePercent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fastForward, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fastForward, 10);
        ArgumentOutOfRangeException.ThrowIfLessThan(battlePercent, 50);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(battlePercent, 300);
        fastForwardMultiplier = fastForward;
        battleSpeedPercent = battlePercent;
    }

    public double EffectiveSpeed(SceneKind scene, bool scriptedInterface = false, bool battle = false)
    {
        if (battle)
        {
            return SpeedMultiplier * (battleSpeedPercent / 100d);
        }
        return !scriptedInterface && (scene is SceneKind.Field or SceneKind.WorldMap) ? SpeedMultiplier : 1;
    }

    public TimeSpan Advance(TimeSpan elapsed, SceneKind scene, bool scriptedInterface = false, bool battle = false)
    {
        // Movie callbacks follow decoded frames and real PCM playback together.
        // Scripted interfaces and controlled ladder phases sample held buttons at real time.
        double multiplier = EffectiveSpeed(scene, scriptedInterface, battle);
        TimeSpan simulationElapsed = elapsed * multiplier;
        Total += simulationElapsed;
        return simulationElapsed;
    }
}
