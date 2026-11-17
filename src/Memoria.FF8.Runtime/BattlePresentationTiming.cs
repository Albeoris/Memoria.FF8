namespace Memoria.FF8.Runtime;

public readonly record struct BattlePresentationCallbackStep(long Callbacks, long Subticks, bool HalfSubtick);

public static class BattlePresentationTiming
{
    // The original 47CE10 instruction callback interval is independent of the display and host update rate.
    private const int RateNumerator = 31;
    private const int RateDenominator = 2;
    public const int NativeUiServicePollsPerCallback = 4;
    public const double CallbacksPerSecond = (double)RateNumerator / RateDenominator;

    public static TimeSpan ElapsedForCallbacks(long callbacks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(callbacks);
        long numerator = checked(callbacks * TimeSpan.TicksPerSecond * RateDenominator);
        long ticks = Math.DivRem(numerator, RateNumerator, out long remainder);
        return TimeSpan.FromTicks(remainder == 0 ? ticks : checked(ticks + 1));
    }

    public static BattlePresentationCallbackStep Advance(long subticks, bool halfSubtick, TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsed.Ticks);
        if (subticks is < 0 or >= TimeSpan.TicksPerSecond)
        {
            throw new ArgumentOutOfRangeException(nameof(subticks));
        }
        long units = checked(RateDenominator * subticks + (halfSubtick ? 1 : 0) + RateNumerator * elapsed.Ticks);
        long denominator = RateDenominator * TimeSpan.TicksPerSecond;
        long remainder = units % denominator;
        return new(units / denominator, remainder / RateDenominator, (remainder & 1) != 0);
    }
}
