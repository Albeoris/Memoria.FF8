namespace Memoria.FF7.Formats;

public sealed record Ff7BattleStageFadeRamp(int Accumulated, int Delta, int Remaining);
public sealed record Ff7BattleStageFadeState(byte Red = 0, byte Green = 0, byte Blue = 0, short Transparency = 0, Ff7BattleStageFadeRamp? Ramp = null)
{
    public byte Alpha => Ff7BattleStageFade.Alpha(Transparency);
}

public static class Ff7BattleStageFade
{
    public static byte Alpha(short transparency)
    {
        int value = transparency >> 4;
        return value >= 256 ? (byte)0 : (byte)(255 - value);
    }

    public static Ff7BattleStageFadeState FadeTo(Ff7BattleStageFadeState state, int target, int duration)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);
        if (state.Ramp is not null)
        {
            return state;
        }
        int accumulated = state.Transparency << 16;
        int delta = unchecked((target << 16) - accumulated) / duration;
        return state with { Ramp = new(accumulated, delta, duration) };
    }

    public static Ff7BattleStageFadeState Advance(Ff7BattleStageFadeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Ramp is not { } ramp)
        {
            return state;
        }
        int accumulated = unchecked(ramp.Accumulated + ramp.Delta);
        return state with { Transparency = (short)(accumulated >> 16), Ramp = ramp.Remaining == 1 ? null : ramp with { Accumulated = accumulated, Remaining = ramp.Remaining - 1 } };
    }
}
