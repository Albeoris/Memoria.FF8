using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanUiRepeatTests
{
    [Theory]
    [InlineData(0, 0x0510, 5, 0x100)]
    [InlineData(1, 0x0510, 0, 0)]
    [InlineData(0x80, 0x0710, 7, 0x100)]
    [InlineData(0xFF, 0x0910, 9, 0x100)]
    public void Signed_counter_decrement_reloads_actual_high_byte_interval(byte counter, ushort timing, byte expectedCounter, ushort expectedValue)
    {
        NativeBattleScanByteOwner device = ScanConfiguredInputFixture.Unknown(196).WriteWord(0x14, 0x0100);
        NativeBattleScanByteOwner settings = ScanConfiguredInputFixture.Unknown(4).WriteWord(0, timing);
        NativeBattleScanByteOwner globals = ScanConfiguredInputFixture.Unknown(12).WriteByte(2, counter).WriteWord(8, 0x100);
        NativeBattleScanUiRepeatStep result = NativeBattleScanUiRepeat.Repeat49FE20(device, settings, globals, 0x8100, 2);
        Assert.Equal(expectedCounter, result.Globals1D2B524.ReadByte(2));
        Assert.Equal(expectedValue, result.Value);
        Assert.Equal((ushort)0x8100, result.Globals1D2B524.ReadWord(8));
        Assert.False(result.Globals1D2B524.Known[0]);
        Assert.False(result.Globals1D2B524.Known[10]);
    }

    [Fact]
    public void Changed_overlapping_buttons_restart_delay_before_decrement_and_full_held_word_is_persisted()
    {
        NativeBattleScanByteOwner device = ScanConfiguredInputFixture.Unknown(196).WriteWord(0x10, 0xC0);
        NativeBattleScanByteOwner settings = ScanConfiguredInputFixture.Unknown(4).WriteWord(0, 0x0510);
        NativeBattleScanByteOwner globals = ScanConfiguredInputFixture.Unknown(12).WriteByte(0, 0xFF).WriteWord(4, 0x40);
        NativeBattleScanUiRepeatStep result = NativeBattleScanUiRepeat.Repeat49FE20(device, settings, globals, 0x80C0, 0);
        Assert.Equal((byte)15, result.Globals1D2B524.ReadByte(0));
        Assert.Equal((ushort)0, result.Value);
        Assert.Equal((ushort)0x80C0, result.Globals1D2B524.ReadWord(4));
    }

    [Theory]
    [InlineData(0, 0x40, 0x40)]
    [InlineData(0x40, 0x80, 0x80)]
    [InlineData(0x40, 0, 0)]
    public void No_overlap_returns_current_mask_immediately_and_loads_delay(ushort previous, ushort held, ushort expected)
    {
        NativeBattleScanByteOwner device = ScanConfiguredInputFixture.Unknown(196).WriteWord(0x12, 0xC0);
        NativeBattleScanByteOwner settings = ScanConfiguredInputFixture.Unknown(4).WriteWord(0, 0x0510);
        NativeBattleScanByteOwner globals = ScanConfiguredInputFixture.Unknown(12).WriteByte(1, 0xA5).WriteWord(6, previous);
        NativeBattleScanUiRepeatStep result = NativeBattleScanUiRepeat.Repeat49FE20(device, settings, globals, held, 1);
        Assert.Equal((byte)16, result.Globals1D2B524.ReadByte(1));
        Assert.Equal(expected, result.Value);
        Assert.Equal(held, result.Globals1D2B524.ReadWord(6));
    }

    [Fact]
    public void Logical_zero_selector_controls_all_four_masks_and_distinct_counters_without_touching_input_ring()
    {
        NativeBattleScanInputPublicationState input = ScanConfiguredInputFixture.Input(1, 0);
        NativeBattleScanByteOwner selected = input.Input.Devices[1].WriteWord(0x10, 1).WriteWord(0x12, 2).WriteWord(0x14, 4).WriteWord(0x16, 8);
        input = input with { Input = input.Input with { Devices = input.Input.Devices.SetItem(1, selected) }, Settings = input.Settings.WriteWord(0, 0x0510) };
        NativeBattleScanByteOwner globals = new(new byte[12].ToImmutableArray(), Enumerable.Repeat(true, 12).ToImmutableArray());
        globals = globals.WriteByte(0, 1).WriteByte(1, 0).WriteByte(2, 0x80).WriteByte(3, 2);
        for (int group = 0; group < 4; group++)
        {
            globals = globals.WriteWord(4 + group * 2, 0xF);
        }
        string before = JsonSerializer.Serialize(input);
        NativeBattleScanUiRepeatStep step = NativeBattleScanUiRepeat.Repeat49FDA0(input, globals, 0xF);
        Assert.Equal((ushort)6, step.Value);
        Assert.Equal(new byte[] { 0, 5, 5, 1 }, step.Globals1D2B524.Bytes.AsSpan().Slice(0, 4).ToArray());
        Assert.Equal(before, JsonSerializer.Serialize(input));
        NativeBattleScanByteOwner clonedGlobals = JsonSerializer.Deserialize<NativeBattleScanByteOwner>(JsonSerializer.Serialize(globals))!;
        NativeBattleScanInputPublicationState clonedInput = JsonSerializer.Deserialize<NativeBattleScanInputPublicationState>(before)!;
        Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(NativeBattleScanUiRepeat.Repeat49FDA0(clonedInput, clonedGlobals, 0xF)));
    }

    [Fact]
    public void Missing_selected_device_or_previous_full_word_is_not_replaced_with_zero()
    {
        NativeBattleScanInputPublicationState input = ScanConfiguredInputFixture.Input(2, 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleScanUiRepeat.Repeat49FDA0(input, ScanConfiguredInputFixture.Unknown(12), 1));
        NativeBattleScanByteOwner device = ScanConfiguredInputFixture.Unknown(196).WriteWord(0x10, 1);
        Assert.Throws<NotSupportedException>(() => NativeBattleScanUiRepeat.Repeat49FE20(device, input.Settings, ScanConfiguredInputFixture.Unknown(12), 1, 0));
    }
}
