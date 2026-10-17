using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleResourceRowBindingTests
{
    [Theory]
    [InlineData(12, 2)]
    [InlineData(4, 3)]
    [InlineData(15, 0)]
    [InlineData(0, 4)]
    [InlineData(252, 2)]
    public void Original_mask_reads_only_preserved_fields_and_publishes_new_pointer_last(byte mask, int copiedFields)
    {
        const uint skeleton = 0x1D97320;
        const uint oldRow = 0x2100000;
        uint newRow = NativeBattleFileStaging.Address + 0x2A000;
        uint pointer = oldRow;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        List<uint> reads = [];
        List<uint> writes = [];
        NativeBattleResourceRowBinding.Bind502270(skeleton, newRow, mask, address =>
        {
            reads.Add(address);
            if (address == skeleton + 4)
            {
                return pointer;
            }
            int field = checked((int)((address - oldRow) / 4));
            Assert.InRange(field, 0, 3);
            Assert.Equal(0, mask & (1 << field));
            return (uint)(0x12340000 + field);
        }, (address, value) =>
        {
            writes.Add(address);
            if (address == skeleton + 4)
            {
                Assert.Equal(copiedFields, writes.Count - 1);
                pointer = value;
                return;
            }
            Assert.Equal(oldRow, pointer);
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            staging = staging.Write(address, bytes);
        });
        Assert.Equal(newRow, pointer);
        Assert.Equal(copiedFields + 1, reads.Count);
        Assert.Equal(skeleton + 4, writes[^1]);
        for (int field = 0; field < 4; field++)
        {
            uint address = newRow + (uint)field * 4;
            if ((mask & (1 << field)) == 0)
            {
                Assert.Equal((uint)(0x12340000 + field), BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4)));
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => staging.Read(address, 4));
            }
        }
    }

    [Fact]
    public void Overlapping_rows_observe_each_original_read_after_previous_write()
    {
        const uint skeleton = 0x1D97320;
        uint source = NativeBattleFileStaging.Address + 0x2804C;
        byte[] bytes = new byte[20];
        for (int field = 0; field < 5; field++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(field * 4), (uint)(field + 1));
        }
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(source, bytes);
        uint pointer = source;
        NativeBattleResourceRowBinding.Bind502270(skeleton, source + 4, 0, address => address == skeleton + 4 ? pointer : BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4)), (address, value) =>
        {
            if (address == skeleton + 4)
            {
                pointer = value;
                return;
            }
            byte[] word = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(word, value);
            staging = staging.Write(address, word);
        });
        Assert.Equal(source + 4, pointer);
        for (int field = 0; field < 5; field++)
        {
            Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(source + (uint)field * 4, 4)));
        }
    }
}
