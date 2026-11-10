namespace Memoria.FF7.Runtime;

/// <summary>Original FFVII battle gauge initialization and one eligible battle-time update.</summary>
public static class Ff7NativeAtb
{
    public const int SlotCount = 10;

    public static ushort[] Initialize(ushort activeSlots, byte battleType, bool forcePartyReady, Ff7BattleRng random)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(activeSlots, (ushort)0x3FF);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(battleType, (byte)5);

        ushort[] gauges = new ushort[SlotCount];
        int greatest = 0;
        for (int slot = 0; slot < gauges.Length; slot++)
        {
            if ((activeSlots & (1 << slot)) != 0)
            {
                gauges[slot] = (ushort)(random.NextWord() >> 1);
                greatest = Math.Max(greatest, gauges[slot]);
            }
        }
        for (int slot = 0; slot < gauges.Length; slot++)
        {
            if ((activeSlots & (1 << slot)) == 0)
            {
                continue;
            }
            int initial = battleType switch
            {
                0 or 5 => 0xE000 - greatest + gauges[slot],
                2 or 4 => slot < 4 ? 0 : 0xF000 - greatest + gauges[slot],
                _ => slot < 4 ? 0xFFFE : gauges[slot] >> 3
            };
            gauges[slot] = (ushort)(forcePartyReady ? slot < 3 ? 0xFFFE : 0 : initial);
        }
        return gauges;
    }

    public static int SpeedDenominator(ReadOnlySpan<byte> partyDexterity)
    {
        if (partyDexterity.Length is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(partyDexterity));
        }
        int sum = 0;
        foreach (byte dexterity in partyDexterity)
        {
            sum += dexterity;
        }
        return 50 + (sum + partyDexterity.Length - 1) / partyDexterity.Length;
    }

    public static int BaseRate(byte battleSpeed) => 65536 / (2 * (120 + battleSpeed * 480 / 256));

    public static ushort Increment(int slot, byte dexterity, sbyte dexterityModifier, uint status, int denominator, byte battleSpeed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, SlotCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(denominator, 50);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(denominator, 305);
        ArgumentOutOfRangeException.ThrowIfLessThan(dexterityModifier, (sbyte)-100);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dexterityModifier, (sbyte)100);
        if ((status & 0x02004405) != 0)
        {
            return 0;
        }
        int rate = BaseRate(battleSpeed);
        if ((status & 0x200) == 0)
        {
            rate *= 2;
            if ((status & 0x100) != 0)
            {
                rate *= 2;
            }
        }
        int speed = slot < 3 ? dexterity + dexterity * dexterityModifier / 100 + 50 : dexterity;
        return (ushort)(rate * speed / denominator);
    }

    public static bool Advance(ref ushort gauge, ushort increment, bool fast, bool frozen)
    {
        if (gauge == ushort.MaxValue || frozen)
        {
            return false;
        }
        gauge = (ushort)Math.Min(ushort.MaxValue, gauge + (fast ? increment * 4 : increment));
        return gauge == ushort.MaxValue;
    }
}
