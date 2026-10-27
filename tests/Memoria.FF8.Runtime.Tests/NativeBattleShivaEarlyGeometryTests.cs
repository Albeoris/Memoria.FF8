using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaEarlyGeometryTests
{
    [Fact]
    public void Prefix_radius_and_suffix_direction_preserve_sparse_vertices_negative_rounding_and_retained_aliases()
    {
        byte[] source = new byte[592];
        SetSource(source, 0, 0, 0, 0);
        SetSource(source, 1, 20, 0, 0);
        SetSource(source, 72, 1, -5, 7);
        SetSource(source, 73, 3, -2, 7);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x33000, Enumerable.Repeat((byte)0xA5, 592).ToArray());
        NativeBattleShivaGeometryState retained = new(new(91, 10, 93, 94), new(1, 2, 3, 4));
        NativeBattleShivaGeometryStep result = NativeBattleShivaEarlyGeometry.Regenerate5C1470(arena, retained, source.ToImmutableArray(), 0);
        Assert.Equal(new NativeBattleShivaRetainedVector(7, 4, 7, 1), Read(result.Arena, 72));
        Assert.Equal(new NativeBattleShivaRetainedVector(-4, -13, 7, 1), Read(result.Arena, 73));
        Assert.Equal(new NativeBattleShivaRetainedVector(2, 10, 7, 72), result.State.Current);
        Assert.Equal(new NativeBattleShivaRetainedVector(91, -4, 93, 94), result.State.Previous);
        for (int vertex = 0; vertex < 72; vertex++)
        {
            Assert.Equal(Enumerable.Repeat((byte)0xA5, 6), result.Arena.Read(0x33000 + vertex * 8, 6));
            Assert.Equal(new byte[2], result.Arena.Read(0x33006 + vertex * 8, 2));
        }
        Assert.Equal(new byte[] { 0 }, result.Arena.Read(0x32FFF, 1));
        Assert.Equal(new byte[] { 0 }, result.Arena.Read(0x33250, 1));
    }

    [Fact]
    public void Clearing_only_W_keeps_unknown_prefix_coordinates_and_full_extent_retires_start_index()
    {
        ImmutableArray<byte> source = new byte[592].ToImmutableArray();
        NativeBattleShivaGeometryState state = new(new(1, -4, 3, 72), new(5, 6, 7, 8));
        NativeBattleEffectArena unknown = NativeBattleEffectArena.Clear().MarkUnknown(0x33000, 592);
        NativeBattleShivaGeometryStep suffix = NativeBattleShivaEarlyGeometry.Regenerate5C1470(unknown, state, source, 0);
        Assert.Throws<NotSupportedException>(() => suffix.Arena.Read(0x33000, 6));
        Assert.Equal(new byte[2], suffix.Arena.Read(0x33006, 2));
        Assert.Equal((short)72, suffix.State.Current.W);
        NativeBattleShivaGeometryStep complete = NativeBattleShivaEarlyGeometry.Regenerate5C1470(suffix.Arena, suffix.State, source, 36);
        Assert.Equal((short)0, complete.State.Current.W);
        Assert.Equal((short)72, complete.State.Previous.W);
        for (int index = 0; index < 74; index++)
        {
            Assert.Equal(new NativeBattleShivaRetainedVector(0, 0, 0, 1), Read(complete.Arena, index));
        }
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleShivaGeometryStep>(NativeBattleTestSnapshots.Json(suffix))!;
        NativeBattleShivaGeometryStep continued = NativeBattleShivaEarlyGeometry.Regenerate5C1470(restored.Arena, restored.State, source, 36);
        Assert.Equal(NativeBattleTestSnapshots.Json(complete), NativeBattleTestSnapshots.Json(continued));
    }

    [Fact]
    public void Unsupported_count_missing_source_and_signed_square_overflow_fail_explicitly()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleShivaGeometryState state = new(new(0, 0, 0, 0), new(0, 0, 0, 0));
        ImmutableArray<byte> source = new byte[592].ToImmutableArray();
        Assert.Throws<InvalidDataException>(() => NativeBattleShivaEarlyGeometry.Regenerate5C1470(arena, state, source, -1));
        Assert.Throws<InvalidDataException>(() => NativeBattleShivaEarlyGeometry.Regenerate5C1470(arena, state, source, 37));
        Assert.Throws<InvalidDataException>(() => NativeBattleShivaEarlyGeometry.Regenerate5C1470(arena, state, [], 0));
        byte[] overflow = source.ToArray();
        SetSource(overflow, 0, short.MinValue, 0, 0);
        SetSource(overflow, 1, 20000, 0, 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleShivaEarlyGeometry.Regenerate5C1470(arena, state, overflow.ToImmutableArray(), 0));
    }

    private static NativeBattleShivaRetainedVector Read(NativeBattleEffectArena arena, int index)
    {
        byte[] bytes = arena.Read(0x33000 + index * 8, 8);
        return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)));
    }

    private static void SetSource(byte[] bytes, int index, short x, short y, short z)
    {
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8), x);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8 + 2), y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8 + 4), z);
    }
}
