namespace Memoria.FF8;

internal static class DialogueBlink
{
    // Continuous wall-clock modulation keeps blinking independent of the field simulation rate.
    public static float Opacity(double seconds) => 0.55f + 0.45f * (float)((Math.Cos(seconds * Math.Tau) + 1) / 2);
}
