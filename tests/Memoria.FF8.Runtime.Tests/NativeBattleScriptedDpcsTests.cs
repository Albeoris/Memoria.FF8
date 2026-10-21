using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedDpcsTests
{
    [Fact]
    public void Interpolation_publishes_unscaled_MAC_signed_IR_FIFO_material_and_full_IR0()
    {
        NativeBattleScriptedColorRegisters previous = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [0x11111111, null, 0x22222222], Flags = uint.MaxValue, NormalInput = ImmutableArray.CreateRange(new byte[8]), NormalInputKnown = [true, false, true, true, true, true, true, true] };
        NativeBattleScriptedDpcsResult result = NativeBattleScriptedDpcs.Transform45F270(previous, 0xAB010203, 0x12340800, Far(512, 1024, 2048), Constants());
        Assert.Equal(new int?[] { 280, 528, 1032 }, result.Registers.Mac);
        Assert.Equal(result.Registers.Mac, result.Registers.Ir);
        Assert.Equal(new uint?[] { null, 0x22222222, 0xAB402111 }, result.Registers.Rgb);
        Assert.Equal(0xAB402111u, result.PackedRgb);
        Assert.Equal(0xAB010203u, result.Registers.Material);
        Assert.Equal(0x12340800u, result.Registers.Ir0);
        Assert.Equal(0u, result.Registers.Flags);
        Assert.Equal(previous.NormalInput, result.Registers.NormalInput);
        Assert.Equal(previous.NormalInputKnown, result.Registers.NormalInputKnown);
    }

    [Theory]
    [InlineData(1, 3, 5, 0, 2, 2, 0u)]
    [InlineData(-1, -3, -5, 0, -2, -2, 0x00180000u)]
    public void Conversion_uses_native_precision53_nearest_ties_in_both_directions(int red, int green, int blue, int expectedRed, int expectedGreen, int expectedBlue, uint flags)
    {
        NativeBattleScriptedDpcsResult result = NativeBattleScriptedDpcs.Transform45F270(NativeBattleScriptedColorRegisters.Unknown(), 0, 2048, Far(red, green, blue), Constants());
        Assert.Equal(new int?[] { expectedRed, expectedGreen, expectedBlue }, result.Registers.Mac);
        Assert.Equal(flags, result.Registers.Flags);
    }

    [Fact]
    public void Signed_low_WORD_cue_is_not_clamped_and_high_WORD_is_retained()
    {
        NativeBattleScriptedDpcsResult result = NativeBattleScriptedDpcs.Transform45F270(NativeBattleScriptedColorRegisters.Unknown(), 0x7F000000, 0xAABBFFFF, Far(4096, 0, 0), Constants());
        Assert.Equal(-1, result.Registers.Mac[0]);
        Assert.Equal(-1, result.Registers.Ir[0]);
        Assert.Equal(0x00200000u, result.Registers.Flags);
        Assert.Equal(0x7F000000u, result.PackedRgb);
        Assert.Equal(0xAABBFFFFu, result.Registers.Ir0);
    }

    [Theory]
    [InlineData(0, 32767, 32767, 0x00200000u)]
    [InlineData(0, 32768, 32767, 0x81200000u)]
    [InlineData(1, -32768, -32768, 0x00100000u)]
    [InlineData(1, -32769, -32768, 0x80900000u)]
    [InlineData(2, 32768, 32767, 0x00480000u)]
    [InlineData(2, -32769, -32768, 0x00480000u)]
    public void IR_and_RGB_flags_use_the_exact_channel_masks_without_extra_summary(int lane, int value, int expectedIr, uint flags)
    {
        int[] far = [0, 0, 0];
        far[lane] = value;
        NativeBattleScriptedDpcsResult result = NativeBattleScriptedDpcs.Transform45F270(NativeBattleScriptedColorRegisters.Unknown(), 0, 4096, Far(far[0], far[1], far[2]), Constants());
        Assert.Equal(value, result.Registers.Mac[lane]);
        Assert.Equal(expectedIr, result.Registers.Ir[lane]);
        Assert.Equal(flags, result.Registers.Flags);
    }

    [Fact]
    public void Delta_and_final_add_preserve_unchecked_DWORD_wrap()
    {
        NativeBattleScriptedDpcsResult result = NativeBattleScriptedDpcs.Transform45F270(NativeBattleScriptedColorRegisters.Unknown(), 255, 4096, Far(int.MinValue, 0, 0), Constants());
        Assert.Equal(int.MinValue, result.Registers.Mac[0]);
        Assert.Equal(short.MinValue, result.Registers.Ir[0]);
        Assert.Equal(0x81200000u, result.Registers.Flags);
    }

    [Fact]
    public void Unknown_FIFO_entries_stay_unknown_until_shifted_out_and_packet_opcode_does_not_enter_FIFO()
    {
        NativeBattleScriptedColorRegisters state = NativeBattleScriptedColorRegisters.Unknown();
        NativeBattleScriptedDpcsResult first = NativeBattleScriptedDpcs.Transform45F270(state, 0, 4096, Far(2048, 2048, 2048), Constants());
        Assert.Equal(new uint?[] { null, null, 0x00808080 }, first.Registers.Rgb);
        NativeBattleScriptedDpcsResult second = NativeBattleScriptedDpcs.Transform45F270(first.Registers, 0, 0, Far(0, 0, 0), Constants());
        Assert.Equal(new uint?[] { null, 0x00808080, 0 }, second.Registers.Rgb);
        Assert.Equal(0u, second.Registers.Material);
    }

    [Fact]
    public void Missing_far_control_and_unproved_conversion_constants_fail_fast()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedDpcs.Transform45F270(NativeBattleScriptedColorRegisters.Unknown(), 0, 0, new(null, 0, 0, null, null), Constants()));
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedDpcs.Transform45F270(NativeBattleScriptedColorRegisters.Unknown(), 0, 0, Far(0, 0, 0), Constants() with { FixedScale = 1 }));
    }

    private static NativeBattleModelRendererRegisters Far(int red, int green, int blue) => new(unchecked((uint)red), unchecked((uint)green), unchecked((uint)blue), null, null);
    private static NativeBattleSummonColors Constants() => new(0, 0, 1f / 4096f, 4503601774854144.0);
}
