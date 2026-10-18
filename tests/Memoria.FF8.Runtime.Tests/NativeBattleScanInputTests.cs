using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanInputTests
{
    [Fact]
    public void Raw_reader_selects_physical_device_and_wraps_history_without_status_or_button_mapping()
    {
        NativeBattleScanByteOwner selector = Unknown(196).WriteByte(0xC2, 2);
        NativeBattleScanByteOwner device = Unknown(196).WriteByte(0x18, 0).WriteWord(0x1C + 7 * 20 + 2, 0x8010);
        NativeBattleScanInputBank bank = new([Unknown(196), selector, device]);
        Assert.Equal((ushort)0x8010, bank.Read49ED30(3, 1));
        Assert.Equal((ushort)0x8010, bank.Read49ED30(-1, 9));
        byte[] map = Enumerable.Range(1, 12).Select(value => (byte)value).ToArray();
        map[4] = 7;
        Assert.Equal((ushort)0x8040, NativeInputConfiguration.Project(bank.Read49ED30(1, 1), 0x20, map));
        NativeBattleScanInputBank restored = JsonSerializer.Deserialize<NativeBattleScanInputBank>(JsonSerializer.Serialize(bank))!;
        Assert.Equal((ushort)0x8010, restored.Read49ED30(1, 1));
        Assert.Throws<NotSupportedException>(() => bank.Read49ED30(1, 0));
    }

    [Theory]
    [InlineData(0, 0, 4)]
    [InlineData(1, 0, 5)]
    [InlineData(2, 128, 4)]
    [InlineData(0, 128, 6)]
    [InlineData(-1, 128, 4)]
    [InlineData(9, 0, 7)]
    public void Analogue_reader_xors_then_signed_saturates_axis(int axis, byte type, int expectedOffset)
    {
        NativeBattleScanByteOwner device = Unknown(196).WriteByte(0xC2, 0).WriteByte(0xC3, type).WriteByte(0x18, 2).WriteByte(0x1C + 40, 0).WriteByte(0x1C + 41, 0x7F).WriteByte(0x1C + 40 + expectedOffset, 231);
        NativeBattleScanInputBank bank = new([device, Unknown(196)]);
        Assert.Equal(231, bank.Read49F0A0(0, axis, 0));
    }

    [Fact]
    public void Analogue_status_and_packet_type_reject_before_unconsumed_axis_bytes()
    {
        NativeBattleScanByteOwner device = Unknown(196).WriteByte(0xC2, 0).WriteByte(0x18, 0).WriteByte(0x1C, 1);
        NativeBattleScanInputBank bank = new([device, Unknown(196)]);
        Assert.Equal(-1, bank.Read49F0A0(0, 0, 0));
        device = device.WriteByte(0x1C, 0).WriteByte(0xC3, 0).WriteByte(0x1D, 0x4F);
        Assert.Equal(-1, (bank with { Devices = bank.Devices.SetItem(0, device) }).Read49F0A0(0, 0, 0));
        device = device.WriteByte(0x1D, 0x50);
        Assert.Throws<NotSupportedException>(() => (bank with { Devices = bank.Devices.SetItem(0, device) }).Read49F0A0(0, 0, 0));
    }

    [Fact]
    public void Missing_actual_selector_or_selected_device_fails_without_fallback()
    {
        Assert.Throws<NotSupportedException>(() => new NativeBattleScanInputBank([Unknown(196), Unknown(196)]).Read49ED30(0, 0));
        Assert.Throws<NotSupportedException>(() => new NativeBattleScanInputBank([Unknown(196).WriteByte(0xC2, 9), Unknown(196)]).Read49ED30(0, 0));
    }

    private static NativeBattleScanByteOwner Unknown(int length) => new(new byte[length].ToImmutableArray(), new bool[length].ToImmutableArray());

    [Theory]
    [InlineData(0, 0xC8, 0xCC)]
    [InlineData(1, 0xD0, 0xD4)]
    [InlineData(4, 0xD0, 0xC6)]
    [InlineData(5, 0xD0, 0xD4)]
    public void Digital_sampling_prioritizes_opposing_directions_and_preserves_unknown_axes(ushort modifier, int vertical, int horizontal)
    {
        NativeBattleScanByteOwner device = Unknown(196).WriteByte(0xC2, 0).WriteByte(0x18, 0).WriteWord(0x1E, (ushort)(0xF000 | modifier));
        NativeBattleScanByteOwner sampled = NativeBattleScanInput.Sample8501F0(Unknown(48), new([device, Unknown(196)]), 0, Map());
        Assert.Equal((uint)(0xF000 | modifier), sampled.ReadDword(0));
        Assert.Equal((ushort)8, sampled.ReadWord(vertical - 0xA8));
        Assert.Equal((ushort)8, sampled.ReadWord(horizontal - 0xA8));
        Assert.Equal(2, Enumerable.Range(0, 18).Count(index => sampled.ReadWord(12 + index * 2) != 0));
        Assert.All(sampled.Known.Skip(4).Take(8), known => Assert.False(known));
    }

    [Theory]
    [InlineData(0, 16, 0)]
    [InlineData(95, 0, 0)]
    [InlineData(96, 0, 0)]
    [InlineData(97, 0, 0)]
    [InlineData(158, 0, 0)]
    [InlineData(159, 0, 0)]
    [InlineData(160, 0, 0)]
    [InlineData(255, 0, 16)]
    public void Analogue_sampling_uses_exact_deadzone_integer_magnitudes_and_native_axis_order(byte value, ushort lower, ushort upper)
    {
        NativeBattleScanByteOwner device = Analogue(0).WriteByte(0x22, value);
        NativeBattleScanByteOwner sampled = NativeBattleScanInput.Sample8501F0(Unknown(48), new([device, Unknown(196)]), 0, Map());
        Assert.Equal((ushort)value, sampled.ReadWord(4));
        Assert.Equal(lower, sampled.ReadWord(0xB4 - 0xA8));
        Assert.Equal(upper, sampled.ReadWord(0xB6 - 0xA8));
        Assert.Equal(lower, sampled.ReadWord(0xC8 - 0xA8));
        Assert.Equal(upper, sampled.ReadWord(0xCA - 0xA8));
        Assert.Equal((ushort)128, sampled.ReadWord(6));
    }

    [Theory]
    [InlineData(0, 0xC8, 0xCE)]
    [InlineData(1, 0xD0, 0xD6)]
    [InlineData(4, 0xD0, 0xC4)]
    [InlineData(5, 0xD0, 0xD6)]
    public void Analogue_dispatch_obeys_modifier_precedence_and_keeps_all_second_stick_pairs(ushort mask, int destination2, int destination3)
    {
        NativeBattleScanByteOwner device = Analogue(mask).WriteByte(0x22, 0).WriteByte(0x23, 255).WriteByte(0x20, 255).WriteByte(0x21, 0);
        NativeBattleScanByteOwner sampled = NativeBattleScanInput.Sample8501F0(Unknown(48), new([device, Unknown(196)]), 0, Map());
        Assert.Equal((ushort)16, sampled.ReadWord(destination2 - 0xA8));
        Assert.Equal((ushort)16, sampled.ReadWord(destination3 - 0xA8));
        Assert.Equal((ushort)16, sampled.ReadWord(0xBE - 0xA8));
        Assert.Equal((ushort)16, sampled.ReadWord(0xC0 - 0xA8));
    }

    [Fact]
    public void Unsupported_first_axis_sets_all128_without_reading_the_other_analogue_bytes()
    {
        NativeBattleScanByteOwner device = Unknown(196).WriteByte(0xC2, 0).WriteByte(0x18, 0).WriteWord(0x1E, 0).WriteByte(0x1C, 1);
        NativeBattleScanByteOwner sampled = NativeBattleScanInput.Sample8501F0(Unknown(48), new([device, Unknown(196)]), 0, Map());
        Assert.All(Enumerable.Range(0, 4), index => Assert.Equal((ushort)128, sampled.ReadWord(4 + index * 2)));
        Assert.All(Enumerable.Range(0, 18), index => Assert.Equal((ushort)0, sampled.ReadWord(12 + index * 2)));
    }

    private static NativeBattleScanByteOwner Analogue(ushort mask)
    {
        return Unknown(196).WriteByte(0xC2, 0).WriteByte(0x18, 0).WriteWord(0x1E, mask).WriteByte(0x1C, 0).WriteByte(0x1D, 0x50).WriteByte(0xC3, 0).WriteByte(0x20, 128).WriteByte(0x21, 128).WriteByte(0x22, 128).WriteByte(0x23, 128);
    }

    private static byte[] Map() => Enumerable.Range(1, 12).Select(value => (byte)value).ToArray();
}
