namespace Memoria.FF7.Runtime;

/// <summary>Translates shared elemental identities at the boundary of a mixed hit.</summary>
public static class Ff7GuestBattleElements
{
    // Fire, Ice, Lightning, Earth and Poison have the same positions. FFVII inserts Gravity before Water.
    private const byte SharedLowElements = 0x1F;
    private const byte OriginWind = 0x20;
    private const byte OriginWater = 0x40;
    private const byte OriginHoly = 0x80;
    private const ushort HostWater = 0x40;
    private const ushort HostWind = 0x80;
    private const ushort HostHoly = 0x100;

    public static ushort ToHost(byte elements) => (ushort)((elements & SharedLowElements) | (elements & OriginWater) | (elements & OriginWind) << 2 | (elements & OriginHoly) << 1);

    public static byte ToOrigin(ushort elements) => elements == ushort.MaxValue ? (byte)0 : (byte)((elements & SharedLowElements) | (elements & HostWater) | (elements & HostWind) >> 2 | (elements & HostHoly) >> 1);

    internal static int DefenseAdjustment(ushort elements, IReadOnlyList<int> originDefenses)
    {
        byte origin = ToOrigin(elements);
        if (origin == 0)
        {
            return 0;
        }
        // The FFVIII attack consumer uses the lowest elemental bit when more than one is set.
        int index = System.Numerics.BitOperations.TrailingZeroCount((uint)origin);
        return 800 - originDefenses[index];
    }
}
