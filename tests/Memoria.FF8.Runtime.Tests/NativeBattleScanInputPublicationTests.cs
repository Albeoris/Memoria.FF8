using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanInputPublicationTests
{
    [Fact]
    public void Status6_configuration_is_sparse_and_keeps_capability_mask_and_unknown_shift_bytes()
    {
        NativeBattleScanByteOwner device = Unknown(196).WriteByte(0x19, 1).WriteByte(0x1B, 0xA5);
        NativeBattleScanByteOwner configured = NativeBattleScanInputPublication.PublishStatus6(device);
        Assert.Equal((byte)0, configured.ReadByte(0x0B));
        Assert.Equal(uint.MaxValue, configured.ReadDword(0xBC));
        Assert.Equal(ushort.MaxValue, configured.ReadWord(0xC0));
        Assert.Equal((byte)1, configured.ReadByte(0x0A));
        Assert.Equal((byte)0, configured.ReadByte(0x19));
        Assert.Equal((byte)0xA5, configured.ReadByte(0x1B));
        Assert.Equal((byte)0, configured.ReadByte(1));
        Assert.Equal((byte)0, configured.ReadByte(2));
        Assert.False(configured.Known[8]);
        Assert.False(configured.Known[9]);
        Assert.False(configured.Known[6]);
        Assert.False(configured.Known[7]);
    }

    [Fact]
    public void Status6_warm_path_uses_retained_signed_sources_mask_and_shift_counts_without_resetting_capabilities()
    {
        NativeBattleScanByteOwner device = Unknown(196).WriteByte(0x19, 2).WriteByte(0x1B, 0xF3).WriteByte(6, 0xFF).WriteByte(7, 0x80).WriteByte(8, 33).WriteByte(9, 39);
        NativeBattleScanByteOwner published = NativeBattleScanInputPublication.PublishStatus6(device);
        Assert.Equal((byte)0x79, published.ReadByte(1));
        Assert.Equal((byte)1, published.ReadByte(2));
        Assert.Equal((byte)1, published.ReadByte(0x0A));
        Assert.Equal((byte)2, published.ReadByte(0x19));
        Assert.Equal((byte)0xF3, published.ReadByte(0x1B));
        Assert.False(published.Known[0x0B]);
        Assert.All(published.Known.Skip(0xBC).Take(6), known => Assert.False(known));
    }

    [Fact]
    public void Initialization_resolves_actual_selectors_and_polls_only_button_bytes_without_analog_headers()
    {
        NativeBattleScanInputPublicationState state = State();
        NativeBattleScanByteOwner unknown = new(new byte[196].ToImmutableArray(), new bool[196].ToImmutableArray());
        unknown = unknown.WriteByte(0xC2, 0);
        state = state with { Input = new([unknown, unknown]), RawSamples = [Unknown(36), Unknown(36)], Settings = state.Settings.WriteByte(0, 13) };
        List<int> polls = [];
        NativeBattleScanInputPublicationState initialized = NativeBattleScanInputPublication.Initialize49F540(state, device =>
        {
            polls.Add(device);
            return 0x1234;
        });
        Assert.Equal(new[] { 0 }, polls);
        Assert.Equal(0x1D2B29Bu, initialized.PollPointers.ReadDword(0));
        Assert.Equal(0x1D2B29Au, initialized.PollPointers.ReadDword(4));
        Assert.Equal(0u, initialized.PollPointers.ReadDword(8));
        Assert.Equal((byte)1, initialized.Settings.ReadByte(2));
        Assert.Equal((byte)0xED, initialized.RawSamples[0].ReadByte(2));
        Assert.Equal((byte)0xCB, initialized.RawSamples[0].ReadByte(3));
        Assert.False(initialized.RawSamples[0].Known[1]);
        Assert.False(initialized.RawSamples[0].Known[4]);
        Assert.All(initialized.RawSamples[1].Known, known => Assert.False(known));
        Assert.Equal((byte)13, initialized.Input.Devices[0].ReadByte(12));
        Assert.False(initialized.Input.Devices[0].Known[0x10]);
        Assert.False(initialized.Input.Devices[1].Known[0x18]);
        Assert.Equal((ushort)0, initialized.Input.Devices[0].ReadWord(0x1E));
    }

    [Fact]
    public void One_publication_polls_PC_twice_then_advances_each_ring_once_and_preserves_other_raw_bytes()
    {
        NativeBattleScanInputPublicationState state = State();
        NativeBattleScanByteOwner second = state.RawSamples[1].WriteByte(2, 0xFF).WriteByte(3, 0xDF).WriteByte(1, 0x70).WriteByte(4, 17);
        state = state with { RawSamples = state.RawSamples.SetItem(1, second) };
        List<int> polls = [];
        NativeBattleScanInputPublicationState published = NativeBattleScanInputPublication.Publish49E9C0(state, device =>
        {
            polls.Add(device);
            return polls.Count == 1 ? 0x0040u : 0x0080u;
        });
        Assert.Equal(new[] { 0, 0 }, polls);
        Assert.Equal((byte)1, published.Input.Devices[0].ReadByte(0x18));
        Assert.Equal((byte)1, published.Input.Devices[1].ReadByte(0x18));
        Assert.Equal((ushort)0x80, published.Input.Devices[0].ReadWord(0x1C + 20 + 2));
        Assert.Equal((ushort)0x80, published.Input.Devices[0].ReadWord(0x1C + 20 + 0x10));
        Assert.Equal((ushort)0x20, published.Input.Devices[1].ReadWord(0x1C + 20 + 2));
        Assert.Equal(second.Bytes.ToArray(), published.RawSamples[1].Bytes.ToArray());
        Assert.Equal((byte)0x70, published.Input.Devices[1].ReadByte(0x1C + 20 + 1));
        Assert.Equal((byte)17, published.Input.Devices[1].ReadByte(0x1C + 20 + 4));
        Assert.Equal((byte)6, published.Input.Devices[0].ReadByte(0x1A));
        Assert.Equal((byte)1, published.Input.Devices[0].ReadByte(0x0A));
    }

    [Fact]
    public void Direction_conflicts_clear_in_producer_and_disabled_input_clears_edges_without_guessing_release()
    {
        NativeBattleScanInputPublicationState state = State();
        NativeBattleScanInputPublicationState conflict = NativeBattleScanInputPublication.Publish49E9C0(state, _ => 0xF040);
        Assert.Equal((ushort)0x40, conflict.Input.Read49ED30(0, 0));
        NativeBattleScanByteOwner device = state.Input.Devices[0].WriteWord(0x1E, 0x40);
        state = state with { Input = state.Input with { Devices = state.Input.Devices.SetItem(0, device) }, Settings = state.Settings.WriteByte(2, 0) };
        NativeBattleScanInputPublicationState disabled = NativeBattleScanInputPublication.Publish49E9C0(state, _ => 0);
        NativeBattleScanByteOwner ring = disabled.Input.Devices[0];
        Assert.Equal((ushort)0, ring.ReadWord(0x1C + 20 + 2));
        Assert.Equal((ushort)0, ring.ReadWord(0x1C + 20 + 0x12));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(39, 1)]
    [InlineData(40, 0)]
    public void Selector_switch_uses_strict_squared_radius_and_keeps_real_analog_type(byte offset, byte selected)
    {
        NativeBattleScanInputPublicationState state = State();
        NativeBattleScanByteOwner raw = state.RawSamples[0].WriteByte(1, 0x50).WriteByte(4, (byte)(128 + offset)).WriteByte(5, 128).WriteByte(6, 128).WriteByte(7, 128);
        state = state with { RawSamples = state.RawSamples.SetItem(0, raw), Settings = state.Settings.WriteByte(3, 1) };
        NativeBattleScanInputPublicationState published = NativeBattleScanInputPublication.Publish49E9C0(state, _ => 0);
        Assert.Equal(selected, published.Input.Devices[0].ReadByte(0xC2));
        Assert.Equal((byte)0x50, published.RawSamples[0].ReadByte(1));
    }

    [Theory]
    [InlineData(1, 1, 0, 1, 3)]
    [InlineData(1, 1, 1, 0, 0)]
    [InlineData(1, 3, 0, 0, 1)]
    [InlineData(1, 2, 0, 2, 2)]
    [InlineData(1, 1, 255, 1, 3)]
    public void Repeat_uses_signed_counter_overlap_change_delay_and_interval(ushort oldHeld, ushort newHeld, byte counter, ushort expected, byte next)
    {
        NativeBattleScanByteOwner device = Zero(196).WriteWord(0x10, 3).WriteByte(0x0C, counter);
        NativeBattleScanRepeatStep step = NativeBattleScanInputPublication.Repeat49EC90(device, Zero(4).WriteWord(0, 0x0302), oldHeld, newHeld, 0);
        Assert.Equal(expected, step.Value);
        Assert.Equal(next, step.Device.ReadByte(0x0C));
    }

    [Fact]
    public void Release_edges_and_warm_publication_continue_identically_after_serialization()
    {
        NativeBattleScanInputPublicationState state = State();
        state = NativeBattleScanInputPublication.Publish49E9C0(state, _ => 0x40);
        NativeBattleScanInputPublicationState restored = JsonSerializer.Deserialize<NativeBattleScanInputPublicationState>(JsonSerializer.Serialize(state))!;
        NativeBattleScanInputPublicationState released = NativeBattleScanInputPublication.Publish49E9C0(state, _ => 0);
        Assert.Equal(JsonSerializer.Serialize(released), JsonSerializer.Serialize(NativeBattleScanInputPublication.Publish49E9C0(restored, _ => 0)));
        Assert.Equal((ushort)0x40, released.Input.Devices[0].ReadWord(0x1C + 40 + 0x12));
        Assert.Equal((ushort)0, released.Input.Devices[0].ReadWord(0x1C + 40 + 0x10));
        Assert.Equal((byte)2, released.Input.Devices[0].ReadByte(0x18));
    }

    [Fact]
    public void Raw_error_status_retains_old_held_and_mouse_type_requires_its_real_service()
    {
        NativeBattleScanInputPublicationState state = State();
        state = state with
        {
            Input = state.Input with { Devices = state.Input.Devices.SetItem(0, state.Input.Devices[0].WriteWord(0x1E, 0x80)) },
            RawSamples = state.RawSamples.SetItem(0, state.RawSamples[0].WriteByte(0, 1))
        };
        NativeBattleScanInputPublicationState retained = NativeBattleScanInputPublication.Publish49E9C0(state, _ => 0);
        Assert.Equal((ushort)0x80, retained.Input.Read49ED30(0, 0));
        state = state with { RawSamples = state.RawSamples.SetItem(0, state.RawSamples[0].WriteByte(0, 0).WriteByte(1, 0x10)) };
        Assert.Throws<NotSupportedException>(() => NativeBattleScanInputPublication.Publish49E9C0(state, _ => 0));
        state = State() with { PollPointers = Unknown(12) };
        Assert.Throws<NotSupportedException>(() => NativeBattleScanInputPublication.Poll56D990(state, _ => 0));
    }

    private static NativeBattleScanInputPublicationState State()
    {
        NativeBattleScanByteOwner device = Zero(196).WriteWord(0x10, 0xFFFF).WriteWord(0x12, 0xFFFF).WriteWord(0x14, 0xFFFF).WriteWord(0x16, 0xFFFF);
        return new(new([device, device]), [Zero(36), Zero(36).WriteByte(2, 255).WriteByte(3, 255)], Zero(4).WriteWord(0, 0x0302).WriteByte(2, 1), Zero(12).WriteDword(0, 0x1D2B29B).WriteDword(4, 0x1D2B29A));
    }

    private static NativeBattleScanByteOwner Zero(int length) => new(new byte[length].ToImmutableArray(), Enumerable.Repeat(true, length).ToImmutableArray());
    private static NativeBattleScanByteOwner Unknown(int length) => new(new byte[length].ToImmutableArray(), new bool[length].ToImmutableArray());
}
