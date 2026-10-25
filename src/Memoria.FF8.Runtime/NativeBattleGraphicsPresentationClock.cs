namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleGraphicsPresentationStep(long Callbacks, long Subticks, bool HalfSubtick);

public static class NativeBattleGraphicsPresentationClock
{
    //47CE10 selects 15.5 for ordinary battle callbacks through 4020C0.
    private const int RateNumerator = 31;
    private const int RateDenominator = 2;
    public const double CallbacksPerSecond = (double)RateNumerator / RateDenominator;

    public static TimeSpan ElapsedForCallbacks(long callbacks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(callbacks);
        return TimeSpan.FromTicks(checked(callbacks * TimeSpan.TicksPerSecond * RateDenominator) / RateNumerator);
    }

    public static NativeBattleGraphicsPresentationStep Advance(long subticks, bool halfSubtick, TimeSpan elapsed)
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
