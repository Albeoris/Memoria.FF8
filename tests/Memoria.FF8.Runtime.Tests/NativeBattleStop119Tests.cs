using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStop119Tests
{
    [Fact]
    public void Partition_uses_signed_centroids_piece_relative_velocity_and_three_CRT_draws_per_face()
    {
        byte[] source = new byte[8 + 8 * 4 + 29 * 20];
        BinaryPrimitives.WriteInt32LittleEndian(source, 8);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(16), 29);
        for (int face = 0; face < 29; face++)
        {
            int record = 20 + face * 20;
            source[record + 3] = 0x24;
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(record + 6), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(record + 8), 4);
        }
        byte[] vertices = new byte[24];
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(0), -10);
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(8), -11);
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(16), -13);
        NativeCrtRandomState random = new(1);
        ImmutableArray<byte> retained = Enumerable.Repeat((byte)0xA5, NativeBattleStop119Geometry.ShardBytes).ToImmutableArray();
        ImmutableArray<byte> shards = NativeBattleStop119Geometry.Partition(retained, 0, source.ToImmutableArray(), 0, vertices.ToImmutableArray(), NativeBattleCuragaMatrices.Identity8DD770(), new(20, 0, 0), ref random);
        Assert.Equal((short)-11, Word(shards, 0));
        Assert.Equal((short)-2, Word(shards, 8));
        Assert.Equal((short)0, Word(shards, 6));
        Assert.Equal((short)1, Word(shards, 24));
        Assert.Equal((short)0, Word(shards, 32));
        Assert.Equal((short)-2, Word(shards, 40));
        Assert.Equal((short)-64, Word(shards, 16));
        Assert.Equal((short)8, Word(shards, 18));
        Assert.Equal((short)-40, Word(shards, 20));
        Assert.Equal((short)0, Word(shards, 22));
        Assert.Equal(0xA5, shards[30]);
        Assert.Equal(retained.AsSpan().Slice(29 * 56).ToArray(), shards.AsSpan().Slice(29 * 56).ToArray());
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 87; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, random);
    }

    [Fact]
    public void Update_keeps_signed_WORD_wrap_saturated_rotation_fade_cap_and_JSON_next()
    {
        byte[] bytes = new byte[NativeBattleStop119Geometry.ShardBytes];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, 32760);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 20);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14), 4000);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24), -17);
        ImmutableArray<byte> advanced = NativeBattleStop119Geometry.Advance(bytes.ToImmutableArray());
        Assert.Equal((short)-32756, Word(advanced, 0));
        Assert.Equal((short)4096, Word(advanced, 14));
        Assert.Equal((short)-17, Word(advanced, 24));
        Assert.Equal((short)256, Word(advanced, 56 + 14));
        ImmutableArray<byte> cold = JsonSerializer.Deserialize<ImmutableArray<byte>>(JsonSerializer.Serialize(advanced));
        Assert.Equal(NativeBattleStop119Geometry.Advance(advanced), NativeBattleStop119Geometry.Advance(cold));
        Assert.Throws<InvalidDataException>(() => NativeBattleStop119Geometry.Advance([]));
    }

    [Fact]
    public void Segments_cover_the_original_shared_buffer_without_overlap()
    {
        Assert.Equal(new[] { 0, 1624, 5880, 18872, 21784, 23464, 25536 }, Enumerable.Range(0, 7).Select(NativeBattleStop119Geometry.GroupOffset).ToArray());
        Assert.Equal(28784, NativeBattleStop119Geometry.ShardBytes);
        Assert.Equal(514, Enumerable.Range(0, 7).Sum(NativeBattleStop119Geometry.GroupCount));
        Assert.Equal(new[] { 1, 2, 4, 5, 6, 7, 8 }, Enumerable.Range(0, 7).Select(NativeBattleStop119Geometry.GeometryIndex).ToArray());
        Assert.Equal(-1, NativeBattleStop119Geometry.Group(4));
    }

    private static short Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset));
}
