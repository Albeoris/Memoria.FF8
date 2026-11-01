using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionMagicEligibilityTests
{
    [Theory]
    [InlineData(32, 9)]
    [InlineData(38, 10)]
    [InlineData(39, 10)]
    [InlineData(34, 11)]
    [InlineData(34, 14)]
    [InlineData(40, 15)]
    [InlineData(41, 18)]
    public void Effect_slots_require_their_own_mask_even_when_percentage_is_zero(int offset, int slot)
    {
        byte[] record = new byte[60];
        record[offset] = 0x80;
        Assert.True(NativeJunctionMagicEligibility.HasContribution(record, slot));
        foreach (int other in new[] { 9, 10, 11, 15 })
        {
            bool sameCategory = other == slot || other is >= 11 and <= 14 && slot is >= 11 and <= 14 || other >= 15 && slot >= 15;
            Assert.Equal(sameCategory, NativeJunctionMagicEligibility.HasContribution(record, other));
        }
    }

    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(18)]
    public void Casting_elements_statuses_and_junction_percentages_do_not_enable_a_slot(int slot)
    {
        byte[] record = new byte[60];
        Array.Fill(record, (byte)255, 0, 23);
        record[33] = record[35] = record[36] = record[37] = 100;
        Assert.False(NativeJunctionMagicEligibility.HasContribution(record, slot));
    }
}
