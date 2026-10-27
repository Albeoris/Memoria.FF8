using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectNodeBankTests
{
    [Fact]
    public void Executable_reads_and_bounded_stores_keep_their_current_owner_without_external_access()
    {
        int calls = 0;
        using NativeBattleEffectNodeBank bank = new(0x1000, new NativeBattleNodeSource([1, 2, 3, 4]), (_, count) =>
        {
            calls++;
            return new byte[count];
        });
        Assert.Equal(new byte[] { 2, 3 }, bank.Read(0x1001, 2));
        bank.Write(0, [4, 3, 2, 1]);
        Assert.Equal(new byte[] { 3, 2 }, bank.Read(0x1001, 2));
        Assert.Equal(new byte[] { 4, 3, 2, 1 }, bank.Read(0x1000, 4));
        Assert.Throws<InvalidDataException>(() => bank.Write(4, [1]));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void External_byte_and_signed_word_tracks_read_exact_addresses_from_the_current_declared_owner()
    {
        byte[] tracks = [0x80, 0xFF, 0x7F];
        List<(uint Address, int Length)> reads = [];
        using NativeBattleEffectNodeBank bank = new(0x1000, new NativeBattleNodeSource([1, 2, 3, 4]), (address, count) =>
        {
            reads.Add((address, count));
            return tracks.AsSpan(checked((int)(address - 0x2000)), count).ToArray();
        });
        Assert.Equal((byte)0x80, Assert.Single(bank.Read(0x2000, 1)));
        Assert.Equal((short)-128, BinaryPrimitives.ReadInt16LittleEndian(bank.Read(0x2000, 2)));
        tracks[0] = 0x01;
        tracks[1] = 0x00;
        Assert.Equal((short)1, BinaryPrimitives.ReadInt16LittleEndian(bank.Read(0x2000, 2)));
        Assert.Equal(new[] { (0x2000u, 1), (0x2000u, 2), (0x2000u, 2) }, reads);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, bank.Read(0x1000, 4));
    }

    [Fact]
    public void Resource_reader_uses_current_local_stores_before_external_immutable_catalog()
    {
        int reads = 0;
        using NativeBattleEffectNodeBank bank = new(0x1000, new NativeBattleNodeSource([1, 2, 3, 4]), readResource: address =>
        {
            Assert.Equal(0x2000u, address);
            reads++;
            return [8, 9];
        });
        bank.WriteByte(1, 7);
        using NativeBattleGfSourceReader local = bank.OpenResourceReader(0x1001);
        Assert.Equal((byte)7, local.Byte(0));
        Assert.Equal(0, reads);
        using NativeBattleGfSourceReader external = bank.OpenResourceReader(0x2000);
        Assert.Equal((byte)8, external.Byte(0));
        Assert.Equal(1, reads);
    }

    [Theory]
    [InlineData(0xFFFu, 2)]
    [InlineData(0x1003u, 2)]
    [InlineData(0x1000u, -1)]
    [InlineData(0xFFFFFFFFu, 2)]
    public void Malformed_or_cross_owner_reads_cannot_escape_through_the_external_provider(uint address, int length)
    {
        int calls = 0;
        using NativeBattleEffectNodeBank bank = new(0x1000, new NativeBattleNodeSource([1, 2, 3, 4]), (_, count) =>
        {
            calls++;
            return new byte[Math.Max(0, count)];
        });
        Assert.Throws<InvalidDataException>(() => bank.Read(address, length));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Missing_owner_remains_strict_and_the_declared_owner_failure_is_preserved()
    {
        using NativeBattleEffectNodeBank local = new(0x1000, new NativeBattleNodeSource([1, 2, 3, 4]));
        Assert.Empty(local.Read(0x1004, 0));
        Assert.Throws<InvalidDataException>(() => local.Read(0x2000, 1));
        NotSupportedException unknown = new("The retained external track is unknown.");
        using NativeBattleEffectNodeBank addressed = new(0x1000, new NativeBattleNodeSource([1, 2, 3, 4]), (_, _) => throw unknown);
        Assert.Same(unknown, Assert.Throws<NotSupportedException>(() => addressed.Read(0x2000, 2)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void External_owner_must_return_the_exact_requested_width(int returnedLength)
    {
        using NativeBattleEffectNodeBank bank = new(0x1000, ImmutableArray.Create<byte>(1, 2, 3, 4), (_, _) => new byte[returnedLength]);
        Assert.Throws<InvalidDataException>(() => bank.Read(0x2000, 2));
    }

    [Fact]
    public void External_owner_cannot_return_an_unknown_null_track()
    {
        using NativeBattleEffectNodeBank bank = new(0x1000, new NativeBattleNodeSource([1, 2, 3, 4]), (_, _) => null!);
        Assert.Throws<InvalidDataException>(() => bank.Read(0x2000, 2));
    }
}
