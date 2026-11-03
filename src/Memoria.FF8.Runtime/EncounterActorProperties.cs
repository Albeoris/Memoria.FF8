namespace Memoria.FF8.Runtime;

public static class EncounterActorProperties
{
    /// <summary>Truncates once after multiplication and retains a positive signed HP owner.</summary>
    public static int ScaleMaximumHp(int baseline, double multiplier)
    {
        if (baseline <= 0 || !double.IsFinite(multiplier) || multiplier <= 0 || multiplier > 100)
        {
            throw new InvalidDataException("Maximum HP requires a positive baseline and a finite multiplier in (0, 100].");
        }
        return (int)Math.Clamp(Math.Floor(baseline * multiplier), 1, int.MaxValue);
    }
}
