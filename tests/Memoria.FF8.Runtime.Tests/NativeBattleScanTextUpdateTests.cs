using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanTextUpdateTests
{
    [Theory]
    [InlineData(4000, 4096, 4096, 1)]
    [InlineData(100, -4096, 0, 0)]
    [InlineData(-32768, 32767, 0, 0)]
    [InlineData(10, 1, 11, 1)]
    public void Progress_clamps_signed_sum_and_updates_only_actual_visibility_and_rectangles(short old, short rate, ushort expected, byte visible)
    {
        NativeBattleScanTextFrame frame = ScanTextFixture.Frame().WithRow(row => row.WriteDword(8, 0).WriteWord(0x1C, unchecked((ushort)old)).WriteWord(0x1E, unchecked((ushort)rate)).WriteByte(0x23, 91));
        NativeBattleScanTextFrame result = NativeBattleScanTextUpdate.Advance49FEB0(frame, 0, 0, 0, ScanTextFixture.Unused());
        Assert.Equal(expected, result.Windows.Row(0).ReadWord(0x1C));
        Assert.Equal(visible, result.Windows.Backend(37).ReadByte(0x35));
        Assert.Equal((byte)91, result.Windows.Row(0).ReadByte(0x23));
        Assert.Equal(frame.Windows.Row(0).Bytes.AsSpan().Slice(0, 8).ToArray(), result.Windows.Row(0).Bytes.AsSpan().Slice(0, 8).ToArray());
        Assert.Equal(visible == 0 ? (byte)0 : (byte)1, result.Windows.Row(0).ReadByte(0x16));
    }

    [Fact]
    public void Initialization_is_sparse_and_speed_gate_does_not_persist_transient_state_two()
    {
        NativeBattleScanTextFrame frame = ScanTextFixture.Frame().WithRow(row => row.WriteDword(0x24, 0).WriteWord(0x10, 100).WriteWord(0x14, 0));
        NativeBattleScanTextFrame result = NativeBattleScanTextUpdate.Advance49FEB0(frame, 0, 0, 0, ScanTextFixture.Unused());
        Assert.Equal(1u, result.Windows.Row(0).ReadDword(0x24));
        Assert.Equal((ushort)100, result.Windows.Row(0).ReadWord(0x14));
        Assert.Equal(new byte[] { 0, 0, 0xA5, 0xA5, 0, 0, 0, 0, 0xA5, 0xA5, 0xA5, 0xA5 }, result.Globals1D2B524.Bytes.ToArray());
    }

    [Theory]
    [InlineData(1, 0x25)]
    [InlineData(7, 0x80)]
    public void Page_break_uses_current_token_and_does_not_consume_until_acknowledged(byte token, byte indicator)
    {
        NativeBattleScanTextFrame frame = ScanTextFixture.Frame().WithRow(row => row.WriteDword(0x24, 3).WriteByte(0x2E, 0xA5));
        frame = frame with { ExpandedContext128 = frame.ExpandedContext128.WriteByte(0, token) };
        NativeBattleScanTextFrame result = NativeBattleScanTextUpdate.Advance49FEB0(frame, 0, 0x40, 0, ScanTextFixture.Unused());
        Assert.Equal(9u, result.Windows.Row(0).ReadDword(0x24));
        Assert.Equal(indicator, result.Windows.Row(0).ReadByte(0x2E));
        Assert.Equal((byte)0, result.Windows.Row(0).ReadByte(0x20));
    }

    [Fact]
    public void Acknowledgement_preserves_low_nibble_in_both_halves_and_orders_expansion_consume_expansion()
    {
        NativeBattleScanTextFrame frame = ScanTextFixture.Frame().WithRow(row => row.WriteDword(0x24, 10).WriteByte(0x17, 0xA3).WriteByte(0x2E, 0xFF));
        frame = frame with { ExpandedContext128 = frame.ExpandedContext128.WriteByte(0, 7) };
        List<uint> calls = [];
        NativeBattleScanTextServices services = new((current, pointer) =>
        {
            calls.Add(pointer);
            return current;
        }, pointer => pointer == 0x100 ? (byte)7 : throw new InvalidDataException(), (_, _) => throw new InvalidDataException());
        NativeBattleScanTextFrame result = NativeBattleScanTextUpdate.Advance49FEB0(frame, 0, 0x40, 0, services);
        Assert.Equal(new uint[] { 0x100, 0x101, 0x101 }, calls);
        Assert.Equal((byte)0x33, result.Windows.Row(0).ReadByte(0x17));
        Assert.Equal((byte)0, result.Windows.Row(0).ReadByte(0x2E));
        Assert.Equal(1u, result.Windows.Row(0).ReadDword(0x24));
        Assert.Equal(0x101u, result.Windows.Row(0).ReadDword(8));
    }

    [Fact]
    public void Selection_uses_high_word_edges_secondary_repeat_and_lower_first_clamp()
    {
        NativeBattleScanTextFrame frame = ScanTextFixture.Frame().WithRow(row => row.WriteDword(0x24, 13).WriteByte(0x29, 5).WriteByte(0x2A, 2).WriteByte(0x2B, 5).WriteByte(0x2C, 0xFF));
        List<int> sounds = [];
        NativeBattleScanTextServices services = ScanTextFixture.Unused() with
        {
            Sound4B92A0 = (current, sound) =>
            {
                sounds.Add(sound);
                return current;
            }
        };
        NativeBattleScanTextFrame lower = NativeBattleScanTextUpdate.Advance49FEB0(frame, 0, 0x00100000, 0x4000, services);
        Assert.Equal((byte)5, lower.Windows.Row(0).ReadByte(0x2B));
        Assert.Empty(sounds);
        NativeBattleScanTextFrame confirmed = NativeBattleScanTextUpdate.Advance49FEB0(frame, 0, 0x00400000, 0, services);
        Assert.Equal(7u, confirmed.Windows.Row(0).ReadDword(0x24));
        Assert.Equal((byte)1, confirmed.Windows.Row(0).ReadByte(0x19));
        Assert.Equal(new[] { 2 }, sounds);
        sounds.Clear();
        NativeBattleScanTextFrame repeated = NativeBattleScanTextUpdate.Advance49FEB0(frame, 0, 0x40, 0x1000, services);
        Assert.Equal((byte)2, repeated.Windows.Row(0).ReadByte(0x2B));
        Assert.Equal(new[] { 1 }, sounds);
        NativeBattleScanTextFrame other = NativeBattleScanTextUpdate.Advance49FEB0(frame with { SelectedWindow1D2B51C = -1 }, 0, uint.MaxValue, uint.MaxValue, services);
        Assert.Equal((byte)5, other.Windows.Row(0).ReadByte(0x2B));
        Assert.Equal(13u, other.Windows.Row(0).ReadDword(0x24));
    }

    [Fact]
    public void Control_tokens_change_speed_format_and_wait_without_fabricated_completion()
    {
        NativeBattleScanTextFrame frame = ScanTextFixture.Frame().WithRow(row => row.WriteDword(0x24, 3).WriteByte(0x17, 0xA7));
        frame = frame with { ExpandedContext128 = frame.ExpandedContext128.Write(0, new byte[] { 8, 0x21, 6, 0x3C, 9, 0x22, 0 }) };
        NativeBattleScanTextFrame first = NativeBattleScanTextUpdate.Advance49FEB0(frame, 0, 0, 0, ScanTextFixture.Unused());
        Assert.Equal((ushort)0, first.Windows.Row(0).ReadWord(0x10));
        Assert.Equal((byte)0xAC, first.Windows.Row(0).ReadByte(0x17));
        Assert.Equal((byte)1, first.Windows.Row(0).ReadByte(0x33));
        Assert.Equal(15u, first.Windows.Row(0).ReadDword(0x24));
        Assert.Equal((byte)6, first.Windows.Row(0).ReadByte(0x20));
        NativeBattleScanTextFrame clone = JsonSerializer.Deserialize<NativeBattleScanTextFrame>(JsonSerializer.Serialize(first))!;
        NativeBattleScanTextFrame next = NativeBattleScanTextUpdate.Advance49FEB0(first, 0, 0, 0, ScanTextFixture.Unused());
        NativeBattleScanTextFrame independent = NativeBattleScanTextUpdate.Advance49FEB0(clone, 0, 0, 0, ScanTextFixture.Unused());
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(independent));
        Assert.Equal((byte)0, next.Windows.Row(0).ReadByte(0x33));
        Assert.Equal((byte)0, next.Windows.Row(0).ReadByte(0x28));
    }

    [Fact]
    public void Full_line_scroll_updates_one_pixel_and_returns_until_signed_multiple_of_sixteen()
    {
        NativeBattleScanTextFrame frame = ScanTextFixture.Frame().WithRow(row => row.WriteDword(0x24, 4).WriteByte(0x21, 3).WriteByte(0x23, 3).WriteWord(0x14, 0).WriteWord(0x12, 0xFFFF));
        NativeBattleScanTextFrame result = NativeBattleScanTextUpdate.Advance49FEB0(frame, 0, 0, 0, ScanTextFixture.Unused());
        Assert.Equal((ushort)0, result.Windows.Row(0).ReadWord(0x12));
        Assert.Equal((ushort)4096, result.Windows.Row(0).ReadWord(0x14));
        Assert.Equal((byte)2, result.Windows.Row(0).ReadByte(0x21));
        Assert.Equal(1u, result.Windows.Row(0).ReadDword(0x24));
    }
}

internal static class ScanTextFixture
{
    public static NativeBattleScanTextFrame Frame()
    {
        NativeBattleScanByteOwner row = Owner(60, 0).Write(0, new NativeBattleScanRect(170, 52, 136, 128).ToBytes()).WriteDword(8, 0x100).WriteDword(0x0C, 0x100).WriteByte(0x18, 37).WriteByte(0x16, 1).WriteWord(0x1C, 4096).WriteByte(0x29, 0xFF).WriteByte(0x2A, 0xFF);
        return new(new(Enumerable.Repeat(row, 8).ToImmutableArray(), ImmutableDictionary<byte, NativeBattleScanByteOwner>.Empty.Add(37, Owner(64, 0))), Owner(128, 0), 0xA5, 0, Owner(12, 0xA5));
    }

    public static NativeBattleScanByteOwner Owner(int length, byte value) => new(Enumerable.Repeat(value, length).ToImmutableArray(), Enumerable.Repeat(true, length).ToImmutableArray());
    public static NativeBattleScanTextFrame WithRow(this NativeBattleScanTextFrame frame, Func<NativeBattleScanByteOwner, NativeBattleScanByteOwner> update) => frame with { Windows = frame.Windows with { TextRows = frame.Windows.TextRows.SetItem(0, update(frame.Windows.Row(0))) } };
    public static NativeBattleScanTextServices Unused() => new((_, _) => throw new InvalidDataException("Unexpected expansion."), _ => throw new InvalidDataException("Unexpected source read."), (_, _) => throw new InvalidDataException("Unexpected sound."));
}
