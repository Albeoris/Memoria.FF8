using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanInlineIconTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    public void Opcode_five_copies_raw_operand_even_when_operand_matches_a_line_terminator(byte operand)
    {
        byte[] source = [5, operand, 0x44, 2];
        NativeBattleScanTextFrame frame = Frame();
        string before = JsonSerializer.Serialize(frame);
        List<uint> reads = [];
        NativeBattleScanTextFrame result = NativeBattleScanTextExpansion.Expand4B8B30(frame, 0x100, -1, Services(source, reads));
        Assert.Equal(source, result.ExpandedContext128.Bytes.AsSpan().Slice(0, 4).ToArray());
        Assert.Equal(new uint[] { 0x100, 0x101, 0x102, 0x103 }, reads);
        Assert.False(result.ExpandedContext128.Known[4]);
        Assert.Equal((byte)0xA5, result.ExpandedContext128.Bytes[4]);
        Assert.Equal(before, JsonSerializer.Serialize(frame));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    public void Prefix_limit_is_checked_before_opcode_but_not_between_opcode_and_its_operand(int maximum, int readCount)
    {
        List<uint> reads = [];
        NativeBattleScanTextFrame result = NativeBattleScanTextExpansion.Expand4B8B30(Frame(), 0x100, maximum, Services([5, 0x5D, 0x44, 2], reads));
        Assert.Equal(readCount, reads.Count);
        Assert.Equal((uint)(0x100 + readCount - 1), reads[^1]);
        Assert.Equal((byte)0, result.ExpandedContext128.ReadByte(maximum));
        Assert.Equal(maximum + 1, result.ExpandedContext128.Known.Count(known => known));
        Assert.False(result.ExpandedContext128.Known[maximum + 1]);
        if (maximum >= 2)
        {
            Assert.Equal((byte)5, result.ExpandedContext128.ReadByte(0));
            Assert.Equal((byte)0x5D, result.ExpandedContext128.ReadByte(1));
        }
    }

    private static NativeBattleScanTextExpansionServices Services(byte[] source, List<uint> reads)
    {
        return new(pointer =>
        {
            reads.Add(pointer);
            return source[checked((int)(pointer - 0x100))];
        }, _ => throw new InvalidOperationException("An inline icon is a raw pair, not an external name service."));
    }

    private static NativeBattleScanTextFrame Frame()
    {
        NativeBattleScanByteOwner output = new(Enumerable.Repeat((byte)0xA5, 128).ToImmutableArray(), new bool[128].ToImmutableArray());
        return new(new([], ImmutableDictionary<byte, NativeBattleScanByteOwner>.Empty), output, 0xA5, 0, new(new byte[12].ToImmutableArray(), new bool[12].ToImmutableArray()));
    }
}
