using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePrimitiveColorTests
{
    [Theory]
    [InlineData(0x808080u, 255, 255, 255)]
    [InlineData(0x403F20u, 64, 126, 128)]
    [InlineData(0x4080FFu, 255, 128, 64)]
    [InlineData(0xAA5500u, 0, 128, 255)]
    public void Original_PC_textured_diffuse_normalizes_channels_by_the_brightest_component(uint rgb, int red, int green, int blue)
    {
        NativeBattlePrimitiveColor result = NativeBattlePrimitiveColorState.FromColdProcess().Textured(rgb, false, 0, false);
        Assert.Equal(new Vector3(red, green, blue), result.Rgb);
        Assert.Equal((byte)255, result.Alpha);
    }

    [Fact]
    public void Textured_semi_modes_preserve_their_distinct_native_diffuse_and_alpha_dispatch()
    {
        NativeBattlePrimitiveColorState state = NativeBattlePrimitiveColorState.FromColdProcess();
        Assert.Equal(new NativeBattlePrimitiveColor(new(255), 127), state.Textured(0x808080, true, 0, false));
        Assert.Equal(new NativeBattlePrimitiveColor(new(255), 255), state.Textured(0x808080, true, 1, false));
        Assert.Equal(new NativeBattlePrimitiveColor(Vector3.Zero, 110), state.Textured(0x808080, true, 2, false));
        Assert.Equal(new NativeBattlePrimitiveColor(new(64), 255), state.Textured(0x808080, true, 3, false));
        Assert.Equal(new NativeBattlePrimitiveColor(new(255), 70), state.Textured(0, true, 2, true));
        Assert.Equal(new NativeBattlePrimitiveColor(new(255), 255), (state with { SubtractUsesNormalizedColor = true }).Textured(0x808080, true, 2, false));
    }

    [Fact]
    public void Untextured_subtraction_uses_the_original_complementary_channel_and_unclamped_sum_alpha()
    {
        NativeBattlePrimitiveColorState state = NativeBattlePrimitiveColorState.FromColdProcess();
        Assert.Equal(new NativeBattlePrimitiveColor(Vector3.Zero, 192), state.Untextured(0x808080, true, 2));
        Assert.Equal(new NativeBattlePrimitiveColor(new(255, 0, 0), 80), state.Untextured(0x505000, true, 2));
        Assert.Equal(new NativeBattlePrimitiveColor(new(0, 255, 0), 80), state.Untextured(0x500050, true, 2));
        Assert.Equal(new NativeBattlePrimitiveColor(new(0, 0, 255), 80), state.Untextured(0x005050, true, 2));
    }
}
