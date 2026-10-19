using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePandemona291TrailDrawingTests
{
    [Fact]
    public void Packed_lookup_basis_composes_GTE_columns_and_current_view_translation()
    {
        byte[] table = new byte[0x4000];
        BinaryPrimitives.WriteInt16LittleEndian(table.AsSpan(2), 4096);
        byte[] record = new byte[24];
        BinaryPrimitives.WriteInt16LittleEndian(record, 10);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(2), 20);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(4), 30);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(14), 4096);
        NativeBattleActionMatrix basis = NativeBattlePandemona291TrailDrawing.Prepare6F1650(record, table.ToImmutableArray(), Identity with { X = 100, Y = 200, Z = 300 });
        Assert.Equal(Identity.Rotation.ToArray(), basis.Rotation.ToArray());
        Assert.Equal((110, 220, 330), (basis.X, basis.Y, basis.Z));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GT3_retains_original_prefix_and_post_projection_rejected_stores(bool negative, bool near)
    {
        NativeBattleStaticMeshResource mesh = Mesh(negative);
        List<ImmutableArray<byte>> inputs = [];
        NativeBattleGfGeometryBatch batch = NativeBattlePandemona291TrailDrawing.Draw6F17C0(mesh, Identity with { Z = near ? 0 : 1000 }, new(Identity, 100, 160, 100), 0x155, inputs.Add);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Single(inputs);
        Assert.Equal(mesh.Bytes.Skip(8).Take(8), inputs[0]);
        Assert.Equal(0x09000000u, Read(attempt, 0));
        Assert.Equal(0x34123456u, Read(attempt, 4));
        Assert.Equal(0x12345678u, Read(attempt, 12));
        Assert.Equal(0x23456789u, Read(attempt, 24));
        Assert.Equal(0x3456u, Read(attempt, 36));
        foreach (int offset in new[] { 8, 16, 20, 28, 32 })
        {
            Assert.Equal(near ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, attempt.WriteMask[offset]);
        }
        if (near || negative)
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(0, batch.AllocationBytes);
        }
        else
        {
            NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
            Assert.Equal(79, primitive.Bucket);
            Assert.Equal(new[] { 1000, 1000, 1000, 0 }, primitive.VertexPayload4);
            NativeBattleCommonPacketWrite linked = NativeBattleCommonPacketArena.Append(NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400(), NativeBattleLogicalOrdering.Clear(), batch);
            Assert.Single(linked.Ordering.Proxies);
            Assert.Equal(40, linked.Process.ByteCursor);
        }
    }

    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static uint Read(NativeBattleGfGeometryAttempt attempt, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(offset));

    private static NativeBattleStaticMeshResource Mesh(bool negative)
    {
        byte[] data = new byte[88];
        BinaryPrimitives.WriteInt32LittleEndian(data, 32);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 3);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(16), 100);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(26), 100);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(56), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(60), 0x34123456);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(66), negative ? (ushort)4 : (ushort)2);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(68), negative ? (ushort)2 : (ushort)4);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(70), 0x3456);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(72), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(76), 0x23456789);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(80), 0x00445566);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(84), 0x00778899);
        return new(0, 0x139FAF8, data.ToImmutableArray());
    }
}
