namespace Memoria.FF8.Runtime;

/// <summary>Scales player translation without advancing simulation clocks or changing native motor state.</summary>
public static class PlayerMovementSpeed
{
    public const int DefaultPercent = 100;
    public const int MinimumPercent = 50;
    public const int MaximumPercent = 300;

    public static int ValidatePercent(int percent)
    {
        if (percent is < MinimumPercent or > MaximumPercent)
        {
            throw new ArgumentOutOfRangeException(nameof(percent));
        }
        return percent;
    }

    public static int Scale(int movement, int percent)
    {
        ValidatePercent(percent);
        return checked((int)((long)movement * percent / 100));
    }
}
