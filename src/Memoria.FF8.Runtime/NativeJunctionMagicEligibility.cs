namespace Memoria.FF8.Runtime;

public static class NativeJunctionMagicEligibility
{
    public static bool HasContribution(ReadOnlySpan<byte> magicRecord, int slot)
    {
        if (magicRecord.Length != 60 || (uint)slot >= 19)
        {
            throw new ArgumentException("Junction eligibility requires a complete magic record and a supported junction slot.");
        }
        return slot switch
        {
            < 9 => unchecked((sbyte)magicRecord[23 + slot]) != 0,
            9 => magicRecord[32] != 0,
            10 => (magicRecord[38] | magicRecord[39]) != 0,
            <= 14 => magicRecord[34] != 0,
            _ => (magicRecord[40] | magicRecord[41]) != 0
        };
    }
}
