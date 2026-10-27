using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCommonPacketTransactionTests
{
    [Fact]
    public void Late_selected_existing_spill_banks_enlist_in_current_pass_before_public_overlays()
    {
        const int capacity = 147456;
        NativeBattlePrimitiveProcess initial = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = initial };
        NativeBattleOriginalEffectState effect = new(202, 201, new(202, 1, 0), null, null);
        using NativeBattleTaskOperation parent = new(memory, effect);
        NativeBattlePrimitiveProcess selected = (initial with { PacketBank = "off_B6D094", PacketBuffer = 0 }).ApplyCommonWrite(capacity - 4, [1, 2, 3, 4, 5, 6, 7, 8], Known(8));
        NativeBattlePrimitiveProcess published;
        using (NativeBattleTaskOperation callback = new(memory, effect))
        {
            using NativeBattleCommonPacketTransaction transaction = new(selected);
            transaction.BeginCheckpoint();
            NativeBattlePrimitiveProcess current = transaction.View.ApplyCommonWrite(capacity - 4, [9, 10, 11, 12, 13, 14, 15, 16], Known(8));
            transaction.CommitCheckpoint();
            published = transaction.Freeze(current);
            Assert.Equal(new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 }, published.ReadCommonPacket(capacity - 4, 8));
            Assert.Same(selected.CommonPacketMemory!.Bank094Buffer0, published.CommonPacketMemory!.Bank094Buffer0);
            Assert.Same(selected.CommonPacketMemory.Bank094Buffer1, published.CommonPacketMemory.Bank094Buffer1);
        }
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, published.ReadCommonPacket(capacity - 4, 8));
        parent.Commit();
    }

    [Fact]
    public void WholeSliceStoresReuseImmutableReceiptsAndValidateMasksBeforeRanges()
    {
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        NativeBattleCommonPacketStore store = new(0, [1, 2, 3, 4], Known(4));
        ImmutableArray<NativeBattleCommonPacketStore> writes = Enumerable.Repeat(store, 1000).ToImmutableArray();
        _ = source.ApplyCommonWrites([store]);
        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattlePrimitiveProcess result = source.ApplyCommonWrites(writes);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 40000, $"Whole-slice packet stores allocated {allocated} bytes.");
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.ReadCommonPacket(0, 4));
        Assert.Null(source.CommonPacketMemory);
        InvalidDataException invalidMask = Assert.Throws<InvalidDataException>(() => source.ApplyCommonWrites([new(int.MaxValue, [0], [(NativeBattleGfPacketByteState)255])]));
        Assert.Contains("matching data and original byte masks", invalidMask.Message);
        Assert.Same(source, source.ApplyCommonWrites([new(0, [0], [NativeBattleGfPacketByteState.Untouched])]));
        Assert.Throws<InvalidDataException>(() => source.ApplyCommonWrites([new(int.MaxValue, [0], [NativeBattleGfPacketByteState.Untouched])]));
    }

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
        NativeBattleEffectArena invalid = NativeBattleStorageSnapshots.Capture(memory.Bank070Buffer0!) with { UnknownRegions = [] };
        Assert.Throws<InvalidDataException>(() => (process with { CommonPacketMemory = memory with { Bank070Buffer0 = invalid } }).ValidateCommonPacketMemory());
        process = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattlePrimitiveProcess>(SnapshotJson(process))!);
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
        NativeBattlePrimitiveProcess original = NativeBattleStorageSnapshots.Capture(source);
        string originalJson = SnapshotJson(original);
        ImmutableArray<NativeBattleCommonPacketStore> stores = [new(20, [1, 2, 3, 4], Known(4))];
        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattlePrimitiveProcess expected = NativeBattleStorageSnapshots.Restore(original);
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
        Assert.Same(source.CommonPacketMemory!.Bank070Buffer0, frozen.CommonPacketMemory!.Bank070Buffer0);
        Assert.True(sharedBytes < 1_000_000, $"Bounded shared producers allocated {sharedBytes} bytes.");
        Assert.True(scalarBytes < 1_000_000, $"Bounded scalar producers allocated {scalarBytes} bytes.");
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(actual));
        Assert.Equal(SnapshotJson(expected), SnapshotJson(frozen));
        Assert.Equal(originalJson, SnapshotJson(original));
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
        NativeBattlePrimitiveProcess original = NativeBattleStorageSnapshots.Capture(source);
        string originalJson = SnapshotJson(original);
        NativeBattlePrimitiveProcess expected = NativeBattleStorageSnapshots.Restore(original);
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
        Assert.Equal(SnapshotJson(expected), SnapshotJson(transaction.Freeze(actual)));
        Assert.Equal(originalJson, SnapshotJson(original));
        Assert.Null(transaction.Freeze(actual).CommonTransaction);
    }

    [Fact]
    public void Read_through_and_failed_callback_rollback_preserve_completed_stores_and_previous_snapshots()
    {
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400().ApplyCommonWrite(0, [1, 2, 3, 4], Known(4));
        NativeBattlePrimitiveProcess original = NativeBattleStorageSnapshots.Capture(source);
        string originalJson = SnapshotJson(original);
        using NativeBattleCommonPacketTransaction transaction = new(source);
        NativeBattlePrimitiveProcess actual = transaction.View;
        transaction.BeginCheckpoint();
        actual = actual.ApplyCommonWrite(1, [7], Known(1));
        transaction.CommitCheckpoint();
        NativeBattlePrimitiveProcess completed = transaction.Freeze(actual);
        NativeBattlePrimitiveProcess retainedSnapshot = NativeBattleStorageSnapshots.Capture(completed);
        string retained = SnapshotJson(retainedSnapshot);
        transaction.BeginCheckpoint();
        actual = actual.ApplyCommonWrite(0, [9], Known(1));
        Assert.Equal(new byte[] { 9, 7, 3, 4 }, actual.ReadCommonPacket(0, 4));
        actual = actual.MarkCommonUnknown(2, 1);
        Assert.Throws<NotSupportedException>(() => actual.ReadCommonPacket(0, 4));
        transaction.RollbackCheckpoint();
        Assert.Equal(new byte[] { 1, 7, 3, 4 }, actual.ReadCommonPacket(0, 4));
        Assert.Equal(retained, SnapshotJson(transaction.Freeze(actual)));
        Assert.Equal(retained, SnapshotJson(retainedSnapshot));
        Assert.Equal(originalJson, SnapshotJson(original));
        transaction.Dispose();
        Assert.Throws<ObjectDisposedException>(() => actual.ReadCommonPacket(0, 1));
    }

    [Fact]
    public void Current_bank_selection_lazy_callback_banks_and_unpublished_suffix_are_isolated()
    {
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400().ApplyCommonWrite(0, [1, 2, 3, 4], Known(4));
        NativeBattlePrimitiveProcess before = NativeBattleStorageSnapshots.Capture(source);
        NativeBattlePrimitiveProcess published;
        using (NativeBattleCommonPacketTransaction transaction = new(source))
        {
            NativeBattlePrimitiveProcess current = transaction.View;
            transaction.BeginCheckpoint();
            Assert.Throws<InvalidOperationException>(() => transaction.Freeze(current));
            current = current.ApplyCommonWrite(0, [9], Known(1));
            NativeBattlePrimitiveProcess other = current with { PacketBank = "off_B6D094", PacketBuffer = 1, ByteCursor = 31 };
            other.ApplyCommonWrite(0, [8], Known(1));
            Assert.Equal(new byte[] { 8 }, other.ReadCommonPacket(0, 1));
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, source.ReadCommonPacket(0, 4));
            transaction.RollbackCheckpoint();
            Assert.All(other.ReadCommonPacketSnapshot(0, 1).Known, value => Assert.False(value));
            Assert.Null(transaction.Freeze(current).CommonPacketMemory!.Bank094Buffer1);
            transaction.BeginCheckpoint();
            other.ApplyCommonWrite(0, [7], Known(1));
            transaction.CommitCheckpoint();
            published = transaction.Freeze(other);
            Assert.Equal(31, published.ByteCursor);
            Assert.Equal("off_B6D094", published.PacketBank);
            Assert.Equal((byte)1, published.PacketBuffer);
            Assert.Equal(new byte[] { 7 }, published.ReadCommonPacket(0, 1));
            other.ApplyCommonWrite(0, [99], Known(1));
            Assert.Equal(new byte[] { 7 }, published.ReadCommonPacket(0, 1));
        }
        Assert.Equal(new byte[] { 7 }, published.ReadCommonPacket(0, 1));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, before.ReadCommonPacket(0, 4));
    }

    [Fact]
    public void Whole_batch_validation_precedes_overlay_mutation_and_unknown_retains_backing_bytes()
    {
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400().ApplyCommonWrite(0, [1, 2, 3, 4], Known(4));
        using NativeBattleCommonPacketTransaction transaction = new(source);
        NativeBattlePrimitiveProcess current = transaction.View;
        Assert.Throws<InvalidDataException>(() => transaction.Append(current, [new(0, [9], Known(1)), new(int.MaxValue, [8], [(NativeBattleGfPacketByteState)255])]));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, current.ReadCommonPacket(0, 4));
        current.ApplyCommonWrite(1, [99], [NativeBattleGfPacketByteState.Unknown]);
        (byte[] bytes, bool[] known) = current.ReadCommonPacketSnapshot(0, 4);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes);
        Assert.Equal(new[] { true, false, true, true }, known);
        NativeBattlePrimitiveProcess accepted = transaction.Freeze(current);
        Assert.Same(source.CommonPacketMemory!.Bank070Buffer0, accepted.CommonPacketMemory!.Bank070Buffer0);
        Assert.Equal(bytes, accepted.ReadCommonPacketSnapshot(0, 4).Bytes);
        Assert.Equal(known, accepted.ReadCommonPacketSnapshot(0, 4).Known);
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));

    private static ImmutableArray<NativeBattleGfPacketByteState> Known(int count) => Enumerable.Repeat(NativeBattleGfPacketByteState.Known, count).ToImmutableArray();
}
