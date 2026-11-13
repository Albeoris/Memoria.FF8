namespace Memoria.FF8.Runtime;

/// <summary>Source-owned repeat selection and equipped cost rules shared by battle hosts.</summary>
public static class NativeBattleMagicRepetition
{
    public static IReadOnlyList<int> NativeCounts(uint statuses, int available)
    {
        if (available <= 0)
        {
            return [];
        }
        List<int> counts = [1];
        if ((statuses & 0x20000) != 0)
        {
            counts.Add(2);
        }
        if ((statuses & 0x40000) != 0)
        {
            counts.Add(3);
        }
        return counts;
    }

    public static IReadOnlyList<int> PlayerCounts(uint statuses, int available, uint passives, bool infiniteMagic, bool unlimitedMagic)
    {
        return NativeCounts(statuses, available).Select(count => Cost(count, passives, infiniteMagic, unlimitedMagic) == 1 ? count : Math.Min(count, available)).Distinct().Order().ToArray();
    }

    public static int Cost(int casts, uint passives, bool infiniteMagic, bool unlimitedMagic)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(casts, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(casts, 3);
        return (!infiniteMagic || unlimitedMagic) && (casts == 2 && (passives & 0x20) != 0 || casts == 3 && (passives & 0x40) != 0) ? 1 : casts;
    }

    public static bool AdmitsRepeat(uint statuses, int ordinal)
    {
        return ordinal == 1 || ordinal == 2 && (statuses & 0x60000) != 0 || ordinal == 3 && (statuses & 0x40000) != 0;
    }
}
