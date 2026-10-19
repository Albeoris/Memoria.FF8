using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel215VertexCollapseTests
{
    [Fact]
    public void CollapseCopiesOldRowsAndPreservesPaddingOfInterpolatedAndHiddenRows()
    {
        byte[] mesh = Mesh(80);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(100, Enumerable.Repeat((byte)0xA5, 640).ToArray());
        arena = NativeBattleDuel215Controller.CollapseVertices69C5A0(arena, 100, mesh, 3584);
        Assert.Equal(mesh.AsSpan(12 + 5 * 8, 8).ToArray(), arena.Read(100 + 5 * 8, 8));
        Assert.Equal(mesh.AsSpan(12 + 6 * 8, 6).ToArray(), arena.Read(100 + 6 * 8, 6));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, arena.Read(100 + 6 * 8 + 6, 2));
        Assert.Equal(mesh.AsSpan(12 + 66 * 8, 4).ToArray(), arena.Read(100 + 69 * 8, 4));
        Assert.Equal(mesh.AsSpan(12 + 69 * 8 + 4, 2).ToArray(), arena.Read(100 + 69 * 8 + 4, 2));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, arena.Read(100 + 69 * 8 + 6, 2));
        Assert.Equal(mesh.AsSpan(12 + 70 * 8, 8).ToArray(), arena.Read(100 + 70 * 8, 8));
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 0x40, 0xA5, 0xA5 }, arena.Read(100 + 71 * 8, 8));
    }

    [Fact]
    public void CollapseUsesSignedDivisionTowardZeroAndDoesNotReadUnwrittenPadding()
    {
        byte[] mesh = Mesh(8);
        BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(12), -65);
        BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(12 + 2 * 8), 0);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(100, 64);
        arena = NativeBattleDuel215Controller.CollapseVertices69C5A0(arena, 100, mesh, 512);
        Assert.Equal(-1, BinaryPrimitives.ReadInt16LittleEndian(arena.Read(100, 2)));
        Assert.Throws<NotSupportedException>(() => arena.Read(106, 2));
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 0x40 }, arena.Read(116, 6));
    }

    private static byte[] Mesh(int count)
    {
        byte[] bytes = new byte[12 + count * 8];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), count);
        for (int index = 0; index < count; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + index * 8), checked((short)(index * 7)));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 8), checked((short)(index * -3)));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16 + index * 8), checked((short)(index + 100)));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18 + index * 8), 0x4321);
        }
        return bytes;
    }
}
