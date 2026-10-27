using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleNodeBankOperationTests
{
    [Fact]
    public void Constructor_baseline_and_accepted_local_cells_survive_flush_without_observing_external_unread_stores()
    {
        NativeBattleNodeSource source = new(new byte[32].ToImmutableArray());
        using (NativeBattleNodeBankOperation operation = new(source))
        {
            source.Arena.Write(2, [99]);
            Assert.Equal((byte)0, operation.ReadByte(2));
            operation.Write(4, [7]);
            Assert.Equal((byte)0, source.ReadByte(4));
            operation.Publish();
            Assert.Equal((byte)7, source.ReadByte(4));
            Assert.Equal((byte)7, operation.ReadByte(4));
            Assert.Equal((byte)0, operation.ReadByte(2));
            operation.Write(4, [8]);
            Assert.Equal((byte)8, operation.ReadByte(4));
        }
        Assert.Equal((byte)7, source.ReadByte(4));
        Assert.Equal((byte)99, source.ReadByte(2));
    }

    [Fact]
    public void Resource_observation_begins_at_first_selection_and_remains_fixed_across_later_local_and_physical_stores()
    {
        NativeBattleNodeSource source = new(new byte[32].ToImmutableArray());
        using NativeBattleNodeBankOperation operation = new(source);
        operation.Write(0, [1]);
        using NativeBattleGfSourceReader selected = operation.OpenReader(0, 16);
        operation.Write(0, [2]);
        source.Arena.Write(1, [3]);
        Assert.Equal((byte)1, selected.Byte(0));
        Assert.Equal((byte)0, selected.Byte(1));
        using NativeBattleGfSourceReader later = operation.OpenReader(0, 16);
        Assert.Equal((byte)2, later.Byte(0));
        Assert.Equal((byte)0, later.Byte(1));
        operation.Publish();
        Assert.Equal((byte)1, selected.Byte(0));
        Assert.Equal((byte)2, operation.ReadByte(0));
    }

    [Fact]
    public void Bounded_word_store_preserves_unknown_padding_and_untouched_pointer_provenance()
    {
        NativeBattleNodeSource source = new(Enumerable.Range(0, 32).Select(index => (byte)index).ToImmutableArray());
        source.Arena.MarkUnknown(3, 1).WriteResourcePointer(16, new(null, null) { CpuRow = 2 });
        using NativeBattleNodeBankOperation operation = new(source);
        operation.WriteUInt16(0, 0x1234);
        operation.Publish();
        Assert.Equal(0x1234, source.ReadUInt16(0));
        var padding = source.Arena.ReadSnapshot(3, 1);
        Assert.Equal((byte)3, padding.Data[0]);
        Assert.False(padding.Known[0]);
        Assert.Equal(2, source.Arena.ReadResourcePointer(16)!.CpuRow);
    }
    [Fact]
    public void Empty_publication_does_not_accept_other_effect_writes_to_the_same_bank()
    {
        NativeBattleEffectMemory memory = new(null, 0, 0, [], 0, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(1, 0, new(1, 1, 0), null, null);
        NativeBattleNodeSource source = new([0, 0, 0, 0]);
        using (NativeBattleTaskOperation operation = new(memory, effect))
        {
            NativeBattleTaskOperation.AdmitCurrentRetainedEffectStorage(source);
            using NativeBattleNodeBankOperation bank = new(source);
            source.Arena.WriteUInt32(0, 9);
            bank.Publish();
        }
        Assert.Equal(0u, source.ReadUInt32(0));
    }

    [Fact]
    public void Explicit_external_source_acceptance_retains_only_its_prefix_and_not_other_effect_banks()
    {
        NativeBattleEffectMemory memory = new(null, 0, 0, [], 0, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(1, 0, new(1, 1, 0), null, null);
        NativeBattleNodeSource first = new([0, 0, 0, 0]);
        NativeBattleNodeSource second = new([0, 0, 0, 0]);
        using (NativeBattleTaskOperation operation = new(memory, effect))
        {
            NativeBattleTaskOperation.AdmitCurrentRetainedEffectStorage(first);
            NativeBattleTaskOperation.AdmitCurrentRetainedEffectStorage(second);
            using NativeBattleNodeBankOperation bank = new(first);
            bank.WriteUInt32(0, 1);
            second.Arena.WriteUInt32(0, 2);
            bank.Publish(true);
            first.Arena.WriteUInt32(0, 9);
            second.Arena.WriteUInt32(0, 8);
        }
        Assert.Equal(1u, first.ReadUInt32(0));
        Assert.Equal(0u, second.ReadUInt32(0));
    }

}
