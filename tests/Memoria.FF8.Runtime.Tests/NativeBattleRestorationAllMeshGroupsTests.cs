using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRestorationAllMeshGroupsTests
{
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 24)]
    [InlineData(2, 32)]
    [InlineData(3, 40)]
    [InlineData(4, 28)]
    [InlineData(5, 36)]
    [InlineData(6, 40)]
    [InlineData(7, 52)]
    public void All_eight_native_groups_preserve_allocation_fade_and_signed_depth_bias(int group, int allocation)
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleEsunaMeshContext context = new(0xF0, 0, 2048, -4, 0, 0, full, full, 17, 2);
        NativeBattleGfGeometryBatch result = NativeBattleRestorationMeshPackets.EmitBatch88E240(Mesh(group), matrix, new(matrix, 216, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(allocation, result.AllocationBytes);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Attempts);
        Assert.NotNull(packet.Primitive);
        Assert.Equal((group & 1) == 0 ? 271 : 272, packet.Primitive.Bucket);
        Assert.Equal(0x00404040u, packet.Primitive.Words[1] & 0xFFFFFF);
        Assert.Equal((uint)(allocation / 4 - 1) << 24, packet.Primitive.Words[0]);
    }

    [Fact]
    public void Flat_quad_rejection_keeps_prefix_and_does_not_read_the_fourth_corner()
    {
        NativeBattleModuleProjectedVertex Read(int index) => index == 3 ? throw new InvalidOperationException("Rejected flat quad must not read its fourth source.") : new(0, 4096, 0x20000);
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleGfGeometryBatch result = NativeBattleRestorationMeshPackets.EmitF4_88E590(Face(1), Read, new(0x30, null, null, 0, 0, 0, full, full, 17, 2));
        Assert.Equal(0, result.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.All(attempt.WriteMask.Take(8), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.All(attempt.WriteMask.Skip(8), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
    }

    [Fact]
    public void Gouraud_textured_triangle_uses_actual_nonzero_UV_window_bracketing()
    {
        NativeBattleModuleProjectedVertex[] points = [new(XY(100, 100), 4096, 0), new(XY(300, 100), 4096, 0), new(XY(100, 300), 4096, 0)];
        NativeBattleGfGeometryBatch result = NativeBattleRestorationMeshPackets.EmitGt3_88F750(Face(6), index => points[index], new(0x30, null, null, 0, 1, 2, new(0, 0, 256, 256), new(0, 0, 128, 128), 17, 2));
        Assert.Equal(64, result.AllocationBytes);
        Assert.Equal(new[] { 0, 40, 0, 52 }, result.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Equal(0xE2000000u, result.Attempts[1].Primitive!.Words[1]);
        Assert.Equal(0xE2000210u, result.Attempts[3].Primitive!.Words[1]);
    }

    private static ImmutableArray<byte> Mesh(int selected)
    {
        byte[] face = Face(selected);
        byte[] mesh = new byte[40 + 32 + face.Length];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 40);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 4);
        short[] coordinates = [-256, -256, 0, 256, -256, 0, -256, 256, 0, 256, 256, 0];
        for (int vertex = 0; vertex < 4; vertex++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + vertex * 8 + axis * 2), coordinates[vertex * 3 + axis]);
            }
        }
        int cursor = 40;
        for (int group = 0; group < 8; group++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(cursor), group == selected ? 1 : 0);
            cursor += 4;
            if (group == selected)
            {
                face.CopyTo(mesh, cursor);
                cursor += face.Length;
            }
        }
        return mesh.ToImmutableArray();
    }

    private static byte[] Face(int group)
    {
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] face = new byte[strides[group]];
        uint code = (uint)(0x20 + (group & 1) * 8 + ((group >> 1) & 1) * 4 + (group >> 2) * 16);
        BinaryPrimitives.WriteUInt32LittleEndian(face, (code << 24) | 0x808080);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(8), 4);
        if ((group & 1) != 0)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 6);
        }
        if ((group & 2) != 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x12340201);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0xABCD0403);
            if ((group & 1) == 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0x0605);
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x08070605);
            }
        }
        if ((group & 4) != 0)
        {
            int start = (group & 2) == 0 ? 12 : (group & 1) == 0 ? 20 : 24;
            for (int index = 1; index < ((group & 1) == 0 ? 3 : 4); index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(start + (index - 1) * 4), 0x808080);
            }
        }
        return face;
    }

    private static uint XY(short x, short y) => unchecked((ushort)x) | (uint)unchecked((ushort)y) << 16;
}
