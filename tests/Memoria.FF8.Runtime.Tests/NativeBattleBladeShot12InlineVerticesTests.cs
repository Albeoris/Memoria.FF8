using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBladeShot12InlineVerticesTests
{
    [Fact]
    public void Inline_override_reads_actual_byte_offsets_and_preserves_nullable_W_without_selected_flag()
    {
        List<int> offsets = [];
        var actual = Draw(0, readInline: offset =>
        {
            offsets.Add(offset);
            return (Points[offset / 8], null);
        });
        Assert.Equal(new[] { 0, 8, 16 }, offsets);
        Assert.Equal(0x1362E44u, BitConverter.ToUInt32(actual.Context.Read(4, 4)));
        Assert.Equal(new[] { 2000, 2000, 2000, 0 }, Assert.Single(actual.Batch.Attempts).Primitive!.VertexPayload4);
        Assert.Null(actual.Memory.GeometryRegisters!.V2[6]);
        Assert.Null(actual.Memory.GeometryRegisters.V2[7]);
    }

    [Fact]
    public void Default_inline_path_uses_resource_vertices_without_callback_and_matches_override()
    {
        var expected = Draw(0);
        var actual = Draw(0, readInline: offset => (Points[offset / 8], (short?)777));
        Assert.Equal(Assert.Single(expected.Batch.Attempts).Primitive!.Words, Assert.Single(actual.Batch.Attempts).Primitive!.Words);
        Assert.Equal(expected.Batch.AllocationBytes, actual.Batch.AllocationBytes);
    }

    [Fact]
    public void Selected_path_keeps_its_owner_guard_and_never_uses_inline_override()
    {
        Assert.Throws<NotSupportedException>(() => Draw(0x2033, readInline: _ => throw new InvalidOperationException()));
        int selected = 0;
        var actual = Draw(0x2033, readInline: _ => throw new InvalidOperationException(), readSelected: offset =>
        {
            selected++;
            return (Points[offset / 8], (short?)55);
        });
        Assert.Equal(3, selected);
        Assert.NotNull(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal(0x30000000u, BitConverter.ToUInt32(actual.Context.Read(4, 4)));
    }

    private static readonly NativeBattleVector[] Points = [new(-400, -400, 0), new(400, -400, 0), new(-400, 400, 0)];
    private static NativeBattleStaticMeshContext88Step Draw(uint flags, Func<int, (NativeBattleVector Vertex, short? W)>? readInline = null, Func<int, (NativeBattleVector Vertex, short? W)>? readSelected = null)
    {
        byte[] bytes = new byte[76];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), Points[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), Points[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 8), 777);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(36), 0x20112233);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(44), 4);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(0x1362E3Cu)).Write(4, BitConverter.GetBytes(0x30000000u)).Write(8, new byte[3]).Write(12, BitConverter.GetBytes(2048)).Write(28, BitConverter.GetBytes(flags));
        return NativeBattleStaticMeshContext88.DrawLoaded(NativeBattleBladeShot12MeshContext88Tests.Memory(2000), new(0, 0x1362E3C, bytes.ToImmutableArray()), new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, selectedVerticesAddress: readSelected is null ? null : 0x30000000, readSelectedVertex: readSelected, readInlineVertex: readInline);
    }
}
