using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFieldRegistrationMemoryTests
{
    private const uint Address = 0x1900000;

    [Fact]
    public void Abandoned_registration_does_not_rebase_or_publish_prepared_bytes()
    {
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().Rebase(Address).Write(Address + 16, [1, 2, 3, 4]);
        string original = SnapshotJson(source);
        using (NativeFieldRegistrationMemory memory = new(source, Address - 8, Address + 32))
        {
            memory.Write8(Address, 9);
            NativeBattleFileStaging preview = memory.Prepare();
            Assert.Equal((byte)9, preview.ReadByte(Address));
            Assert.Equal(Address, source.BaseAddress);
            Assert.Equal(original, SnapshotJson(source));
            Assert.Throws<InvalidOperationException>(() => memory.Write8(Address, 7));
        }
        Assert.Equal(original, SnapshotJson(source));
        using NativeBattleFileStagingTransaction next = new(source);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, next.View.Read(Address + 16, 4));
    }

    [Fact]
    public void Borrowed_registration_keeps_prior_outer_stores_and_discards_only_its_suffix()
    {
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().Rebase(Address);
        using NativeBattleFileStagingTransaction outer = new(source);
        outer.View.WriteByte(Address + 4, 6);
        using (NativeFieldRegistrationMemory memory = new(outer.View, Address - 8, Address + 32))
        {
            memory.Write8(Address + 4, 9);
            memory.Prepare();
            Assert.Equal((byte)9, outer.View.ReadByte(Address + 4));
            Assert.False(AllKnown(source, Address + 4, 1));
        }
        Assert.Equal((byte)6, outer.View.ReadByte(Address + 4));
        Assert.Equal(Address, outer.View.BaseAddress);
        using (NativeFieldRegistrationMemory memory = new(outer.View, Address, Address + 32))
        {
            memory.Write8(Address + 8, 7);
            Assert.Same(outer.View, memory.Finish());
        }
        Assert.Equal((byte)7, outer.View.ReadByte(Address + 8));
        Assert.False(AllKnown(source, Address + 8, 1));
        outer.Freeze(outer.View);
        Assert.Equal((byte)6, source.ReadByte(Address + 4));
        Assert.Equal((byte)7, source.ReadByte(Address + 8));
    }

    [Fact]
    public void Registration_preserves_unknown_backing_and_invalidates_only_changed_pointer_fields()
    {
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().Rebase(Address).Write(Address, [0xA1, 0xB2, 0xC3, 0xD4]).MarkUnknown(Address, 4);
        NativeBattleResourcePointer first = new(null, 0x12345678, 8);
        NativeBattleResourcePointer second = new(null, 0x87654321, 16);
        source.WriteResourcePointer(Address + 8, first).WriteResourcePointer(Address + 16, second);
        NativeBattleFileStaging detached = NativeBattleStorageSnapshots.Capture(source);
        using (NativeFieldRegistrationMemory memory = new(source, Address, Address + 32))
        {
            memory.Copy(Address, Address + 4, 4);
            memory.Write8(Address + 9, 0x99);
            Assert.Same(source, memory.Finish());
        }
        (byte[] bytes, bool[] known) = source.ReadSnapshot(Address + 4, 4);
        Assert.Equal(new byte[] { 0xA1, 0xB2, 0xC3, 0xD4 }, bytes);
        Assert.All(known, value => Assert.False(value));
        Assert.Equal(second, source.ReadResourcePointer(Address + 16));
        Assert.False(source.TryGetResourcePointerMetadata(Address + 8, out _));
        Assert.Equal(first, detached.ReadResourcePointer(Address + 8));
        Assert.Equal(new byte[4], detached.ReadSnapshot(Address + 4, 4).Data);
        NativeBattleFileStaging roundTrip = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(SnapshotJson(source))!);
        Assert.Equal(bytes, roundTrip.ReadSnapshot(Address + 4, 4).Data);
        Assert.Equal(known, roundTrip.ReadSnapshot(Address + 4, 4).Known);
    }

    [Fact]
    public void Constructor_failure_does_not_leave_a_borrowed_checkpoint_open()
    {
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().Rebase(Address);
        using NativeBattleFileStagingTransaction outer = new(source);
        Assert.Throws<OverflowException>(() => new NativeFieldRegistrationMemory(outer.View, Address, uint.MaxValue));
        outer.BeginCheckpoint();
        outer.View.WriteByte(Address, 7);
        outer.RollbackCheckpoint();
        Assert.False(AllKnown(outer.View, Address, 1));
    }

    [Fact]
    public void Parent_rollback_after_child_registration_finish_restores_all_overlay_state()
    {
        NativeBattleFileStaging source = new([]) { BaseAddress = Address, LoaderZeroRanges = [new(Address, 64)] };
        NativeBattleResourcePointer pointer = new(null, 0x12345678, 16);
        source.WriteResourcePointer(Address + 8, pointer);
        string original = SnapshotJson(source);
        using NativeBattleFileStagingTransaction outer = new(source);
        outer.View.WriteByte(Address + 28, 77);
        outer.BeginCheckpoint();
        outer.View.MarkUnknown(Address + 24, 1);
        using (NativeFieldRegistrationMemory memory = new(outer.View, Address - 8, Address + 64))
        {
            Assert.Equal(2, outer.CheckpointDepth);
            memory.Copy(Address + 24, Address + 20, 1);
            memory.Write8(Address + 9, 0xCC);
            memory.Finish();
        }
        Assert.Equal(1, outer.CheckpointDepth);
        Assert.Equal(Address - 8, outer.View.BaseAddress);
        Assert.False(outer.View.TryGetResourcePointerMetadata(Address + 8, out _));
        Assert.False(AllKnown(outer.View, Address + 20, 1));
        Assert.Equal(original, SnapshotJson(source));
        Assert.Throws<InvalidOperationException>(() => outer.Freeze(outer.View));
        outer.RollbackCheckpoint();
        Assert.Equal(0, outer.CheckpointDepth);
        Assert.Equal(Address, outer.View.BaseAddress);
        Assert.Equal(pointer, outer.View.ReadResourcePointer(Address + 8));
        Assert.True(AllKnown(outer.View, Address + 20, 5));
        Assert.Equal((byte)77, outer.View.ReadByte(Address + 28));
        outer.Freeze(outer.View);
        Assert.Equal(pointer, source.ReadResourcePointer(Address + 8));
        Assert.Equal((byte)77, source.ReadByte(Address + 28));
    }

    [Fact]
    public void Nested_marks_restore_overlaps_loader_zero_and_effective_pointer_entries_in_reverse_order()
    {
        NativeBattleFileStaging source = new([]) { BaseAddress = Address, LoaderZeroRanges = [new(Address, 64)] };
        NativeBattleResourcePointer first = new(null, 0x12345678, 8);
        NativeBattleResourcePointer second = new(null, 0x87654321, 16);
        source.WriteResourcePointer(Address + 8, first);
        using NativeBattleFileStagingTransaction transaction = new(source);
        transaction.BeginCheckpoint();
        Assert.Throws<InvalidOperationException>(() => transaction.BeginCheckpoint());
        transaction.View.WriteResourcePointer(Address + 16, second);
        int child = transaction.BeginNestedCheckpoint();
        transaction.View.WriteByte(Address + 9, 0x99);
        transaction.View.WriteByte(Address + 9, 0x88);
        transaction.View.ForgetLoaderZero(Address + 24, 4);
        transaction.View.Rebase(Address - 8);
        Assert.Equal(new[] { Address + 16 }, transaction.View.EnumerateResourcePointers().Select(pair => pair.Key));
        int grandchild = transaction.BeginNestedCheckpoint();
        transaction.View.WriteResourcePointer(Address + 16, first);
        transaction.View.MarkUnknown(Address + 28, 2);
        Assert.Throws<InvalidOperationException>(() => transaction.CommitNestedCheckpoint(child));
        transaction.RollbackNestedCheckpoint(grandchild);
        Assert.Equal(second, transaction.View.ReadResourcePointer(Address + 16));
        Assert.True(AllKnown(transaction.View, Address + 28, 2));
        transaction.CommitNestedCheckpoint(child);
        Assert.False(AllKnown(transaction.View, Address + 24, 4));
        transaction.RollbackCheckpoint();
        Assert.Equal(Address, transaction.View.BaseAddress);
        Assert.Equal(first, transaction.View.ReadResourcePointer(Address + 8));
        Assert.True(AllKnown(transaction.View, Address + 24, 6));
        Assert.Equal(new[] { Address + 8 }, transaction.View.EnumerateResourcePointers().Select(pair => pair.Key));
        Assert.Throws<InvalidDataException>(() => transaction.View.SetResourcePointerMetadata(Address, new(null, null, -1)));
    }

    [Fact]
    public void Sparse_registration_allocations_do_not_scale_with_the_retained_extent()
    {
        uint distant = Address + 0x4000000;
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().Rebase(Address).Write(distant, [17]);
        // Warm the transaction lease before measuring the registration's own window allocation.
        using (NativeFieldRegistrationMemory warmup = new(source, Address, Address + 32))
        {
            warmup.Write8(Address, 1);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        using (NativeFieldRegistrationMemory memory = new(source, Address, Address + 32))
        {
            memory.Write8(Address, 9);
            Assert.Equal((byte)17, memory.Read8(distant));
            memory.Write8(distant, 19);
            memory.Finish();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 1024 * 1024, $"Sparse registration allocated {allocated} bytes for two accessed pages.");
        Assert.Equal((byte)9, source.ReadByte(Address));
        Assert.Equal((byte)19, source.ReadByte(distant));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    public void Overlapping_copies_across_pages_preserve_unknown_backing(int from, int to)
    {
        byte[] original = Enumerable.Range(0, 0x9000).Select(index => (byte)index).ToArray();
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().Rebase(Address).Write(Address, original);
        source.MarkUnknown(Address + 0x3FFF, 4);
        (byte[] expected, bool[] known) = source.ReadSnapshot(Address, original.Length);
        expected.AsSpan(from, 0x8000).CopyTo(expected.AsSpan(to, 0x8000));
        known.AsSpan(from, 0x8000).CopyTo(known.AsSpan(to, 0x8000));
        using (NativeFieldRegistrationMemory memory = new(source, Address, Address + (uint)original.Length))
        {
            memory.Copy(Address + (uint)from, Address + (uint)to, 0x8000);
            Assert.Null(memory.ReadOptional16(Address + 0x3FFF + (uint)to - (uint)from));
            memory.Finish();
        }
        (byte[] actual, bool[] observed) = source.ReadSnapshot(Address, original.Length);
        Assert.Equal(expected, actual);
        Assert.Equal(known, observed);
    }

    [Fact]
    public void Observed_cpu_banks_preserve_untouched_pointer_receipts_and_reconcile_only_stores()
    {
        const uint bankAddress = 0x196B018;
        NativeBattleResourcePointer preserved = new(null, 0x12345678, 8);
        NativeBattleResourcePointer overwritten = new(null, 0x87654321, 16);
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().Rebase(Address);
        source.WriteResourcePointer(bankAddress + 8, preserved);
        source.WriteResourcePointer(bankAddress + 16, overwritten);
        source.WriteByte(bankAddress + 24, 99);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0].Write(8, source.Read(bankAddress + 8, 4)).Write(16, source.Read(bankAddress + 16, 4));
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } };
        NativeBattleModelCpuResources? reconciled;
        using (NativeFieldRegistrationMemory memory = new(source, Address, bankAddress + 3 * NativeBattleModelCpuBank.Length))
        {
            memory.ObserveCpuBanks(cpu);
            Assert.Same(cpu, memory.ReconcileCpuBanks(cpu));
            Assert.Throws<NotSupportedException>(() => memory.Read8(bankAddress + 24));
            memory.Write8(bankAddress + 17, 0xCC);
            memory.Copy(bankAddress + 24, bankAddress + 28, 1);
            reconciled = memory.ReconcileCpuBanks(cpu);
            memory.Finish();
        }
        Assert.NotNull(reconciled);
        Assert.Equal((byte)0xCC, reconciled.Cpu.Banks[0].Read(17, 1)[0]);
        Assert.Equal(preserved, source.ReadResourcePointer(bankAddress + 8));
        Assert.False(source.TryGetResourcePointerMetadata(bankAddress + 16, out _));
        Assert.False(AllKnown(source, bankAddress + 24, 1));
        Assert.Equal((byte)99, source.ReadSnapshot(bankAddress + 24, 1).Data[0]);
    }

    private static bool AllKnown(NativeBattleFileStaging memory, uint address, int length) => memory.ReadSnapshot(address, length).Known.All(value => value);

    private static string SnapshotJson(NativeBattleFileStaging source) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(source));
}
