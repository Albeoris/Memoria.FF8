using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuake38GroundMemoryTests
{
    private const int Entries = 0x1000;
    private const int Zones = 0xD000;

    [Fact]
    public void Entry_builder_matches_original_values_without_consuming_or_overwriting_unwritten_padding()
    {
        byte[] vertices = new byte[24];
        for (int index = 0; index < 3; index++)
        {
            Word(vertices, index * 8, (short)(index * 20 - 30));
            Word(vertices, index * 8 + 2, 30000);
            Word(vertices, index * 8 + 4, -30000);
        }
        byte[] polygon = new byte[20];
        Word(polygon, 2, 1);
        Word(polygon, 4, 2);
        for (int index = 6; index < polygon.Length; index++)
        {
            polygon[index] = (byte)(index * 7);
        }
        byte[] retained = Enumerable.Repeat((byte)0xA5, 112).ToArray();
        NativeBattleRawPolygon source = new(false, polygon.ToImmutableArray());
        ImmutableArray<byte> expected = NativeBattleQuake38GroundEntries.Build(1, 50, source, vertices.ToImmutableArray(), retained.ToImmutableArray());
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(Entries, retained).MarkUnknown(Entries + 6, 2).MarkUnknown(Entries + 30, 2).MarkUnknown(Entries + 111, 1).WriteResourcePointer(Entries + 60, new(null, 0xA5A5A5A5));
        NativeBattleEffectArena result = NativeBattleQuake38GroundEntries.Build(arena, Entries, 1, 50, source, vertices);
        var actual = result.ReadSnapshot(Entries, 112);
        Assert.Equal(expected.ToArray(), actual.Data);
        foreach (int offset in new[] { 6, 7, 30, 31, 111 })
        {
            Assert.False(actual.Known[offset]);
        }
        Assert.Equal(0xA5A5A5A5u, result.ReadResourcePointer(Entries + 60)!.StagingAddress);
        Assert.Equal(-10, result.ReadInt16(Entries + 24));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuake38GroundEntries.Build(arena, Entries, 1, 50, source, vertices.AsSpan(0, 1)));
    }

    [Fact]
    public void Zone_assignment_counts_each_distinct_bit_once_across_five_native_passes_and_wraps_unsigned_counts()
    {
        byte[] entry = new byte[112];
        Word(entry, 52, 0);
        Word(entry, 54, 1);
        Word(entry, 58, 2);
        byte[] zones = new byte[12];
        Word(zones, 6, -1);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(Entries, entry).Write(Zones, zones).MarkUnknown(Entries + 6, 2).MarkUnknown(Zones + 400, 4);
        NativeBattleEffectArenaSnapshot initial = arena.Capture();
        NativeBattleEffectArena result = NativeBattleQuake38GroundZones.Assign8D9950(arena, Entries, Zones, 0, 0, 1, 3, Resources(), 8192, 8192, 0);
        Assert.Equal((byte)3, result.ReadByte(Entries + 88));
        Assert.Equal((byte)7, result.ReadByte(Entries + 92));
        Assert.Equal((ushort)8, result.ReadUInt16(Zones));
        Assert.Equal((ushort)8, result.ReadUInt16(Zones + 4));
        Assert.Equal((ushort)8, result.ReadUInt16(Zones + 8));
        Assert.Equal((ushort)1, result.ReadUInt16(Zones + 2));
        Assert.Equal((ushort)0, result.ReadUInt16(Zones + 6));
        Assert.Equal((ushort)1, result.ReadUInt16(Zones + 10));
        Assert.Throws<NotSupportedException>(() => result.ReadByte(Entries + 6));
        Assert.Throws<NotSupportedException>(() => result.ReadByte(Zones + 400));
        byte[] beforeRepeat = result.Read(Zones, 12);
        NativeBattleEffectArena repeated = NativeBattleQuake38GroundZones.Assign8D9950(result, Entries, Zones, 0, 0, 1, 3, Resources(), 8192, 8192, 0);
        Assert.Equal(beforeRepeat, repeated.Read(Zones, 12));

        NativeBattleEffectArena invalid = NativeBattleStorageSnapshots.Restore<NativeBattleEffectArena>(initial).MarkUnknown(Zones + 10, 2);
        Assert.Throws<NotSupportedException>(() => NativeBattleQuake38GroundZones.Assign8D9950(invalid, Entries, Zones, 0, 0, 1, 3, Resources(), 8192, 8192, 0));
        Assert.Equal((byte)0, invalid.ReadByte(Entries + 88));
        Assert.Equal((ushort)0, invalid.ReadUInt16(Zones));

        NativeBattleQuake38Resources zeroBit = Resources() with { ZoneMap = Enumerable.Repeat((byte)16, 1024).ToImmutableArray() };
        NativeBattleEffectArena zeroBitResult = NativeBattleQuake38GroundZones.Assign8D9950(NativeBattleStorageSnapshots.Restore<NativeBattleEffectArena>(initial), Entries, Zones, 0, 0, 1, 3, zeroBit, 8192, 8192, 0);
        Assert.Equal((ushort)0, zeroBitResult.ReadUInt16(Zones));
        Assert.Equal((ushort)5, zeroBitResult.ReadUInt16(Zones + 2));
        Assert.Equal((ushort)4, zeroBitResult.ReadUInt16(Zones + 6));
        Assert.Equal((ushort)5, zeroBitResult.ReadUInt16(Zones + 10));
    }

    [Fact]
    public void Adjacency_preserves_native_zero_based_scan_and_reciprocal_intra_entry_store_order()
    {
        NativeBattleEffectArena arena = AdjacencyFixture();
        NativeBattleEffectArena result = NativeBattleQuake38Adjacency.Classify8D95E0(arena, Entries, Zones, 1, 1, 1, 0, 0, 1, 3);
        Assert.Equal((byte)1, result.ReadByte(Entries + 96));
        Assert.Equal((byte)7, result.ReadByte(Entries + 112 + 96));
        Assert.Equal((byte)4, result.ReadByte(Entries + 112 + 97));
        Assert.Equal((byte)2, result.ReadByte(Entries + 112 + 98));
        Assert.Throws<NotSupportedException>(() => result.ReadByte(Entries + 6));
        Assert.Throws<NotSupportedException>(() => result.ReadByte(Entries + 224));
        NativeBattleEffectArena next = NativeBattleQuake38Adjacency.Classify8D95E0(result, Entries, Zones, 1, 1, 1, 1, 1, 2, 3);
        Assert.Equal((byte)7, next.ReadByte(Entries + 112 + 97));
        Assert.Same(next, NativeBattleQuake38Adjacency.Classify8D95E0(next, Entries, Zones, 1, 2, 1, 1, 1, 2, 3));
    }

    [Fact]
    public void Adjacency_required_unknown_after_an_earlier_match_does_not_publish_partial_flags()
    {
        NativeBattleEffectArena arena = AdjacencyFixture().MarkUnknown(Entries + 112 + 89, 1);
        Assert.Throws<NotSupportedException>(() => NativeBattleQuake38Adjacency.Classify8D95E0(arena, Entries, Zones, 1, 1, 1, 0, 0, 1, 3));
        Assert.Equal((byte)0, arena.ReadByte(Entries + 96));
        Assert.Equal((byte)0, arena.ReadByte(Entries + 112 + 96));
    }

    private static NativeBattleEffectArena AdjacencyFixture()
    {
        byte[] entries = new byte[224];
        byte[] zones = new byte[16];
        for (int primitive = 0; primitive < 2; primitive++)
        {
            int offset = primitive * 112;
            entries[offset + 110] = 1;
            for (int vertex = 0; vertex < 4; vertex++)
            {
                Word(entries, offset + 52 + vertex * 2, (short)vertex);
                Word(zones, vertex * 4 + 2, 2);
            }
            for (int edge = 0; edge < 3; edge++)
            {
                entries[offset + 88 + edge] = (byte)(primitive == 0 ? 1 : edge + 2);
            }
        }
        return NativeBattleEffectArena.Clear().Write(Entries, entries).Write(Zones, zones).MarkUnknown(Entries + 6, 2).MarkUnknown(Entries + 224, 0xB600 - 224);
    }

    private static NativeBattleQuake38Resources Resources() => new(null!, null!, null!, null!, null!, Enumerable.Repeat((byte)3, 1024).ToImmutableArray(), Enumerable.Repeat((byte)7, 1024).ToImmutableArray(), [], [], [], [], 0, 0);
    private static void Word(Span<byte> bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes[offset..], value);
}
