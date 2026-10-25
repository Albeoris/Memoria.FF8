namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleGraphicsPresentationStep(long Callbacks, long Subticks, bool HalfSubtick);

public static class NativeBattleGraphicsPresentationClock
{
    //47CE10 selects 15.5 for ordinary battle callbacks through 4020C0.
    public const double CallbacksPerSecond = 15.5;

    public static NativeBattleGraphicsPresentationStep Advance(long subticks, bool halfSubtick, TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsed.Ticks);
        if (subticks is < 0 or >= TimeSpan.TicksPerSecond)
        {
            throw new ArgumentOutOfRangeException(nameof(subticks));
        }
        long units = checked(2 * subticks + (halfSubtick ? 1 : 0) + 31 * elapsed.Ticks);
        long denominator = 2 * TimeSpan.TicksPerSecond;
        long remainder = units % denominator;
        return new(units / denominator, remainder / 2, (remainder & 1) != 0);
    }
}
