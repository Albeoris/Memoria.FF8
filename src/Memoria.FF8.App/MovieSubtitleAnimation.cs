using Memoria.FF8.Media;

namespace Memoria.FF8;

internal readonly record struct MovieSubtitleAnimation(float Opacity, float Reveal, float OffsetX, float OffsetY, float ScaleX, float ScaleY, float Blur, float Scatter, float Noise, float Brightness, float SplitX, float SplitY, float Ripple)
{
    public static MovieSubtitleAnimation At(IReadOnlyList<MovieSubtitleKeyframe> keyframes, double seconds)
    {
        return new(
            Sample(keyframes, seconds, 1, frame => frame.Opacity),
            Sample(keyframes, seconds, 1, frame => frame.Reveal),
            Sample(keyframes, seconds, 0, frame => frame.OffsetX),
            Sample(keyframes, seconds, 0, frame => frame.OffsetY),
            Sample(keyframes, seconds, 1, frame => frame.ScaleX),
            Sample(keyframes, seconds, 1, frame => frame.ScaleY),
            Sample(keyframes, seconds, 0, frame => frame.Blur),
            Sample(keyframes, seconds, 0, frame => frame.Scatter),
            Sample(keyframes, seconds, 0, frame => frame.Noise),
            Sample(keyframes, seconds, 1, frame => frame.Brightness),
            Sample(keyframes, seconds, 0, frame => frame.SplitX),
            Sample(keyframes, seconds, 0, frame => frame.SplitY),
            Sample(keyframes, seconds, 0, frame => frame.Ripple));
    }

    private static float Sample(IReadOnlyList<MovieSubtitleKeyframe> keyframes, double seconds, float baseline, Func<MovieSubtitleKeyframe, float?> property)
    {
        double previousSeconds = 0;
        float previousValue = baseline;
        foreach (MovieSubtitleKeyframe frame in keyframes)
        {
            if (property(frame) is not float value)
            {
                continue;
            }
            if (frame.Seconds > seconds)
            {
                double amount = (seconds - previousSeconds) / (frame.Seconds - previousSeconds);
                return (float)(previousValue + ((double)value - previousValue) * amount);
            }
            previousSeconds = frame.Seconds;
            previousValue = value;
        }
        return previousValue;
    }
}
