using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGfMorphOffsetsTests
{
    [Fact]
    public void Explicit_shape_offsets_share_the_GTE_products_and_write_XYZ_only_at_the_actual_destination_offset()
    {
        ImmutableArray<byte> first = Shape(2, [new(1, -1, 3), new(32767, -32768, 4)]);
        ImmutableArray<byte> second = Shape(4, [new(1, -1, 3), new(-32768, 32767, -4)]);
        ImmutableArray<byte> prior = Enumerable.Repeat((byte)0xA5, 25).ToImmutableArray();
        ImmutableArray<byte> result = NativeBattleGfMorphGeometry.Morph45E9D0(first, 2, second, 4, 2, 2048, prior, 3);
        Assert.Equal(new NativeBattleVector(0, 0, 4), Vector(result, 3));
        Assert.Equal(new NativeBattleVector(0, 0, 0), Vector(result, 11));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.AsSpan().Slice(9, 2).ToArray());
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.AsSpan().Slice(17, 2).ToArray());
        Assert.All(result.Take(3).Concat(result.Skip(19)), value => Assert.Equal((byte)0xA5, value));
        Assert.All(prior, value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(new NativeBattleVector[] { new(0, 0, 4), new(0, 0, 0) }, NativeBattleGfMorphGeometry.MorphVertices45E9D0(first, 2, second, 4, 2, 2048).ToArray());
    }

    [Fact]
    public void Explicit_shape_count_is_bounded_by_the_selected_six_read_bytes_and_eight_byte_stride()
    {
        ImmutableArray<byte> xyz = [1, 0, 2, 0, 3, 0];
        Assert.Equal(new NativeBattleVector(1, 2, 3), Assert.Single(NativeBattleGfMorphGeometry.MorphVertices45E9D0(xyz, 0, xyz, 0, 1, 0)));
        Assert.Throws<InvalidDataException>(() => NativeBattleGfMorphGeometry.MorphVertices45E9D0(xyz, 0, xyz, 0, 2, 0));
        Assert.Throws<InvalidDataException>(() => NativeBattleGfMorphGeometry.MorphVertices45E9D0(xyz, -1, xyz, 0, 1, 0));
        Assert.Throws<InvalidDataException>(() => NativeBattleGfMorphGeometry.MorphVertices45E9D0(default, 0, xyz, 0, 1, 0));
    }

    private static ImmutableArray<byte> Shape(int offset, NativeBattleVector[] vertices)
    {
        byte[] data = Enumerable.Repeat((byte)0xBC, offset + vertices.Length * 8).ToArray();
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + index * 8), vertices[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + index * 8 + 2), vertices[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + index * 8 + 4), vertices[index].Z);
        }
        return data.ToImmutableArray();
    }
    private static NativeBattleVector Vector(ImmutableArray<byte> data, int offset) => new(BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan().Slice(offset)), BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan().Slice(offset + 2)), BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan().Slice(offset + 4)));
}
