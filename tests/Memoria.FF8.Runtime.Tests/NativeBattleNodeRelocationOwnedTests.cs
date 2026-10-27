using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleNodeRelocationOwnedTests
{
    [Fact]
    public void Bounded_relocation_matches_scalar_pointer_order_and_preserves_unknown_nonpointer_bytes()
    {
        byte[] bytes = new byte[0x300];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x104);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x84), 0x280);
        bytes[0x180] = 0xA5;
        byte[] expected = NativeBattleEffectNodeBankFixups.Relocate(bytes.ToImmutableArray(), 0x100000);
        BinaryPrimitives.WriteUInt32LittleEndian(expected, 1);
        NativeBattleNodeSource source = new(bytes.ToImmutableArray());
        source.Arena.MarkUnknown(0x180, 1);
        using (NativeBattleNodeBankOperation operation = new(source))
        {
            NativeBattleEffectNodeBankFixups.Relocate(operation, 0x100000);
            operation.WriteUInt32(0, 1);
            Assert.Equal(0u, source.ReadUInt32(0));
            Assert.Equal(0x104u, source.ReadUInt32(4));
            operation.Publish();
        }
        var actual = source.Arena.ReadSnapshot(0, bytes.Length);
        Assert.Equal(expected, actual.Data);
        Assert.False(actual.Known[0x180]);
        Assert.Equal((byte)0xA5, actual.Data[0x180]);
        Assert.Equal(0x100000u, source.ReadUInt32(0x104 + 0xC8));
    }

    [Fact]
    public void Invalid_node_after_directory_stores_leaves_published_source_unchanged()
    {
        byte[] bytes = new byte[0x300];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x2F0);
        NativeBattleNodeSource source = new(bytes.ToImmutableArray());
        using (NativeBattleNodeBankOperation operation = new(source))
        {
            Assert.Throws<InvalidDataException>(() => NativeBattleEffectNodeBankFixups.Relocate(operation, 0x100000));
        }
        Assert.Equal(bytes, source.Arena.Read(0, bytes.Length));
    }
}
