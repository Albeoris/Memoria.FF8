namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldSalaryMovementTests
{
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(31, 0, 0, 3)]
    [InlineData(22, 0, 22, 3)]
    [InlineData(31, 0, 8, 4)]
    [InlineData(-32768, 0, 0, 4096)]
    [InlineData(32767, 32767, 0, 5792)]
    public void Original_signed_word_length_truncates_before_division(int x, int y, int z, uint expected)
    {
        Assert.Equal(expected, NativeWorldSalaryMovement.AcceptedVectorUnits(x, y, z));
    }

    [Fact]
    public void Unsupported_velocity_width_or_signed_square_root_domain_refuses()
    {
        Assert.Throws<NotSupportedException>(() => NativeWorldSalaryMovement.AcceptedVectorUnits(32768, 0, 0));
        Assert.Throws<NotSupportedException>(() => NativeWorldSalaryMovement.AcceptedVectorUnits(32767, 32767, 32767));
    }
}
