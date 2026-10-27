using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStorm11Tests
{
    [Fact]
    public void Native_vertex_selector_keeps_the_reverse_index_and_actual_adjacent_group_bytes()
    {
        NativeBattleRawMesh mesh = new([], []) { PackedVertexGroups = [new(0, 2, Packed(new(10, 20, 30), new(40, 50, 60), new(70, 80, 90)))] };
        ImmutableArray<NativeBattleActionMatrix> matrices = [NativeBattleCuragaMatrices.Identity8DD770() with { X = 100 }];
        int index = 0;
        Assert.True(NativeBattleStorm11Vertices.Find6CE880([mesh], 1, matrices, ref index, out NativeBattleVector adjacent));
        Assert.Equal(new(170, 80, 90), adjacent);
        Assert.Equal(2, index);
        index = 2;
        Assert.True(NativeBattleStorm11Vertices.Find6CE880([mesh], 1, matrices, ref index, out NativeBattleVector first));
        Assert.Equal(new(110, 20, 30), first);
        Assert.Equal(0, index);
        Assert.Equal(2, NativeBattleStorm11Vertices.Count69EEE0([mesh], 1));
        Assert.Equal(0, NativeBattleStorm11Vertices.Count69EEE0([mesh], 0));
    }

    [Fact]
    public void Vertex_selector_decrements_only_visible_groups_and_saturates_final_world_coordinates()
    {
        NativeBattleRawMesh hidden = new([], []) { PackedVertexGroups = [new(0, 100, new byte[606].ToImmutableArray())] };
        NativeBattleRawMesh first = new([], []) { PackedVertexGroups = [new(0, 1, Packed(new(1, 2, 3), new(4, 5, 6)))] };
        NativeBattleRawMesh second = new([], []) { PackedVertexGroups = [new(0, 2, Packed(new(100, 200, 300), new(400, 500, 600), new(700, 800, 900)))] };
        int index = 3;
        Assert.True(NativeBattleStorm11Vertices.Find6CE880([hidden, first, second], 6, [NativeBattleCuragaMatrices.Identity8DD770() with { X = 40000, Y = -40000 }], ref index, out NativeBattleVector point));
        Assert.Equal(new(short.MaxValue, short.MinValue, 300), point);
        Assert.Equal(3, NativeBattleStorm11Vertices.Count69EEE0([hidden, first, second], 6));
    }

    [Fact]
    public void Raw_reader_preserves_next_group_header_as_the_native_one_past_vertex()
    {
        byte[] geometry = new byte[48];
        BinaryPrimitives.WriteInt32LittleEndian(geometry, 1);
        BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(8), 2);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(10), 0);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(12), 1);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(14), 10);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(20), 2);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(22), 1);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(24), 100);
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read(geometry, 3);
        int index = 0;
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        Assert.True(NativeBattleStorm11Vertices.Find6CE880(meshes, 1, [identity, identity, identity], ref index, out NativeBattleVector point));
        Assert.Equal(new(2, 1, 100), point);
        Assert.Equal(2, NativeBattleStorm11Vertices.Count69EEE0(meshes, 1));
    }

    [Fact]
    public void Debris_bounce_preserves_overshoot_and_reverses_spin_before_angle_update()
    {
        NativeBattleModulePoolTask task = Task(0x6EB790, 0x30).WriteWord(12, 29).WriteWord(0x12, unchecked((ushort)-1)).WriteWord(0x1A, 1).WriteWord(0x20, 100).WriteWord(0x28, 10);
        NativeBattleModulePoolTask preserved = NativeBattleStorageSnapshots.Capture(task);
        NativeBattleModulePoolTask advanced = NativeBattleStorm11Motion.MoveDebris6EB790(task, out bool ended);
        Assert.True(ended);
        Assert.Equal(20, NativeBattleStorm11Motion.Word(advanced, 0x12));
        Assert.Equal(-41, NativeBattleStorm11Motion.Word(advanced, 0x1A));
        Assert.Equal(-10, NativeBattleStorm11Motion.Word(advanced, 0x28));
        Assert.Equal(90, NativeBattleStorm11Motion.Word(advanced, 0x20));
        Assert.Equal(30, NativeBattleStorm11Motion.Word(advanced, 12));
    }

    [Fact]
    public void Last_sprite_update_wraps_native_words_and_ends_only_after_its_last_draw_frame()
    {
        NativeBattleModulePoolTask task = Task(0x6EBA80, 0x20).WriteWord(12, 12).WriteWord(16, 32767).WriteWord(0x18, 1);
        NativeBattleModulePoolTask preserved = NativeBattleStorageSnapshots.Capture(task);
        NativeBattleModulePoolTask advanced = NativeBattleStorm11Motion.MoveSprite(task, 13, out bool ended);
        Assert.True(ended);
        Assert.Equal(short.MinValue, NativeBattleStorm11Motion.Word(advanced, 16));
        Assert.Equal(12, NativeBattleStorm11Motion.Word(preserved, 12));
        Assert.Equal(13, NativeBattleStorm11Motion.Word(advanced, 12));
    }

    [Fact]
    public void Wobble_preserves_current_fourth_words_and_uses_the_retained_source_for_each_update()
    {
        byte[] original = new byte[308 * 8];
        byte[] current = new byte[original.Length];
        BinaryPrimitives.WriteInt16LittleEndian(original, 100);
        BinaryPrimitives.WriteInt16LittleEndian(original.AsSpan(2), 200);
        BinaryPrimitives.WriteInt16LittleEndian(current.AsSpan(6), -1234);
        ImmutableArray<byte> once = NativeBattleStorm11Motion.Wobble(original.ToImmutableArray(), current.ToImmutableArray(), 0, 0);
        ImmutableArray<byte> twice = NativeBattleStorm11Motion.Wobble(original.ToImmutableArray(), once, 0, 0);
        Assert.Equal((short)100, BinaryPrimitives.ReadInt16LittleEndian(twice.AsSpan()));
        Assert.Equal((short)200, BinaryPrimitives.ReadInt16LittleEndian(twice.AsSpan().Slice(2)));
        Assert.Equal((short)-1234, BinaryPrimitives.ReadInt16LittleEndian(twice.AsSpan().Slice(6)));
        Assert.Equal(once, twice);
    }

    private static NativeBattleModulePoolTask Task(uint callback, int stride) => new(1, 0, null, callback, new byte[stride - 12].ToImmutableArray());

    private static ImmutableArray<byte> Packed(params NativeBattleVector[] points)
    {
        byte[] bytes = new byte[points.Length * 6];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 6), points[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 6 + 2), points[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 6 + 4), points[index].Z);
        }
        return bytes.ToImmutableArray();
    }
}
