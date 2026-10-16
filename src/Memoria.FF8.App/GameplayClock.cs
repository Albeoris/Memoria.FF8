using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class GameplayClock
{
    public int SpeedMultiplier { get; private set; } = 1;
    public TimeSpan Total { get; private set; }

    public void ToggleSpeed()
    {
        SpeedMultiplier = SpeedMultiplier == 1 ? 3 : 1;
    }

    public TimeSpan Advance(TimeSpan elapsed, SceneKind scene, bool scriptedInterface = false)
    {
        // Movie callbacks follow decoded frames and real PCM playback together.
        // Scripted interfaces own their held-button repeat delays in field callbacks.
        int multiplier = !scriptedInterface && (scene is SceneKind.Field or SceneKind.WorldMap) ? SpeedMultiplier : 1;
        TimeSpan simulationElapsed = TimeSpan.FromTicks(checked(elapsed.Ticks * multiplier));
        Total += simulationElapsed;
        return simulationElapsed;
    }
}
