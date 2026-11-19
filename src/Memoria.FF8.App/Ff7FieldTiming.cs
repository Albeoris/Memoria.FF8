using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private SceneKind TimingScene => Session!.World is not null ? SceneKind.WorldMap : Session.Host.Process.Movie is { Playing: true } ? SceneKind.Movie : SceneKind.Field;

    private bool RequiresRealTimeFieldInput() => Session!.RequiresRealTimeFieldInput;
}

internal static class Ff7FieldTiming
{
    public const int CallbacksPerSecond = 30;

    public static void Advance(GameplayClock clock, TimeSpan elapsed, SceneKind scene, Func<bool> requiresRealTimeInput, Func<bool> advanceCallback, ref double remainder)
    {
        const double callbackDuration = 1d / CallbacksPerSecond;
        TimeSpan simulationElapsed = clock.Advance(elapsed, scene, scriptedInterface: requiresRealTimeInput());
        remainder += simulationElapsed.TotalSeconds;
        while (remainder + 1e-9 >= callbackDuration)
        {
            remainder = Math.Max(0, remainder - callbackDuration);
            bool keepAdvancing = advanceCallback();
            if (simulationElapsed > elapsed && requiresRealTimeInput())
            {
                // The next host update samples held input before running any more ladder callbacks.
                remainder = 0;
                return;
            }
            if (!keepAdvancing)
            {
                return;
            }
        }
    }
}
