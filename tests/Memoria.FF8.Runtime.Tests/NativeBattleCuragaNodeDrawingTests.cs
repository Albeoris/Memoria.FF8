using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaNodeDrawingTests
{
    private const int Mesh = NativeBattleCuragaSpawnTests.Node;
    private const int Sprite = Mesh + 672;

    [Theory]
    [InlineData(1, 80)]
    [InlineData(2, 120)]
    public void Complete_sprite_pass_precedes_mesh_pass_and_both_use_primary_physical_bank_with_native_instance_stores(byte instances, int allocation)
    {
        var fixture = Fixture(instances, 1000);
        NativeBattleCuragaNodeDrawStep draw = NativeBattleCuragaNodeKinematics.DrawNodes880390And87ED10(fixture.Frame, fixture.Task, Projection(), 0, NativeBattleLogicalOrdering.Clear());
        Assert.Equal(allocation, draw.Batch.AllocationBytes);
        Assert.Equal(allocation, draw.Frame.State.PrimaryDrawCursor);
        Assert.Equal(0x6000, draw.Frame.State.SecondaryDrawCursor);
        NativeBattleGeometryPrimitive[] primitives = draw.Batch.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => attempt.Primitive!).ToArray();
        Assert.Equal(0x2E808080u, primitives[0].Words[1]);
        Assert.Equal(0x2C202020u, primitives[^1].Words[1]);
        Assert.Equal(Enumerable.Range(0, instances + 1).Select(index => index * 40), draw.Ordering.Proxies.Select(proxy => proxy.Packet.ByteOffset));
        Assert.All(draw.Ordering.Proxies, proxy => Assert.Equal("20DFAB8", proxy.Packet.Bank));
        Assert.Equal(new uint[] { instances == 1 ? 0u : 6u, instances == 1 ? 0u : unchecked((uint)-7), instances == 1 ? 1000u : 1100u }, NativeBattleCuragaEmitterTests.Dwords(draw.Frame, Sprite + 0xC0, 3));
    }

    [Fact]
    public void Rejected_sprite_stores_stay_ordered_at_the_same_cursor_before_the_following_accepted_mesh()
    {
        var fixture = Fixture(1, 0);
        NativeBattleCuragaNodeDrawStep draw = NativeBattleCuragaNodeKinematics.DrawNodes880390And87ED10(fixture.Frame, fixture.Task, Projection(), 0, NativeBattleLogicalOrdering.Clear());
        Assert.Equal(40, draw.Batch.AllocationBytes);
        Assert.Null(draw.Batch.Attempts[0].Primitive);
        Assert.Equal(0, draw.Batch.Attempts[0].ByteOffsetRelative);
        Assert.Equal(NativeBattleGfPacketByteState.Known, draw.Batch.Attempts[0].WriteMask[14]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, draw.Batch.Attempts[0].WriteMask[8]);
        Assert.Equal(0, draw.Batch.Attempts[1].ByteOffsetRelative);
        Assert.NotNull(draw.Batch.Attempts[1].Primitive);
        Assert.Equal(0x2C202020u, Assert.Single(draw.Ordering.Packets).Words[1]);
    }

    private static (NativeBattleCuragaFrame Frame, uint Task) Fixture(byte instances, int depth)
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaFrame frame = NativeBattleCuragaNodes.AllocateGeometry882080(fixture.Frame, fixture.Task, NativeBattleCuragaSpawnTests.Emitter, 0).Frame;
        NativeBattleCuragaNodeAllocation emitter = NativeBattleCuragaNodes.AllocateEmitter881700(frame, fixture.Task, 0, 1, 0);
        frame = NativeBattleCuragaNodes.AllocateGeometry882080(emitter.Frame, fixture.Task, emitter.ArenaOffset!.Value, 1).Frame;
        frame = NativeBattleCuragaSpawnTests.Bank(frame, data =>
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), NativeBattleCuragaExecutableState.Address + 0x300);
            data[0x316] = 0;
            int mesh = 0x400;
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(mesh), 40);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(mesh + 4), 4);
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(mesh + 8 + corner * 8), corner % 2 == 0 ? (short)-100 : (short)100);
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(mesh + 10 + corner * 8), corner < 2 ? (short)-100 : (short)100);
            }
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(mesh + 52), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(mesh + 56), 0x2C202020);
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(mesh + 60 + corner * 2), (ushort)(corner * 2));
            }
            int sprite = 0x800;
            data[sprite + 4] = 255;
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(sprite + 8), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(sprite + 10), 16);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(sprite + 16), 1);
            data[sprite + 20] = 16;
            data[sprite + 21] = 128;
            data[sprite + 22] = 8;
            data[sprite + 23] = 0x2E;
        });
        frame = Node(frame, Mesh, 1, 1000, NativeBattleCuragaExecutableState.Address + 0x400);
        frame = Node(frame, Sprite, instances, depth, NativeBattleCuragaExecutableState.Address + 0x800);
        frame = frame with { State = frame.State with { PrimaryDrawCursor = 0, SecondaryDrawCursor = 0x6000 } };
        return (frame, fixture.Task);
    }

    private static NativeBattleCuragaFrame Node(NativeBattleCuragaFrame frame, int node, byte count, int depth, uint resource)
    {
        byte[] matrix = new byte[32];
        BinaryPrimitives.WriteInt16LittleEndian(matrix, 4096);
        BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(8), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(16), 4096);
        BinaryPrimitives.WriteInt32LittleEndian(matrix.AsSpan(28), depth);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(node + 0xAC, matrix).Write(node + 0x1D8, [count]) } };
        frame = NativeBattleCuragaEmitterTests.Dwords(frame, node + 0x170, [resource]);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(node + 0x19C, [4, 0, 251, 255, 232, 3, 0, 0, 6, 0, 249, 255, 76, 4, 0, 0]) } };
        return frame;
    }

    private static NativeBattleCameraProjection Projection() => new(NativeBattleCuragaMatrices.Identity8DD770(), 216, 160, 108);
}
