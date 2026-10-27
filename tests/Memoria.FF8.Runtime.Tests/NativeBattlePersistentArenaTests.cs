using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePersistentArenaTests
{
    [Fact]
    public void Bounded_source_transaction_pointer_enumeration_reads_committed_base_without_routing_recursion()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        source.WriteResourcePointer(36, new(null, 0x12345678));
        NativeBattleEffectArena bounded = NativeBattleEffectArena.BorrowWorking(source.Working, 32, 16);
        using NativeBattleEffectArenaTransaction transaction = new(bounded);
        transaction.View.WriteResourcePointer(8, new(null, 0x87654321));
        Assert.Equal(new[] { 4, 8 }, bounded.EnumerateResourcePointers().Select(entry => entry.Key).Order().ToArray());
        Assert.Equal(new[] { 4, 8 }, transaction.View.EnumerateResourcePointers().Select(entry => entry.Key).Order().ToArray());
        Assert.Equal(36, Assert.Single(source.EnumerateResourcePointers()).Key);
        transaction.Freeze();
        Assert.Equal(new[] { 36, 40 }, source.EnumerateResourcePointers().Select(entry => entry.Key).Order().ToArray());
    }

    [Fact]
    public void Borrowed_mutations_are_retained_in_the_canonical_owners_detached_data_footprint()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(8, [1]);
        NativeBattleEffectArena view = NativeBattleEffectArena.BorrowWorking(source.Working, 32, 8);
        view.WriteByte(0, 9);
        view.MarkUnknown(2, 2);
        NativeBattleEffectArenaSnapshot frozen = source.Capture();
        Assert.Equal(9, frozen.ReadByte(32));
        Assert.False(frozen.ReadSnapshot(34, 2).Known[0]);
        NativeBattleEffectArena parsed = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize<NativeBattleEffectArena>(frozen))!;
        Assert.Equal(9, parsed.ReadByte(32));
        Assert.Equal(1, parsed.ReadByte(8));
        Assert.False(parsed.ReadSnapshot(34, 2).Known[0]);
        Assert.DoesNotContain(parsed.Regions, region => region.Offset <= 34 && region.Offset + region.Data.Length > 34);
    }

    [Fact]
    public void Borrowed_physical_views_route_public_suffixes_but_base_reads_and_private_writers_keep_committed_bytes()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(32, [1, 2, 3, 4]);
        NativeBattleEffectArena borrowed = NativeBattleEffectArena.BorrowWorking(source.Working, 32, 16);
        using NativeBattleEffectArenaWriter privateWriter = new(source);
        privateWriter.Write(32, [9]);
        using NativeBattleEffectArenaTransaction transaction = new(source);
        Assert.True(NativeBattleEffectArenaTransaction.HasActive(source.Working));
        transaction.View.Write(32, [5, 6]);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, source.Read(32, 4));
        Assert.Equal(new byte[] { 5, 6, 3, 4 }, borrowed.Read(0, 4));
        borrowed.WriteByte(2, 7);
        using (NativeBattleEffectArenaWriter forward = new(borrowed))
        {
            forward.Write(3, [8]);
            Assert.Same(borrowed, forward.Freeze());
            Assert.Throws<InvalidDataException>(() => forward.Write(16, [1]));
        }
        borrowed.WriteResourcePointer(4, new(null, 0x12345678));
        Assert.Equal(0x12345678u, transaction.View.ReadUInt32(36));
        Assert.Equal(4, Assert.Single(borrowed.EnumerateResourcePointers()).Key);
        Assert.False(source.TryGetResourcePointer(36, out _));
        Assert.Throws<InvalidOperationException>(() => source.Capture());
        Assert.Throws<InvalidOperationException>(() => borrowed.Capture());
        Assert.Throws<InvalidOperationException>(() => new NativeBattleEffectArenaTransaction(source));
        Assert.Equal(9, privateWriter.ReadThrough(32, 1)[0]);
        transaction.Freeze();
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, source.Read(32, 4));
        Assert.Equal(16, borrowed.Capture().LogicalLength);
        transaction.BeginCheckpoint();
        Assert.Throws<InvalidOperationException>(() => source.Capture());
        transaction.RollbackCheckpoint();
        source.Capture();
    }

    [Fact]
    public void Partial_physical_overlap_fails_before_stores_and_disposal_removes_routing()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        NativeBattleEffectArena window = NativeBattleEffectArena.BorrowWorking(source.Working, 16, 16);
        NativeBattleEffectArena crossing = NativeBattleEffectArena.BorrowWorking(source.Working, 8, 16);
        using (NativeBattleEffectArenaTransaction transaction = new(window))
        {
            Assert.Throws<NotSupportedException>(() => crossing.Write(0, new byte[16]));
            Assert.Equal(0, source.ReadByte(8));
            window.WriteByte(0, 7);
            Assert.Equal(7, transaction.View.ReadByte(0));
            Assert.Equal(0, source.ReadByte(16));
        }
        Assert.False(NativeBattleEffectArenaTransaction.HasActive(source.Working));
        Assert.Equal(0, window.ReadByte(0));
        crossing.WriteByte(8, 9);
        Assert.Equal(9, source.ReadByte(16));
    }

    [Fact]
    public void Routed_raw_stores_preserve_explicit_knowledge_and_retained_hidden_bytes()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(32, [1, 2, 3, 4]);
        NativeBattleEffectArena borrowed = NativeBattleEffectArena.BorrowWorking(source.Working, 32, 16);
        using NativeBattleEffectArenaTransaction transaction = new(source);
        transaction.WriteWithKnownness(32, [9, 8, 7, 6], [true, false, true, false]);
        transaction.WriteRetainedBytes(32, [5, 4, 3, 2]);
        var staged = borrowed.ReadSnapshot(0, 4);
        Assert.Equal(new byte[] { 5, 4, 3, 2 }, staged.Data);
        Assert.Equal(new[] { true, false, true, false }, staged.Known);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, source.Read(32, 4));
        transaction.Freeze();
        Assert.Equal(staged.Data, source.ReadSnapshot(32, 4).Data);
        Assert.Equal(staged.Known, source.ReadSnapshot(32, 4).Known);
    }

    [Fact]
    public void Bounded_captures_and_json_restore_keep_relative_bounds_and_independent_owners()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.CreateUnknown(64);
        source.Write(8, [1, 2, 3, 4]);
        NativeBattleEffectArenaSnapshot captured = source.Capture();
        Assert.Equal(64, captured.LogicalLength);
        Assert.Throws<InvalidDataException>(() => captured.ReadByte(64));
        string json = JsonSerializer.Serialize<NativeBattleEffectArena>(captured);
        NativeBattleEffectArena parsed = JsonSerializer.Deserialize<NativeBattleEffectArena>(json)!;
        Assert.Equal(64, parsed.LogicalLength);
        Assert.Throws<InvalidDataException>(() => parsed.ReadByte(64));
        NativeBattleEffectArena first = NativeBattleStorageSnapshots.Restore(parsed);
        NativeBattleEffectArena second = NativeBattleStorageSnapshots.Restore(parsed);
        Assert.Throws<InvalidDataException>(() => first.WriteByte(64, 9));
        first.WriteByte(8, 9);
        Assert.Equal(1, second.ReadByte(8));
        Assert.Equal(1, parsed.ReadByte(8));
        Assert.Equal(json, JsonSerializer.Serialize<NativeBattleEffectArena>(captured));
        NativeBattleEffectArena bounded = NativeBattleEffectArena.BorrowWorking(first.Working, 8, 8);
        NativeBattleEffectArenaSnapshot projected = bounded.Capture();
        Assert.Equal(8, projected.LogicalLength);
        Assert.Equal(new byte[] { 9, 2, 3, 4 }, projected.Read(0, 4));
        NativeBattleEffectArena reparsed = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize<NativeBattleEffectArena>(projected))!;
        Assert.Throws<InvalidDataException>(() => reparsed.ReadByte(8));
        Assert.Equal(projected.ReadSnapshot(0, 8).Data, reparsed.ReadSnapshot(0, 8).Data);
        Assert.Equal(projected.ReadSnapshot(0, 8).Known, reparsed.ReadSnapshot(0, 8).Known);
        NativeBattleEffectArena oldDefault = JsonSerializer.Deserialize<NativeBattleEffectArena>("{\"Regions\":[],\"UnknownRegions\":[]}")!;
        Assert.Equal(NativeBattleEffectArena.Length, oldDefault.LogicalLength);
    }

    [Fact]
    public void Warmed_state_scalar_writers_allocate_no_records_temporary_arenas_or_encoded_arrays()
    {
        NativeBattleAcid317State direct = NativeBattleAcid317State.FromColdProcess();
        NativeBattleEffect213State mapped = NativeBattleEffect213State.FromColdProcess();
        for (uint value = 0; value < 2000; value++)
        {
            direct.WriteDword(NativeBattleAcid317State.BaseAddress, value);
            mapped.WriteDword(NativeBattleEffect213Profile.CursorGlobal, value);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (uint value = 0; value < 2000; value++)
        {
            direct.WriteDword(NativeBattleAcid317State.BaseAddress, value);
            mapped.WriteDword(NativeBattleEffect213Profile.CursorGlobal, value);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(1999u, direct.ReadDword(NativeBattleAcid317State.BaseAddress));
        Assert.Equal(1999u, mapped.Storage.ReadUInt32(checked((int)(NativeBattleEffect213Profile.CursorGlobal - NativeBattleEffect213Profile.BaseAddress))));
    }

    [Fact]
    public void Imported_adjacent_data_regions_keep_their_scope_boundaries_until_a_real_store_merges_them()
    {
        NativeBattleEffectArena source = new([new(4, [1]), new(5, [2])]);
        NativeBattleEffectArenaSnapshot frozen = source.Capture();
        Assert.Equal(2, frozen.Regions.Length);
        Assert.Equal(4, frozen.Regions[0].Offset);
        Assert.Equal(5, frozen.Regions[1].Offset);
        NativeBattleEffectArena restored = NativeBattleStorageSnapshots.Restore<NativeBattleEffectArena>(frozen);
        Assert.Equal(2, restored.Capture().Regions.Length);
        restored.WriteByte(4, 1);
        NativeBattleEffectMemoryRegion merged = Assert.Single(restored.Capture().Regions);
        Assert.Equal(4, merged.Offset);
        Assert.Equal(new byte[] { 1, 2 }, merged.Data);
        Assert.Equal(2, frozen.Regions.Length);
    }

    [Fact]
    public void State_owned_stores_and_view_copies_keep_record_identity_but_reject_frozen_storage()
    {
        NativeBattleAcid317State source = NativeBattleAcid317State.FromColdProcess();
        Assert.Same(source, source.WriteDword(NativeBattleAcid317State.BaseAddress, 0x12345678));
        Assert.Same(source, source.WriteWord(NativeBattleAcid317State.BaseAddress + 4, 0xABCD));
        Assert.Same(source, source.WriteView(NativeBattleAcid317State.BaseAddress + 8, NativeBattleEffectArena.Clear().Write(0, [1, 2]), 2));
        Assert.Equal(0x12345678u, source.ReadDword(NativeBattleAcid317State.BaseAddress));
        Assert.Equal((ushort)0xABCD, source.ReadWord(NativeBattleAcid317State.BaseAddress + 4));
        Assert.Equal((ushort)0x0201, source.ReadWord(NativeBattleAcid317State.BaseAddress + 8));
        NativeBattleAcid317State frozen = NativeBattleStorageSnapshots.Capture(source);
        Assert.Throws<InvalidOperationException>(() => frozen.WriteDword(NativeBattleAcid317State.BaseAddress, 7));
        Assert.Equal(0x12345678u, frozen.ReadDword(NativeBattleAcid317State.BaseAddress));
    }

    [Fact]
    public void External_parent_rollback_restores_direct_and_published_overlay_classification_with_its_kernel()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(4, [1, 2]);
        string before = JsonSerializer.Serialize<NativeBattleEffectArena>(source.Capture());
        source.Working.BeginCheckpoint();
        source.WriteByte(8, 7);
        using (NativeBattleEffectArenaTransaction transaction = new(source))
        {
            transaction.View.Write(12, [0xA5, 0xB6]);
            transaction.View.MarkUnknown(5, 1);
            transaction.View.WriteResourcePointer(20, new(null, 0x12345678));
            transaction.Freeze();
        }
        Assert.Equal(7, source.ReadByte(8));
        Assert.Equal(0x12345678u, source.ReadUInt32(20));
        Assert.True(source.TryGetResourcePointer(20, out _));
        source.Working.RollbackCheckpoint();
        Assert.Equal(before, JsonSerializer.Serialize<NativeBattleEffectArena>(source.Capture()));
        Assert.False(source.TryGetResourcePointer(20, out _));
        source.Working.BeginCheckpoint();
        using (NativeBattleEffectArenaTransaction transaction = new(source))
        {
            transaction.View.WriteByte(8, 9);
            transaction.Freeze();
        }
        source.Working.CommitCheckpoint();
        Assert.Equal(9, source.ReadByte(8));
        Assert.Equal(2, source.Capture().Regions.Length);
    }

    [Fact]
    public void Unknown_classification_does_not_invent_data_footprints_and_hidden_written_data_remains_explicit()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.CreateUnknown(64);
        NativeBattleEffectArenaSnapshot empty = arena.Capture();
        Assert.Empty(empty.Regions);
        Assert.Equal(new NativeBattleEffectUnknownRegion(0, 64), Assert.Single(empty.UnknownRegions));
        using (NativeBattleEffectArenaTransaction transaction = new(arena))
        {
            transaction.View.MarkUnknown(4, 8);
            transaction.View.Write(20, [0xA5, 0xB6]);
            transaction.View.MarkUnknown(20, 2);
            transaction.BeginCheckpoint();
            transaction.View.Write(40, [3, 4]);
            transaction.RollbackCheckpoint();
            transaction.Freeze();
        }
        NativeBattleEffectArenaSnapshot frozen = arena.Capture();
        NativeBattleEffectMemoryRegion data = Assert.Single(frozen.Regions);
        Assert.Equal(20, data.Offset);
        Assert.Equal(new byte[] { 0xA5, 0xB6 }, data.Data);
        Assert.Equal(new NativeBattleEffectUnknownRegion(0, 64), Assert.Single(frozen.UnknownRegions));
        NativeBattleEffectArena restored = NativeBattleStorageSnapshots.Restore<NativeBattleEffectArena>(frozen);
        NativeBattleEffectArenaSnapshot recaptured = restored.Capture();
        Assert.Equal(JsonSerializer.Serialize<NativeBattleEffectArena>(frozen), JsonSerializer.Serialize<NativeBattleEffectArena>(recaptured));
    }

    [Fact]
    public void Sparse_global_capture_restore_preserves_unknown_gaps_and_rejects_hidden_out_of_scope_stores()
    {
        NativeBattleAcid317State source = NativeBattleAcid317State.FromUnknown();
        source.Validate();
        NativeBattleAcid317State frozen = NativeBattleStorageSnapshots.Capture(source);
        frozen.Validate();
        Assert.Empty(frozen.Globals.Regions);
        NativeBattleAcid317State restored = NativeBattleStorageSnapshots.Restore(frozen);
        restored.Validate();
        restored.Globals.WriteByte(0x10, 7);
        restored.Globals.MarkUnknown(0x10, 1);
        Assert.Throws<InvalidDataException>(restored.Validate);
        Assert.Empty(frozen.Globals.Regions);
    }

    [Fact]
    public void Publication_group_failure_rolls_back_bytes_knowledge_pointers_and_all_storage_classification()
    {
        NativeBattleEffectArena first = NativeBattleEffectArena.Clear().Write(4, [1, 2]);
        first.WriteResourcePointer(12, new(null, 0x12345678));
        NativeBattleEffectArena second = NativeBattleEffectArena.BorrowWorking(new NativeBattleWorkingMemory(32, maxJournalEntries: 1), 0, 32);
        string before = JsonSerializer.Serialize<NativeBattleEffectArena>(first.Capture());
        using NativeBattleEffectArenaTransaction one = new(first);
        using NativeBattleEffectArenaTransaction two = new(second);
        one.View.Write(4, [9, 8]);
        one.View.MarkUnknown(5, 1);
        one.View.WriteByte(13, 3);
        two.View.Write(0, [1, 2]);
        Assert.Throws<InvalidOperationException>(() => NativeBattleEffectArenaTransaction.CommitGroup(new[] { one, two }));
        Assert.Equal(new byte[] { 9, 8 }, one.View.ReadSnapshot(4, 2).Data);
        one.Dispose();
        two.Dispose();
        Assert.Equal(before, JsonSerializer.Serialize<NativeBattleEffectArena>(first.Capture()));
        Assert.Equal(0, second.StoredLength);
    }

    [Fact]
    public void Guardian_work_bank_keeps_runtime_authority_and_detaches_at_snapshot_boundary()
    {
        NativeBattleGuardianWorkBuffer source = NativeBattleGuardianWorkBuffer.FromColdProcess();
        Assert.Same(source, source.Write(8, [1, 2]));
        NativeBattleGuardianWorkBuffer frozen = NativeBattleStorageSnapshots.Capture(source);
        NativeBattleGuardianWorkBuffer first = NativeBattleStorageSnapshots.Restore(frozen);
        NativeBattleGuardianWorkBuffer second = NativeBattleStorageSnapshots.Restore(frozen);
        first.Write(8, [3, 4]);
        Assert.Equal(new byte[] { 1, 2 }, frozen.Read(8, 2));
        Assert.Equal(new byte[] { 1, 2 }, second.Read(8, 2));
        Assert.Throws<InvalidOperationException>(() => frozen.Write(8, [5, 6]));
        Assert.Throws<InvalidOperationException>(() => _ = source.Regions);
        NativeBattleGuardianWorkBuffer json = JsonSerializer.Deserialize<NativeBattleGuardianWorkBuffer>(JsonSerializer.Serialize(frozen))!;
        Assert.True(json.IsSnapshot);
        Assert.Equal(new byte[] { 1, 2 }, json.Read(8, 2));
    }

    [Fact]
    public void Working_stores_keep_authority_and_only_explicit_captures_are_detached_serializable()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        source.WriteUInt32(12, 0x12345678);
        source.WriteResourcePointer(12, new(null, 0x12345678));
        source.WriteByte(24, 0xA5);
        source.MarkUnknown(24, 1);
        NativeBattleWorkingMemory owner = source.Working;
        NativeBattleEffectArenaSnapshot captured = source.Capture();
        string historical = JsonSerializer.Serialize<NativeBattleEffectArena>(captured);
        source.WriteUInt32(12, 0x87654321);
        Assert.Same(owner, source.Working);
        Assert.Equal(0x12345678u, captured.ReadUInt32(12));
        Assert.Equal((byte)0xA5, captured.ReadSnapshot(24, 1).Data[0]);
        Assert.False(captured.ReadSnapshot(24, 1).Known[0]);
        Assert.True(captured.TryGetResourcePointer(12, out _));
        Assert.False(source.TryGetResourcePointer(12, out _));
        Assert.Equal(historical, JsonSerializer.Serialize<NativeBattleEffectArena>(captured));
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(source));
        Assert.Throws<InvalidOperationException>(() => captured.WriteByte(0, 1));
        Assert.Throws<InvalidOperationException>(() => _ = source.Regions);
        Assert.Throws<InvalidOperationException>(() => _ = source.UnknownRegions);
        NativeBattleEffectArena restoredSnapshot = JsonSerializer.Deserialize<NativeBattleEffectArena>(historical)!;
        Assert.IsType<NativeBattleEffectArenaSnapshot>(restoredSnapshot);
        NativeBattleEffectArena first = NativeBattleStorageSnapshots.Restore(restoredSnapshot);
        NativeBattleEffectArena second = NativeBattleStorageSnapshots.Restore(restoredSnapshot);
        first.WriteUInt32(12, 7);
        Assert.Equal(0x12345678u, second.ReadUInt32(12));
        Assert.Equal(0x12345678u, restoredSnapshot.ReadUInt32(12));
    }

    [Fact]
    public void Overlay_publication_commits_prefix_and_disposal_discards_unpublished_suffix()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(8, [1, 2, 3, 4]);
        NativeBattleEffectArenaSnapshot historical = source.Capture();
        using (NativeBattleEffectArenaTransaction transaction = new(source))
        {
            transaction.View.Write(8, [9, 8]);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, source.Read(8, 4));
            Assert.Equal(new byte[] { 9, 8, 3, 4 }, transaction.View.Read(8, 4));
            Assert.Same(source, transaction.Freeze());
            Assert.Equal(new byte[] { 9, 8, 3, 4 }, source.Read(8, 4));
            transaction.View.Write(9, [7, 6, 5]);
            Assert.Equal(new byte[] { 9, 7, 6, 5 }, transaction.View.Read(8, 4));
            Assert.Equal(new byte[] { 9, 8, 3, 4 }, source.Read(8, 4));
        }
        Assert.Equal(new byte[] { 9, 8, 3, 4 }, source.Read(8, 4));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, historical.Read(8, 4));
        using NativeBattleEffectArenaTransaction next = new(source);
        Assert.Equal(new byte[] { 9, 8, 3, 4 }, next.View.Read(8, 4));
    }

    [Fact]
    public void Checkpoint_rollback_restores_overlapping_hidden_bytes_and_pointer_metadata()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(32, [0xA5, 0xB6, 0xC7, 0xD8]);
        source.WriteResourcePointer(12, new(null, 0x12345678));
        using NativeBattleEffectArenaTransaction transaction = new(source);
        transaction.View.WriteByte(4, 3);
        transaction.BeginCheckpoint();
        transaction.View.WriteByte(13, source.ReadByte(13));
        transaction.View.Write(32, [1, 2, 3, 4]);
        transaction.View.MarkUnknown(33, 2);
        transaction.View.WriteByte(34, 9);
        transaction.View.WriteResourcePointer(40, null);
        Assert.False(transaction.View.TryGetResourcePointer(12, out _));
        Assert.True(transaction.View.TryGetResourcePointer(40, out _));
        transaction.RollbackCheckpoint();
        Assert.True(transaction.View.TryGetResourcePointer(12, out _));
        Assert.False(transaction.View.TryGetResourcePointer(40, out _));
        Assert.Equal(new byte[] { 0xA5, 0xB6, 0xC7, 0xD8 }, transaction.View.Read(32, 4));
        Assert.Equal(3, transaction.View.ReadByte(4));
        Assert.Equal(0, source.ReadByte(4));
        transaction.View.MarkUnknown(32, 4);
        transaction.Freeze();
        var hidden = source.ReadSnapshot(32, 4);
        Assert.Equal(new byte[] { 0xA5, 0xB6, 0xC7, 0xD8 }, hidden.Data);
        Assert.All(hidden.Known, known => Assert.False(known));
    }

    [Fact]
    public void Writers_borrow_transaction_stores_without_needing_their_own_publication()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        using NativeBattleEffectArenaTransaction transaction = new(source);
        using (NativeBattleEffectArenaWriter writer = new(transaction.View))
        {
            writer.Write(48, [1, 2, 3, 4]);
        }
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, transaction.View.Read(48, 4));
        Assert.Equal(new byte[4], source.Read(48, 4));
        transaction.Freeze();
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, source.Read(48, 4));
        using (NativeBattleEffectArenaWriter abandoned = new(source))
        {
            abandoned.Write(48, [9, 8, 7, 6]);
        }
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, source.Read(48, 4));
        Assert.Throws<InvalidOperationException>(() => new NativeBattleEffectArenaTransaction(source));
        transaction.Dispose();
        NativeBattleEffectArenaTransaction disposed = new(source);
        NativeBattleEffectArena oldView = disposed.View;
        disposed.Dispose();
        using NativeBattleEffectArenaTransaction reused = new(source);
        Assert.Throws<ObjectDisposedException>(() => oldView.ReadByte(0));
        Assert.Throws<ObjectDisposedException>(() => oldView.WriteByte(0, 1));
    }

    [Fact]
    public void Bounded_views_capture_one_kernel_and_restore_one_independent_authority()
    {
        NativeBattleWorkingMemory kernel = new(128, false);
        kernel.WriteByte(20, 7);
        Views live = new(NativeBattleEffectArena.BorrowWorking(kernel, 16, 32), NativeBattleEffectArena.BorrowWorking(kernel, 20, 16));
        Views frozen = NativeBattleStorageSnapshots.Capture(live);
        Views first = NativeBattleStorageSnapshots.Restore(frozen);
        Views second = NativeBattleStorageSnapshots.Restore(frozen);
        Assert.Same(first.Left.Working, first.Right.Working);
        Assert.NotSame(first.Left.Working, second.Left.Working);
        Assert.Equal(7, first.Left.ReadByte(4));
        first.Right.WriteByte(0, 9);
        Assert.Equal(9, first.Left.ReadByte(4));
        Assert.Equal(7, second.Left.ReadByte(4));
        Assert.Equal(7, frozen.Left.ReadByte(4));
        Assert.Throws<NotSupportedException>(() => frozen.Left.ReadByte(0));
        Assert.Throws<InvalidDataException>(() => live.Right.ReadByte(16));
        Assert.Throws<InvalidDataException>(() => live.Right.ValidateExtent(0));
    }

    [Fact]
    public void Invalid_operations_do_not_publish_and_warmed_scalar_stores_do_not_allocate_or_capture()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        source.WriteUInt32(4, 3);
        long captured = source.Working.CaptureBytes;
        _ = source.ReadUInt32(4);
        uint total = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            source.WriteUInt32(4, unchecked((uint)index));
            total += source.ReadUInt32(4);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(499500u, total);
        Assert.True(allocated < 1024, $"Warmed scalar memory operations allocated {allocated} bytes.");
        Assert.Equal(captured, source.Working.CaptureBytes);
        uint previous = source.ReadUInt32(4);
        Assert.Throws<InvalidDataException>(() => source.WriteResourcePointer(4, new(null, null)));
        Assert.Throws<InvalidDataException>(() => source.WriteUInt32(NativeBattleEffectArena.Length - 3, 0));
        Assert.Equal(previous, source.ReadUInt32(4));
        using NativeBattleEffectArenaTransaction transaction = new(source);
        source.Working.BeginCheckpoint();
        transaction.View.WriteByte(0, 8);
        Assert.Same(source, transaction.Freeze());
        Assert.Equal(1, source.Working.CheckpointDepth);
        Assert.Equal(8, source.ReadByte(0));
        source.Working.RollbackCheckpoint();
        Assert.Equal(0, source.ReadByte(0));
        Assert.Equal(previous, source.ReadUInt32(4));
        Assert.DoesNotContain(source.Capture().Regions, region => region.Offset == 0);
    }

    [Fact]
    public void Explicit_common_coverage_distinguishes_implicit_zero_holes_and_lazy_unknown_storage()
    {
        NativeBattleEffectArena lazy = NativeBattleEffectArena.CreateUnknown(114688);
        lazy.ValidateExplicitCoverage(114688);
        Assert.Equal(0, lazy.Working.Capacity);
        Assert.Equal(0, lazy.StoredLength);
        NativeBattleEffectArena source = new([new(0, [1, 2]), new(4, [5, 6])]);
        Assert.Throws<InvalidDataException>(() => source.ValidateExplicitCoverage(6));
        NativeBattleEffectArenaSnapshot before = source.Capture();
        Assert.Throws<InvalidDataException>(() => before.ValidateExplicitCoverage(6));
        NativeBattleEffectArena restored = NativeBattleStorageSnapshots.Restore<NativeBattleEffectArena>(before);
        Assert.Throws<InvalidDataException>(() => restored.ValidateExplicitCoverage(6));
        NativeBattleEffectArena parsed = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(before))!;
        Assert.Throws<InvalidDataException>(() => parsed.ValidateExplicitCoverage(6));
        using NativeBattleEffectArenaTransaction transaction = new(source);
        transaction.View.MarkUnknown(2, 2);
        transaction.View.ValidateExplicitCoverage(6);
        Assert.Throws<InvalidDataException>(() => source.ValidateExplicitCoverage(6));
        transaction.Freeze();
        source.ValidateExplicitCoverage(6);
        source.Capture().ValidateExplicitCoverage(6);
        Assert.Throws<InvalidDataException>(() => source.ValidateExplicitCoverage(5));
    }

    [Fact]
    public void Warmed_overlay_reuses_dirty_storage_and_first_touch_checkpoint_journal()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        using NativeBattleEffectArenaTransaction transaction = new(source);
        transaction.BeginCheckpoint();
        for (int index = 0; index < 2000; index++)
        {
            transaction.View.WriteUInt32(32, unchecked((uint)index));
        }
        transaction.RollbackCheckpoint();
        transaction.BeginCheckpoint();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 2000; index++)
        {
            transaction.View.WriteUInt32(32, unchecked((uint)index));
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 1024, $"Warmed overlay stores allocated {allocated} bytes.");
        Assert.Equal(0u, source.ReadUInt32(32));
        Assert.Equal(1999u, transaction.View.ReadUInt32(32));
        transaction.RollbackCheckpoint();
        Assert.Equal(0u, transaction.View.ReadUInt32(32));
    }

    private sealed record Views(NativeBattleEffectArena Left, NativeBattleEffectArena Right);
}
