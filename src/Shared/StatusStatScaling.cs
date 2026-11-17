namespace Memoria.Battle;

internal static class StatusStatScaling
{
    internal const int MinimumPercent = 0;
    internal const int MaximumPercent = 100;

    public static int ReducePercent(int baseValue, int percent)
    {
        long scaled = (long)baseValue * (MaximumPercent - percent);
        return checked((int)(scaled / MaximumPercent));
    }
}
