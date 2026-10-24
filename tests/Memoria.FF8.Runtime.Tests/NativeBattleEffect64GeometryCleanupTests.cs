using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect64GeometryCleanupTests
{
    [Fact]
    public void Cleanup_writes_current_triangle_and_quad_marker_words_in_object_order()
    {
        const uint address = 0x5000;
        byte[] bytes = Enumerable.Repeat((byte)0xA5, 128).ToArray();
        PutDword(0, 2);
        PutDword(4, 12);
        PutDword(8, 84);
        PutWord(12, 1);
        PutWord(14, 0);
        PutWord(16, 1);
        PutWord(24, 1);
        PutWord(26, 1);
        PutWord(84, 0);
        PutWord(88, 0);
        PutWord(90, 1);
        PutWord(50, 0x8201);
        PutWord(66, 0xFFFF);
        PutWord(114, 0x9234);
        byte[] before = bytes.ToArray();
        List<uint> writes = [];
        NativeBattleEffectArena temporary = NativeBattleEffect64GeometryCleanup.Clear(address, (source, length) => NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan(checked((int)(source - address)), length)), (destination, value) =>
        {
            writes.Add(destination);
            PutWord(checked((int)(destination - address)), value);
        }, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
        Assert.Equal(new uint[] { address + 50, address + 66, address + 114 }, writes);
        Assert.Equal((ushort)0x0201, Word(50));
        Assert.Equal((ushort)0x7FFF, Word(66));
        Assert.Equal((ushort)0x1234, Word(114));
        Assert.Equal(address + 100, BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)));
        Assert.Equal(ushort.MaxValue, BinaryPrimitives.ReadUInt16LittleEndian(temporary.Read(4, 2)));
        Assert.Equal(ushort.MaxValue, BinaryPrimitives.ReadUInt16LittleEndian(temporary.Read(6, 2)));
        foreach (int offset in new[] { 50, 66, 114 })
        {
            BinaryPrimitives.WriteUInt16LittleEndian(before.AsSpan(offset), (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(before.AsSpan(offset)) & 0x7FFF));
        }
        Assert.Equal(before, bytes);

        void PutDword(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
        void PutWord(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    }

    [Fact]
    public void Empty_geometry_retains_unknown_temporary_bytes_and_performs_no_writes()
    {
        NativeBattleEffectArena result = NativeBattleEffect64GeometryCleanup.Clear(0x5000, (_, length) => NativeBattleEffectArena.Clear().Write(0, new byte[length]), (_, _) => throw new InvalidOperationException("Empty geometry has no marker stores."), NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
        Assert.All(result.ReadSnapshot(0, 8).Known, known => Assert.False(known));
    }
}
