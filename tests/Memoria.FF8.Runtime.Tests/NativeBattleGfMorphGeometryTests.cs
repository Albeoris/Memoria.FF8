using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGfMorphGeometryTests
{
    [Fact]
    public void Native_PC_products_round_separately_before_sum_and_preserve_destination_padding()
    {
        ImmutableArray<byte> source = Geometry([new(1, -1, 3), new(7, 9, -9)], [new(1, -1, 3), new(-7, -9, 9)]);
        ImmutableArray<byte> prior = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray();
        ImmutableArray<byte> result = NativeBattleGfMorphGeometry.Morph701390(source, 0, 0, 1, 2048, prior, 4);
        // Round-to-nearest-even independently gives .5+.5 -> 0, -0.5+-0.5 -> 0, 1.5+1.5 -> 4.
        Assert.Equal(new NativeBattleVector(0, 0, 4), Vector(result, 4));
        Assert.Equal(new NativeBattleVector(0, 0, 0), Vector(result, 12));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.AsSpan().Slice(10, 2).ToArray());
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.AsSpan().Slice(18, 2).ToArray());
        Assert.All(result.Take(4).Concat(result.Skip(20)), value => Assert.Equal((byte)0xA5, value));
        Assert.All(prior, value => Assert.Equal((byte)0xA5, value));
    }

    [Theory]
    [InlineData(0, -30000, 30000)]
    [InlineData(4096, 30000, -30000)]
    [InlineData(8192, 32767, -32768)]
    [InlineData(-4096, -32768, 32767)]
    [InlineData(65536, -30000, 30000)]
    public void Ratio_uses_signed_low_WORD_and_saturates_only_final_MAC(int ratio, int expectedX, int expectedY)
    {
        ImmutableArray<byte> source = Geometry([new(-30000, 30000, -1)], [new(30000, -30000, 1)]);
        ImmutableArray<byte> result = NativeBattleGfMorphGeometry.Morph701390(source, 0, 0, 1, ratio, new byte[8].ToImmutableArray());
        NativeBattleVector vector = Vector(result, 0);
        Assert.Equal(expectedX, vector.X);
        Assert.Equal(expectedY, vector.Y);
    }

    [Fact]
    public void Signed_shape_indices_use_observed_bytes_before_geometry_header()
    {
        byte[] source = new byte[48];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(20), 1);
        Put(source, 12, new(-5, 7, 9));
        Put(source, 28, new(11, -13, 15));
        ImmutableArray<byte> result = NativeBattleGfMorphGeometry.Morph701390(source.ToImmutableArray(), 16, -2, 0, 0, new byte[8].ToImmutableArray());
        Assert.Equal(new NativeBattleVector(-5, 7, 9), Vector(result, 0));
    }

    [Fact]
    public void Empty_geometry_does_not_touch_destination_and_unknown_or_short_owners_fail()
    {
        ImmutableArray<byte> source = new byte[12].ToImmutableArray();
        ImmutableArray<byte> prior = [0xA5, 0xB6];
        Assert.Equal(prior, NativeBattleGfMorphGeometry.Morph701390(source, 0, 0, 1, 17, prior));
        Assert.Throws<InvalidDataException>(() => NativeBattleGfMorphGeometry.Morph701390(default, 0, 0, 0, 0, prior));
        ImmutableArray<byte> oneVertex = Geometry([new(1, 2, 3)], [new(4, 5, 6)]);
        Assert.Throws<InvalidDataException>(() => NativeBattleGfMorphGeometry.Morph701390(oneVertex, 0, 0, 1, 0, prior));
        Assert.Throws<InvalidDataException>(() => NativeBattleGfMorphGeometry.Morph701390(oneVertex, 0, 0, 2, 0, new byte[8].ToImmutableArray()));
    }

    private static ImmutableArray<byte> Geometry(NativeBattleVector[] first, NativeBattleVector[] second)
    {
        Assert.Equal(first.Length, second.Length);
        byte[] source = Enumerable.Repeat((byte)0xBC, 12 + first.Length * 16).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), first.Length);
        for (int index = 0; index < first.Length; index++)
        {
            Put(source, 12 + index * 8, first[index]);
            Put(source, 12 + (first.Length + index) * 8, second[index]);
        }
        return source.ToImmutableArray();
    }

    private static void Put(byte[] data, int offset, NativeBattleVector value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), value.X);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 2), value.Y);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 4), value.Z);
    }

    private static NativeBattleVector Vector(ImmutableArray<byte> data, int offset) => new(BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan().Slice(offset)), BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan().Slice(offset + 2)), BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan().Slice(offset + 4)));
}
