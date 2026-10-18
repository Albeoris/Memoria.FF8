using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwareRasterEdgesTests
{
    [Fact]
    public void ReciprocalInitializationOwnsExactly3000SinglePrecisionValuesIncludingZero()
    {
        NativeSoftwareReciprocalTable table = NativeSoftwareReciprocalTable.Initialize463280();
        Assert.Equal(3000, table.Values.Length);
        Assert.Equal(0, BitConverter.SingleToInt32Bits(table.Values[0]));
        Assert.Equal(0x3F800000, BitConverter.SingleToInt32Bits(table.Values[1]));
        Assert.Equal(0x3EAAAAAB, BitConverter.SingleToInt32Bits(table.Values[3]));
        Assert.Equal((float)(1.0 / 2999), table.AtHeight(2999));
        Assert.Throws<NotSupportedException>(() => table.AtHeight(-1));
        Assert.Throws<NotSupportedException>(() => table.AtHeight(3000));
        Assert.Throws<NotSupportedException>(() => new NativeSoftwareReciprocalTable(default).AtHeight(1));
    }

    [Fact]
    public void TexturedSlopeUsesStoredFloatReciprocalBeforeBiasedRoundingAndRetainsOtherRecordBytes()
    {
        NativeSoftwareEdgeBuffers original = Buffers();
        NativeSoftwareEdgeBuffers edge = NativeSoftwareRasterEdges.SetupTextured4632C0(original, new(10, 100, 1000, -1000), new(13, 106, 997, -991), NativeSoftwareReciprocalTable.Initialize463280());
        Assert.Equal(1, Read(edge.Step, 0));
        Assert.Equal(-1, Read(edge.Step, 0xC));
        Assert.Equal(2, Read(edge.Step, 0x10));
        Assert.Equal(0, NativePrecision53.RoundInteger(3.0 / 6));
        Assert.Equal(10, Read(edge.Current, 0));
        Assert.Equal(1000, Read(edge.Current, 0xC));
        Assert.Equal(-1000, Read(edge.Current, 0x10));
        AssertUntouched(original.Current, edge.Current, [0, 0xC, 0x10]);
        AssertUntouched(original.Step, edge.Step, [0, 0xC, 0x10]);
        Assert.All(original.Current, value => Assert.Equal((byte)0xA5, value));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 2)]
    [InlineData(-1, 0)]
    [InlineData(-3, -2)]
    public void ExactHalfSlopesUseTheSharedNativeBiasedRound(int delta, int expected)
    {
        NativeSoftwareEdgeBuffers edge = NativeSoftwareRasterEdges.SetupUntextured463370(Buffers(), new(0, 0, 0, 0), new(delta, 2, 0, 0), NativeSoftwareReciprocalTable.Initialize463280());
        Assert.Equal(expected, Read(edge.Step, 0));
    }

    [Fact]
    public void ComponentAndHeightSubtractionWrapAsDwordsAndZeroHeightWritesZeroSlopes()
    {
        NativeSoftwareReciprocalTable table = NativeSoftwareReciprocalTable.Initialize463280();
        NativeSoftwareEdgeBuffers edge = NativeSoftwareRasterEdges.SetupTextured4632C0(Buffers(), new(int.MaxValue, int.MaxValue, int.MinValue, 99), new(int.MinValue, int.MinValue, int.MaxValue, -99), table);
        Assert.Equal(1, Read(edge.Step, 0));
        Assert.Equal(-1, Read(edge.Step, 0xC));
        Assert.Equal(-198, Read(edge.Step, 0x10));
        Assert.Equal(int.MaxValue, Read(edge.Current, 0));
        NativeSoftwareEdgeBuffers flat = NativeSoftwareRasterEdges.SetupTextured4632C0(Buffers(), new(7, 4, 11, -13), new(99, 4, -33, 35), table);
        Assert.All(new[] { 0, 0xC, 0x10 }, offset => Assert.Equal(0, Read(flat.Step, offset)));
        Assert.Equal(new[] { 7, 11, -13 }, new[] { 0, 0xC, 0x10 }.Select(offset => Read(flat.Current, offset)));
    }

    [Fact]
    public void UntexturedEdgeWritesOnlyXAndRejectsUnknownHeightOrMissingOwnerWithoutAbsOrClamp()
    {
        NativeSoftwareEdgeBuffers original = Buffers();
        NativeSoftwareReciprocalTable table = NativeSoftwareReciprocalTable.Initialize463280();
        NativeSoftwareEdgeBuffers edge = NativeSoftwareRasterEdges.SetupUntextured463370(original, new(-17, 2, 99, 88), new(19, 5, -99, -88), table);
        Assert.Equal(-17, Read(edge.Current, 0));
        Assert.Equal(12, Read(edge.Step, 0));
        AssertUntouched(original.Current, edge.Current, [0]);
        AssertUntouched(original.Step, edge.Step, [0]);
        Assert.Throws<NotSupportedException>(() => NativeSoftwareRasterEdges.SetupTextured4632C0(original, new(0, 4, 0, 0), new(0, 3, 0, 0), table));
        Assert.Throws<NotSupportedException>(() => NativeSoftwareRasterEdges.SetupTextured4632C0(original, new(0, 0, 0, 0), new(0, 3000, 0, 0), table));
        Assert.Throws<InvalidDataException>(() => NativeSoftwareRasterEdges.SetupTextured4632C0(new([0, 0, 0, 0], original.Step), new(), new(), table));
        Assert.Throws<InvalidDataException>(() => NativeSoftwareRasterEdges.SetupUntextured463370(new(default, original.Step), new(), new(), table));
    }

    [Fact]
    public void IndependentJsonRestoreKeepsReciprocalBitsAndEveryUnwrittenByteThroughNextEdge()
    {
        NativeSoftwareReciprocalTable table = NativeSoftwareReciprocalTable.Initialize463280();
        Saved saved = new(table, NativeSoftwareRasterEdges.SetupTextured4632C0(Buffers(), new(1, 0, 3, 5), new(7, 6, 11, 13), table));
        Saved restored = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(saved))!;
        Assert.Equal(saved.Table.Values.Select(BitConverter.SingleToInt32Bits), restored.Table.Values.Select(BitConverter.SingleToInt32Bits));
        NativeSoftwareRasterVertex start = new(-12345, 8, 5678, -9876);
        NativeSoftwareRasterVertex end = new(45678, 15, -1234, 9876);
        Assert.Equal(JsonSerializer.Serialize(NativeSoftwareRasterEdges.SetupTextured4632C0(saved.Buffers, start, end, saved.Table)), JsonSerializer.Serialize(NativeSoftwareRasterEdges.SetupTextured4632C0(restored.Buffers, start, end, restored.Table)));
    }

    private static NativeSoftwareEdgeBuffers Buffers() => new(Enumerable.Repeat((byte)0xA5, 0x40).ToImmutableArray(), Enumerable.Repeat((byte)0xB6, 0x40).ToImmutableArray());
    private static int Read(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static void AssertUntouched(ImmutableArray<byte> before, ImmutableArray<byte> after, int[] offsets)
    {
        Assert.Equal(before.Length, after.Length);
        for (int index = 0; index < before.Length; index++)
        {
            if (!offsets.Any(offset => index >= offset && index < offset + 4))
            {
                Assert.Equal(before[index], after[index]);
            }
        }
    }
    private sealed record Saved(NativeSoftwareReciprocalTable Table, NativeSoftwareEdgeBuffers Buffers);
}
