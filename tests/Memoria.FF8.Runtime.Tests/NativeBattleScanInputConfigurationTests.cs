using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanInputConfigurationTests
{
    [Fact]
    public void Configuration_preserves_current_selector_order_before_final_identity_publication()
    {
        NativeBattleScanInputPublicationState input = ScanConfiguredInputFixture.Input(1, 0);
        NativeBattleScanConfiguredInputStep result = NativeBattleScanInputConfiguration.Configure4B9460(input, ScanConfiguredInputFixture.Unknown(16));
        Assert.Equal((byte)0, result.State.Input.Devices[0].ReadByte(0xC2));
        Assert.Equal((byte)1, result.State.Input.Devices[1].ReadByte(0xC2));
        for (int group = 0; group < 4; group++)
        {
            Assert.Equal((byte)16, result.State.Input.Devices[0].ReadByte(0x0C + group));
            Assert.Equal((byte)0, result.State.Input.Devices[1].ReadByte(0x0C + group));
        }
        Assert.Equal((byte)1, input.Input.Devices[0].ReadByte(0xC2));
        Assert.Equal((byte)0, input.Input.Devices[1].ReadByte(0xC2));
    }

    [Fact]
    public void Configured_masks_mouse_bounds_and_sparse_fields_are_exact_without_new_raw_samples()
    {
        NativeBattleScanInputPublicationState input = ScanConfiguredInputFixture.Input(0, 1);
        NativeBattleScanConfiguredInputStep result = NativeBattleScanInputConfiguration.Configure4B9460(input, ScanConfiguredInputFixture.Unknown(16));
        Assert.Equal((ushort)0x0510, result.State.Settings.ReadWord(0));
        Assert.Equal((byte)0, result.State.Settings.ReadByte(3));
        Assert.False(result.State.Settings.Known[2]);
        Assert.Same(input.RawSamples[0], result.State.RawSamples[0]);
        Assert.Same(input.RawSamples[1], result.State.RawSamples[1]);
        Assert.Same(input.PollPointers, result.State.PollPointers);
        foreach (NativeBattleScanByteOwner device in result.State.Input.Devices)
        {
            Assert.Equal(new ushort[] { 0x0FFF, 0x5000, 0xA000, 0x0900 }, Enumerable.Range(0, 4).Select(group => device.ReadWord(0x10 + group * 2)));
            Assert.Equal((byte)0x40, device.ReadByte(0));
            Assert.Equal((byte)0x31, device.ReadByte(0xC3));
            Assert.Equal((byte)1, device.ReadByte(0x19));
            Assert.Equal((ushort)0, device.ReadWord(6));
            Assert.Equal(uint.MaxValue, device.ReadDword(0xBC));
            Assert.Equal(ushort.MaxValue, device.ReadWord(0xC0));
            Assert.Equal((byte)0, device.ReadByte(0x0A));
            Assert.Equal((byte)0, device.ReadByte(0x0B));
            Assert.False(device.Known[1]);
            Assert.False(device.Known[0x1A]);
            Assert.All(device.Bytes.AsSpan().Slice(0x1C, 160).ToArray(), value => Assert.Equal((byte)0, value));
            Assert.All(device.Known.Skip(0x1C).Take(160), known => Assert.True(known));
        }
        Assert.Equal(new ushort[] { 0, 0, 0, 0, 0, 0, 319, 223 }, Enumerable.Range(0, 8).Select(index => result.Mouse16.ReadWord(index * 2)));
    }

    [Fact]
    public void Boot_then_configuration_is_observably_different_from_reversing_original_startup()
    {
        NativeBattleScanInputPublicationState original = ScanConfiguredInputFixture.Input(0, 1);
        NativeBattleScanInputPublicationState booted = NativeBattleScanInputPublication.Initialize49F540(original, _ => 0);
        NativeBattleScanConfiguredInputStep correct = NativeBattleScanInputConfiguration.Configure4B9460(booted, ScanConfiguredInputFixture.Unknown(16));
        NativeBattleScanConfiguredInputStep configured = NativeBattleScanInputConfiguration.Configure4B9460(original, ScanConfiguredInputFixture.Unknown(16));
        NativeBattleScanInputPublicationState reversed = NativeBattleScanInputPublication.Initialize49F540(configured.State, _ => 0);
        Assert.Equal((byte)0, correct.State.Input.Devices[1].ReadByte(0x0C));
        Assert.Equal((byte)16, reversed.Input.Devices[1].ReadByte(0x0C));
    }

    [Fact]
    public void Independent_configuration_continuation_keeps_all_retained_unknown_suffix_bytes()
    {
        NativeBattleScanInputPublicationState input = ScanConfiguredInputFixture.Input(1, 0);
        NativeBattleScanInputPublicationState clone = JsonSerializer.Deserialize<NativeBattleScanInputPublicationState>(JsonSerializer.Serialize(input))!;
        NativeBattleScanConfiguredInputStep left = NativeBattleScanInputConfiguration.Configure4B9460(input, ScanConfiguredInputFixture.Unknown(16));
        NativeBattleScanConfiguredInputStep right = NativeBattleScanInputConfiguration.Configure4B9460(clone, ScanConfiguredInputFixture.Unknown(16));
        Assert.Equal(JsonSerializer.Serialize(left), JsonSerializer.Serialize(right));
    }

    [Fact]
    public void Unknown_or_absent_current_selector_fails_instead_of_assuming_identity()
    {
        NativeBattleScanInputPublicationState input = ScanConfiguredInputFixture.Input(0, 1);
        input = input with { Input = input.Input with { Devices = input.Input.Devices.SetItem(0, ScanConfiguredInputFixture.Unknown(196)) } };
        Assert.Throws<NotSupportedException>(() => NativeBattleScanInputConfiguration.Configure4B9460(input, ScanConfiguredInputFixture.Unknown(16)));
        input = input with { Input = input.Input with { Devices = input.Input.Devices.SetItem(0, ScanConfiguredInputFixture.Unknown(196).WriteByte(0xC2, 2)) } };
        Assert.Throws<NotSupportedException>(() => NativeBattleScanInputConfiguration.Configure4B9460(input, ScanConfiguredInputFixture.Unknown(16)));
    }
}

internal static class ScanConfiguredInputFixture
{
    public static NativeBattleScanByteOwner Unknown(int length) => new(Enumerable.Repeat((byte)0xA5, length).ToImmutableArray(), Enumerable.Repeat(false, length).ToImmutableArray());
    public static NativeBattleScanInputPublicationState Input(byte first, byte second) => new(new([Unknown(196).WriteByte(0xC2, first), Unknown(196).WriteByte(0xC2, second)]), [Unknown(36), Unknown(36)], Unknown(4).WriteByte(0, 9).WriteByte(1, 3), Unknown(12));
}
