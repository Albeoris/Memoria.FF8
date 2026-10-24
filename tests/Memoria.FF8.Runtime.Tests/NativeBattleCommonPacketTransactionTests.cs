using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCommonPacketTransactionTests
{
    [Fact]
    public void PhysicalPacketReadsReuseValidatedImmutableBanksWithoutPerReadSliceCollections()
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400().ApplyCommonWrite(0, [1, 2, 3, 4], Known(4));
        _ = process.ReadCommonPacket(0, 4);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int total = 0;
        for (int index = 0; index < 10000; index++)
        {
            total += process.ReadCommonPacket(0, 4)[0];
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(10000, total);
        Assert.True(allocated < 1_000_000, $"Physical packet reads allocated {allocated} bytes.");
        NativeBattleCommonPacketMemory memory = process.CommonPacketMemory!;
        NativeBattleEffectArena invalid = memory.Bank070Buffer0! with { UnknownRegions = [] };
        Assert.Throws<InvalidDataException>(() => (process with { CommonPacketMemory = memory with { Bank070Buffer0 = invalid } }).ValidateCommonPacketMemory());
        process = JsonSerializer.Deserialize<NativeBattlePrimitiveProcess>(JsonSerializer.Serialize(process))!;
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, process.ReadCommonPacket(0, 4));
        using NativeBattleEffectArenaTransaction transaction = new(memory.Bank070Buffer0!);
        NativeBattlePrimitiveProcess borrowed = process with { CommonPacketMemory = memory with { Bank070Buffer0 = transaction.View } };
        borrowed.ValidateCommonPacketMemory();
        transaction.Dispose();
        Assert.Throws<ObjectDisposedException>(() => borrowed.ValidateCommonPacketMemory());
    }

    [Fact]
    public void ModelProducerStoresPublishOnceAndPreserveTheLatestRendererMetadata()
    {
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400().ApplyCommonWrite(0, Enumerable.Repeat((byte)0xA5, 65536).ToImmutableArray(), Known(65536));
        string original = JsonSerializer.Serialize(source);
        ImmutableArray<NativeBattleCommonPacketStore> stores = [new(20, [1, 2, 3, 4], Known(4))];
        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattlePrimitiveProcess expected = source;
        for (int producer = 0; producer < 16; producer++)
        {
            expected = expected.ApplyCommonWrites(stores) with { ByteCursor = producer * 4 };
        }
        long scalarBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        using NativeBattleCommonPacketTransaction transaction = new(source);
        NativeBattlePrimitiveProcess actual = transaction.View;
        for (int producer = 0; producer < 16; producer++)
        {
            actual = actual.ApplyCommonWrites(stores) with { ByteCursor = producer * 4 };
        }
        NativeBattlePrimitiveProcess frozen = transaction.Freeze(actual);
        long sharedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(sharedBytes < scalarBytes / 3, $"Shared producers allocated {sharedBytes} bytes versus {scalarBytes} bytes copying common banks.");
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(actual));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(frozen));
        Assert.Equal(original, JsonSerializer.Serialize(source));
        Assert.Equal(60, frozen.ByteCursor);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, frozen.ReadCommonPacket(20, 4));
    }

    [Fact]
    public void Canonical_buffer_zero_references_retain_identity_and_buffer_one_keeps_its_physical_alias()
    {
        NativeBattlePrimitiveReference canonical = new("off_B6D070", 0, 37);
        Assert.Same(canonical, NativeBattlePrimitiveProcess.NormalizeCommonReference(canonical));
        NativeBattlePrimitiveReference alias = canonical with { Buffer = 1 };
        NativeBattlePrimitiveReference normalized = NativeBattlePrimitiveProcess.NormalizeCommonReference(alias);
        Assert.Equal(new NativeBattlePrimitiveReference("off_B6D070", 0, 114725), normalized);
        Assert.Same(normalized, NativeBattlePrimitiveProcess.NormalizeCommonReference(alias));
        Assert.Equal(alias, canonical with { Buffer = 1 });
        Assert.Equal(new NativeBattlePrimitiveReference("off_B6D094", 0, 147493), NativeBattlePrimitiveProcess.NormalizeCommonReference(alias with { Bank = "off_B6D094" }));
        Assert.Throws<InvalidDataException>(() => NativeBattlePrimitiveProcess.NormalizeCommonReference(canonical with { Buffer = 2 }));
        Assert.Throws<OverflowException>(() => NativeBattlePrimitiveProcess.NormalizeCommonReference(alias with { ByteOffset = int.MaxValue }));
    }

    [Theory]
    [InlineData("off_B6D070", 0, 114688)]
    [InlineData("off_B6D070", 1, 114688)]
    [InlineData("off_B6D094", 0, 147456)]
    [InlineData("off_B6D094", 1, 147456)]
    public void Ordered_masks_crossing_physical_slices_freeze_to_the_identical_immutable_representation(string bank, byte buffer, int capacity)
    {
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { PacketBank = bank, PacketBuffer = buffer };
        string original = JsonSerializer.Serialize(source);
        NativeBattlePrimitiveProcess expected = source;
        using NativeBattleCommonPacketTransaction transaction = new(source);
        NativeBattlePrimitiveProcess actual = transaction.View;
        Random random = new(37);
        for (int index = 0; index < 80; index++)
        {
            int offset = buffer == 0 && index % 3 == 0 ? capacity - 8 : random.Next(128);
            byte[] bytes = new byte[16];
            random.NextBytes(bytes);
            ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, bytes.Length).Select(_ => (NativeBattleGfPacketByteState)random.Next(3)).ToImmutableArray();
            expected = expected.ApplyCommonWrite(offset, bytes.ToImmutableArray(), mask);
            actual = actual.ApplyCommonWrite(offset, bytes.ToImmutableArray(), mask);
        }
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(transaction.Freeze(actual)));
        Assert.Equal(original, JsonSerializer.Serialize(source));
        Assert.Null(transaction.Freeze(actual).CommonTransaction);
    }

    [Fact]
    public void Read_through_and_failed_callback_rollback_preserve_completed_stores_and_previous_snapshots()
    {
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400().ApplyCommonWrite(0, [1, 2, 3, 4], Known(4));
        string original = JsonSerializer.Serialize(source);
        using NativeBattleCommonPacketTransaction transaction = new(source);
        NativeBattlePrimitiveProcess actual = transaction.View;
        transaction.BeginCheckpoint();
        actual = actual.ApplyCommonWrite(1, [7], Known(1));
        transaction.CommitCheckpoint();
        NativeBattlePrimitiveProcess completed = transaction.Freeze(actual);
        string retained = JsonSerializer.Serialize(completed);
        transaction.BeginCheckpoint();
        actual = actual.ApplyCommonWrite(0, [9], Known(1));
        Assert.Equal(new byte[] { 9, 7, 3, 4 }, actual.ReadCommonPacket(0, 4));
        actual = actual.MarkCommonUnknown(2, 1);
        Assert.Throws<NotSupportedException>(() => actual.ReadCommonPacket(0, 4));
        transaction.RollbackCheckpoint();
        Assert.Equal(new byte[] { 1, 7, 3, 4 }, actual.ReadCommonPacket(0, 4));
        Assert.Equal(retained, JsonSerializer.Serialize(transaction.Freeze(actual)));
        Assert.Equal(retained, JsonSerializer.Serialize(completed));
        Assert.Equal(original, JsonSerializer.Serialize(source));
        transaction.Dispose();
        Assert.Throws<ObjectDisposedException>(() => actual.ReadCommonPacket(0, 1));
    }

    private static ImmutableArray<NativeBattleGfPacketByteState> Known(int count) => Enumerable.Repeat(NativeBattleGfPacketByteState.Known, count).ToImmutableArray();
}
