using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRawGeometryRelocationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EverySuccessfulPaletteSlotRetainsExactPlainBytesAndIndependentPredecessors(bool quad)
    {
        for (byte slot = 0; slot < 15; slot++)
        {
            byte[] source = Polygon(quad, (ushort)((227 << 6) | 5));
            NativeBattleRawPolygon polygon = new(quad, source.ToImmutableArray());
            ImmutableArray<byte> first = NativeBattleRawGeometryPackets.Relocate507550(polygon, Binding(3, slot));
            byte[] expected = source.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(10), (ushort)((source[10] & 63) | (slot + 224) << 6));
            ushort page = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(14));
            BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(14), (ushort)((page & 0xFFE0) | (slot < 12 ? slot / 2 + 10 : slot - 2)));
            int vertical = slot < 12 ? (slot & 1) << 7 : 128;
            foreach (int position in quad ? new[] { 9, 13, 17, 19 } : new[] { 7, 9, 13 })
            {
                expected[position] = (byte)((source[position] & 127) | vertical);
            }
            Assert.Equal(expected, first);
            Assert.Equal(source, polygon.Bytes);
            ImmutableArray<byte> second = NativeBattleRawGeometryPackets.Relocate507550(polygon, Binding(3, (byte)((slot + 1) % 15)));
            Assert.Equal(expected, first);
            Assert.NotEqual(first, second);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ColoredGroupsAndNonNativeClutsRetainOriginalAllocationWithOriginalValidationOrder(bool quad)
    {
        NativeBattleModelTextureBinding invalid = new(default);
        NativeBattleRawPolygon colored = new(quad, new byte[(quad ? 20 : 16) + 4].ToImmutableArray());
        Assert.True(colored.Bytes.Equals(NativeBattleRawGeometryPackets.Relocate507550(colored, invalid)));
        foreach (ushort clut in new ushort[] { (ushort)(223 << 6), (ushort)(240 << 6), (ushort)((224 << 6) | 16) })
        {
            NativeBattleRawPolygon polygon = new(quad, Polygon(quad, clut).ToImmutableArray());
            Assert.True(polygon.Bytes.Equals(NativeBattleRawGeometryPackets.Relocate507550(polygon, Binding(0, null))));
            Assert.Throws<InvalidDataException>(() => NativeBattleRawGeometryPackets.Relocate507550(polygon, invalid));
        }
        NativeBattleRawPolygon native = new(quad, Polygon(quad, 224 << 6).ToImmutableArray());
        Assert.Throws<NotSupportedException>(() => NativeBattleRawGeometryPackets.Relocate507550(native, Binding(0, null)));
        Assert.Throws<InvalidDataException>(() => NativeBattleRawGeometryPackets.Relocate507550(native, Binding(0, 15)));
        Assert.Throws<InvalidDataException>(() => NativeBattleRawGeometryPackets.Relocate507550(new(quad, new byte[(quad ? 20 : 16) - 1].ToImmutableArray()), Binding(0, 0)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnchangedRelocationAndNonNativeClutsAllocateNoPerPolygonBuffers(bool quad)
    {
        NativeBattleModelTextureBinding binding = Binding(0, 0);
        NativeBattleRawPolygon source = new(quad, Polygon(quad, 224 << 6).ToImmutableArray());
        NativeBattleRawPolygon unchanged = source with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(source, binding) };
        NativeBattleRawPolygon foreign = source with { Bytes = Polygon(quad, 0).ToImmutableArray() };
        _ = NativeBattleRawGeometryPackets.Relocate507550(unchanged, binding);
        _ = NativeBattleRawGeometryPackets.Relocate507550(foreign, binding);
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool retained = true;
        for (int index = 0; index < 1000; index++)
        {
            retained &= unchanged.Bytes.Equals(NativeBattleRawGeometryPackets.Relocate507550(unchanged, binding));
            retained &= foreign.Bytes.Equals(NativeBattleRawGeometryPackets.Relocate507550(foreign, binding));
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(retained);
        Assert.True(allocated < 16 * 1024, $"Unchanged relocations allocated {allocated} bytes.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedRelocationAllocatesOnlyOneOwnedPlainPolygonBuffer(bool quad)
    {
        NativeBattleModelTextureBinding binding = Binding(3, 5);
        NativeBattleRawPolygon source = new(quad, Polygon(quad, 227 << 6).ToImmutableArray());
        _ = NativeBattleRawGeometryPackets.Relocate507550(source, binding);
        long before = GC.GetAllocatedBytesForCurrentThread();
        uint checksum = 0;
        for (int index = 0; index < 1000; index++)
        {
            checksum += NativeBattleRawGeometryPackets.Relocate507550(source, binding)[10];
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.NotEqual(0u, checksum);
        Assert.True(allocated < 56 * 1024, $"Changed relocations allocated {allocated} bytes.");
    }

    private static NativeBattleModelTextureBinding Binding(int row, byte? slot)
    {
        byte?[] rows = new byte?[16];
        rows[row] = slot;
        return new(rows.ToImmutableArray());
    }

    private static byte[] Polygon(bool quad, ushort clut)
    {
        byte[] bytes = Enumerable.Range(0, quad ? 20 : 16).Select(index => (byte)(index * 13 + 3)).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), clut);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 0xB5F4);
        return bytes;
    }
}
