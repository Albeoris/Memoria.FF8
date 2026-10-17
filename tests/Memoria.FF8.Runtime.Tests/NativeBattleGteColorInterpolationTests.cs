using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGteColorInterpolationTests
{
    private static NativeBattleSummonColors Rules => new(0, 0, 1f / 4096, 4503601774854144d);

    [Theory]
    [InlineData(false, 0x2C000000u)]
    [InlineData(true, 0xCC000000u)]
    public void Secondary_FIFO_command_comes_from_the_actual_third_input_of_45E120(bool quad, uint command)
    {
        ImmutableArray<uint> source = quad ? [0x2C808080, 0xAA202020, 0xBB606060, 0xCCC0C0C0] : [0x2C808080, 0xAA202020, 0xBB606060];
        ImmutableArray<uint> result = NativeBattleGteColorInterpolation.Gouraud45F4C0(source, quad, 2048, 0x404040, Rules);
        Assert.Equal(0x2C606060u, result[0]);
        Assert.Equal(command | 0x303030, result[1]);
        Assert.Equal(command | 0x505050, result[2]);
        if (quad)
        {
            Assert.Equal(command | 0x808080, result[3]);
        }
        Assert.Equal(0xAA202020u, source[1]);
    }

    [Theory]
    [InlineData(0, 0x808080u)]
    [InlineData(2048, 0x606060u)]
    [InlineData(4096, 0x404040u)]
    public void Signed_WORD_factor_and_first_color_45F270_preserve_existing_interpolation_rounding(int factor, uint expected)
    {
        ImmutableArray<uint> result = NativeBattleGteColorInterpolation.Gouraud45F4C0([0x3E808080, 0x11202020, 0x33606060, 0xCCC0C0C0], true, factor + 65536, 0x404040, Rules);
        Assert.Equal(0x3E000000u | expected, result[0]);
        Assert.Equal(0xCC000000u, result[1] & 0xFF000000);
    }

    [Fact]
    public void Wrong_FIFO_source_counts_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleGteColorInterpolation.Gouraud45F4C0(default, false, 0, 0, Rules));
        Assert.Throws<InvalidDataException>(() => NativeBattleGteColorInterpolation.Gouraud45F4C0([0, 0, 0], true, 0, 0, Rules));
    }
}
