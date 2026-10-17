using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStatusCountdownsTests
{
    [Theory]
    [InlineData(-1112, 0x18, 0, -4)]
    [InlineData(-1, 0x18, 0, -4)]
    [InlineData(0, 0x18, 0, -4)]
    [InlineData(19, 0x18, 0, -4)]
    [InlineData(20, 0x19, 0, -4)]
    [InlineData(289, 0x21, 0, -4)]
    [InlineData(290, 0x19, 0x18, -8)]
    [InlineData(2990, 0x22, 0x18, -8)]
    [InlineData(32767, 0x85, 0x1A, -8)]
    public void Signed_timer_rounding_and_unclamped_glyph_bytes_match_native(short timer, int first, int second, int x)
    {
        var draws = NativeBattleStatusCountdowns.Layout50A500(0, 0, 65535, timer, 0xFFFFFF);
        Assert.Equal(2, draws.Length);
        Assert.Equal((byte)first, draws[0].Glyphs[0]);
        Assert.Equal((byte)second, draws[0].Glyphs[1]);
        Assert.Equal((ushort)x, (ushort)draws[0].PackedPosition);
        Assert.Equal(0x10020u, draws[0].TableByteOffset);
        Assert.Equal(0u, draws[1].TableByteOffset);
        Assert.Equal(draws[0].PackedPosition, draws[1].PackedPosition);
        Assert.Equal(draws[0].Glyphs, draws[1].Glyphs);
        Assert.Equal(0xFFFFFFu, draws[0].Color);
        Assert.Equal(0x04FFFFFFu, draws[1].Color);
    }

    [Fact]
    public void Sentinel_skips_only_its_glyphs_but_still_shifts_second_status_line()
    {
        var draws = NativeBattleStatusCountdowns.Layout50A410(0x82000, -1, -1, 7, 0, -1111, 20);
        Assert.Equal(2, draws.Length);
        Assert.Equal(0xFFFDFFFBu, draws[0].PackedPosition);
        Assert.Equal(0xFFu, draws[0].Color);
        Assert.Equal(0x040000FFu, draws[1].Color);
        Assert.Empty(NativeBattleStatusCountdowns.Layout50A500(0, 0, 0, -1111, 0));
    }

    [Fact]
    public void Both_lines_keep_opaque_then_semi_order_and_signed_word_wrap()
    {
        var draws = NativeBattleStatusCountdowns.Layout50A410(0x82000, -32768, -32768, 0, 0, 290, 20);
        Assert.Equal(4, draws.Length);
        Assert.Equal(0x0FFFEFF8u, draws[0].PackedPosition);
        Assert.Equal(0x0FFEEFFCu, draws[2].PackedPosition);
        Assert.Equal(0xFFFFFFu, draws[0].Color);
        Assert.Equal(0x04FFFFFFu, draws[1].Color);
        Assert.Equal(0xFFu, draws[2].Color);
        Assert.Equal(0x040000FFu, draws[3].Color);
    }

    [Fact]
    public void Near_rejection_and_unselected_statuses_do_not_read_unknown_participant_timers()
    {
        Assert.Empty(NativeBattleStatusCountdowns.Layout50A410(0x82000, 0, 0, 0, 0x20000, null, null));
        Assert.Empty(NativeBattleStatusCountdowns.Layout50A410(0x82000, 0, 0, 0, 0x40000, null, null));
        Assert.Empty(NativeBattleStatusCountdowns.Layout50A410(0, 0, 0, 0, 0, null, null));
        Assert.Throws<NotSupportedException>(() => NativeBattleStatusCountdowns.Layout50A410(0x2000, 0, 0, 0, 0, null, null));
        Assert.Throws<NotSupportedException>(() => NativeBattleStatusCountdowns.Layout50A410(0x80000, 0, 0, 0, 0, null, null));
    }

    [Fact]
    public void Countdown_receipt_survives_independent_json_without_timer_state()
    {
        var draws = NativeBattleStatusCountdowns.Layout50A410(0x82000, 23, -23, 100, 0, 290, 19);
        string json = JsonSerializer.Serialize(draws);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleCountdownText[]>(json)));
        Assert.Equal(0xFFFCFFFAu, draws[0].PackedPosition);
        Assert.Equal(0xFFFBFFFEu, draws[2].PackedPosition);
    }

    [Theory]
    [InlineData(0x00123456u, 0xE100041Eu, 0x64123456u)]
    [InlineData(0x03123456u, 0xE100047Eu, 0x64123456u)]
    [InlineData(0x04123456u, 0xE100041Eu, 0x66123456u)]
    [InlineData(0xFF123456u, 0xE100047Eu, 0x66123456u)]
    public void Icon_encoder_stores_all_twenty_four_bytes_including_native_control_and_signed_offsets(uint color, uint mode, uint command)
    {
        byte[] parts = [0x12, 0x34, 0x56, 0xF8, 8, 0xFF, 9, 0x80, 0xAB, 0xCD, 0xEF, 7, 16, 2, 24, 3];
        var packets = NativeMenuIconPackets.Encode4B76A0(parts, 32768, -32768, color, 2);
        Assert.Equal(48, packets.Length);
        ReadOnlySpan<byte> first = packets.AsSpan(0, 24);
        Assert.Equal(0x05000000u, BinaryPrimitives.ReadUInt32LittleEndian(first));
        Assert.Equal(mode, BinaryPrimitives.ReadUInt32LittleEndian(first[4..]));
        Assert.Equal(command, BinaryPrimitives.ReadUInt32LittleEndian(first[8..]));
        Assert.Equal((ushort)32767, BinaryPrimitives.ReadUInt16LittleEndian(first[12..]));
        Assert.Equal((ushort)32640, BinaryPrimitives.ReadUInt16LittleEndian(first[14..]));
        Assert.Equal(0x38583412u, BinaryPrimitives.ReadUInt32LittleEndian(first[16..]));
        Assert.Equal(0x00090008u, BinaryPrimitives.ReadUInt32LittleEndian(first[20..]));
        Assert.Equal((ushort)32770, BinaryPrimitives.ReadUInt16LittleEndian(packets.AsSpan(36, 2)));
        Assert.Equal((ushort)32771, BinaryPrimitives.ReadUInt16LittleEndian(packets.AsSpan(38, 2)));
        Assert.Equal(0x00180010u, BinaryPrimitives.ReadUInt32LittleEndian(packets.AsSpan(44, 4)));
    }

    [Fact]
    public void Icon_empty_parts_match_native_zero_count_and_incomplete_parts_fail()
    {
        Assert.Empty(NativeMenuIconPackets.Encode4B76A0([], 0, 0, 0, 0));
        Assert.Throws<InvalidDataException>(() => NativeMenuIconPackets.Encode4B76A0(new byte[9], 0, 0, 0, 0));
    }
}
