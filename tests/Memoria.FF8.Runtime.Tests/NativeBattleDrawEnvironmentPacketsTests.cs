using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDrawEnvironmentPacketsTests
{
    [Fact]
    public void Restore_writes_five_commands_and_preserves_low_links_and_unwritten_warm_suffix()
    {
        byte[] environment = new byte[92];
        Word(environment, 4, 320);
        Word(environment, 6, 216);
        Word(environment, 8, -1);
        Word(environment, 10, -1024);
        Word(environment, 0x14, 10);
        ImmutableArray<byte> retained = Enumerable.Repeat((byte)0xA5, 64).ToImmutableArray();
        ImmutableArray<byte> result = NativeBattleDrawEnvironmentPackets.Restore45C0F0(retained, environment.ToImmutableArray());
        Assert.Equal(new uint[] { 0x05A5A5A5, 0xE3000000, 0xE4035D3F, 0xE52007FF, 0xE100000A, 0xE2000000 }, Enumerable.Range(0, 6).Select(index => Dword(result, index * 4)).ToArray());
        Assert.Equal(retained.AsSpan().Slice(24).ToArray(), result.AsSpan().Slice(24).ToArray());
        Assert.All(retained, value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(0, environment[0x18]);
    }

    [Fact]
    public void Optional_clear_is_in_same_packet_and_repeated_nonclear_keeps_its_prior_words()
    {
        byte[] environment = new byte[92];
        Word(environment, 0, -9);
        Word(environment, 2, 511);
        Word(environment, 4, 17);
        Word(environment, 6, -2);
        environment[0x18] = 7;
        environment[0x19] = 0x12;
        environment[0x1A] = 0x34;
        environment[0x1B] = 0x56;
        ImmutableArray<byte> original = Enumerable.Repeat((byte)0xBC, 64).ToImmutableArray();
        ImmutableArray<byte> clear = NativeBattleDrawEnvironmentPackets.Restore45C0F0(original, environment.ToImmutableArray());
        Assert.Equal(0x08BCBCBCu, Dword(clear, 0));
        Assert.Equal(0x02563412u, Dword(clear, 24));
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(environment), Dword(clear, 28));
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(environment.AsSpan().Slice(4)), Dword(clear, 32));
        Assert.Equal(original.AsSpan().Slice(36).ToArray(), clear.AsSpan().Slice(36).ToArray());
        environment[0x18] = 0;
        ImmutableArray<byte> next = NativeBattleDrawEnvironmentPackets.Restore45C0F0(clear, environment.ToImmutableArray());
        Assert.Equal(0x05BCBCBCu, Dword(next, 0));
        Assert.Equal(clear.AsSpan().Slice(24).ToArray(), next.AsSpan().Slice(24).ToArray());
        ImmutableArray<byte> restored = JsonSerializer.Deserialize<ImmutableArray<byte>>(JsonSerializer.Serialize(clear));
        Assert.Equal(next.ToArray(), NativeBattleDrawEnvironmentPackets.Restore45C0F0(restored, environment.ToImmutableArray()).ToArray());
    }

    [Fact]
    public void Window_uses_unsigned_width_low_byte_height_and_flags_replace_complete_words()
    {
        byte[] environment = new byte[92];
        Word(environment, 0xC, 0xF8);
        Word(environment, 0xE, 0xA8);
        Word(environment, 0x10, 64);
        Word(environment, 0x12, 128);
        Word(environment, 0x14, -1);
        environment[0x16] = 2;
        environment[0x17] = 4;
        ImmutableArray<byte> result = NativeBattleDrawEnvironmentPackets.Restore45C0F0(new byte[64].ToImmutableArray(), environment.ToImmutableArray());
        Assert.Equal(0xE1000FFFu, Dword(result, 16));
        Assert.Equal(0xE20AFE18u, Dword(result, 20));
        environment[0x13] = 0xFE;
        Assert.Equal(result.ToArray(), NativeBattleDrawEnvironmentPackets.Restore45C0F0(new byte[64].ToImmutableArray(), environment.ToImmutableArray()).ToArray());
    }

    [Fact]
    public void Area_and_offset_keep_actual_padding_and_previous_link_bits()
    {
        ImmutableArray<byte> retained = Enumerable.Repeat((byte)0xCD, 16).ToImmutableArray();
        ImmutableArray<byte> area = NativeBattleDrawEnvironmentPackets.Area45C940(retained, 704, 256, 128, 128);
        Assert.Equal(0x02CDCDCDu, Dword(area, 0));
        Assert.Equal(0xE30402C0u, Dword(area, 4));
        Assert.Equal(0xE405FF3Fu, Dword(area, 8));
        Assert.Equal(retained.AsSpan().Slice(12).ToArray(), area.AsSpan().Slice(12).ToArray());
        ImmutableArray<byte> offset = NativeBattleDrawEnvironmentPackets.Offset45C9B0(retained, -1, -1024);
        Assert.Equal(0x01CDCDCDu, Dword(offset, 0));
        Assert.Equal(0xE52007FFu, Dword(offset, 4));
        Assert.Equal(retained.AsSpan().Slice(8).ToArray(), offset.AsSpan().Slice(8).ToArray());
    }

    [Theory]
    [InlineData(-1, -1, 0, 0, 0xE407FBFEu)]
    [InlineData(1023, 511, 2, 2, 0xE4000000u)]
    public void Area_wraps_zero_signed_extents_and_inclusive_end_before_masks(short x, short y, short width, short height, uint expectedEnd)
    {
        ImmutableArray<byte> result = NativeBattleDrawEnvironmentPackets.Area45C940(new byte[12].ToImmutableArray(), x, y, width, height);
        Assert.Equal(expectedEnd, Dword(result, 8));
    }

    [Fact]
    public void Required_raw_owners_cannot_be_replaced_with_missing_or_short_records()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleDrawEnvironmentPackets.Restore45C0F0(default, new byte[92].ToImmutableArray()));
        Assert.Throws<InvalidDataException>(() => NativeBattleDrawEnvironmentPackets.Restore45C0F0(new byte[64].ToImmutableArray(), new byte[91].ToImmutableArray()));
        Assert.Throws<InvalidDataException>(() => NativeBattleDrawEnvironmentPackets.Restore45C0F0(new byte[65].ToImmutableArray(), new byte[92].ToImmutableArray()));
        Assert.Throws<InvalidDataException>(() => NativeBattleDrawEnvironmentPackets.Area45C940(new byte[11].ToImmutableArray(), 0, 0, 1, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleDrawEnvironmentPackets.Offset45C9B0(new byte[7].ToImmutableArray(), 0, 0));
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    private static void Word(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan().Slice(offset), value);
}
