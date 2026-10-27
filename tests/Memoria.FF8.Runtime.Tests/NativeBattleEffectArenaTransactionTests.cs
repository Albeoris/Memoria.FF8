using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectArenaTransactionTests
{
    [Fact]
    public void Small_source_can_read_zero_holes_and_borrow_a_writer_at_the_full_arena_boundary()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(3, [1, 2, 3]);
        NativeBattleEffectArena expected = NativeBattleStorageSnapshots.Restore<NativeBattleEffectArena>(source.Capture());
        using NativeBattleEffectArenaTransaction transaction = new(source);
        Assert.Equal(new byte[32], transaction.View.Read(64, 32));
        Assert.Equal(new byte[8], transaction.View.Read(NativeBattleEffectArena.Length - 8, 8));
        using NativeBattleEffectArenaWriter borrowed = new(transaction.View);
        borrowed.Write(NativeBattleEffectArena.Length - 4, [9, 8, 7, 6]);
        Assert.Same(transaction.View, borrowed.Freeze());
        Assert.Equal(new byte[] { 0, 0, 0, 0, 9, 8, 7, 6 }, transaction.View.Read(NativeBattleEffectArena.Length - 8, 8));
        Assert.Equal(new byte[] { 1, 2, 3 }, transaction.View.Read(3, 3));
        Assert.Equal(SnapshotJson(expected.Write(NativeBattleEffectArena.Length - 4, [9, 8, 7, 6])), SnapshotJson(transaction.Freeze()));
        Assert.Throws<InvalidDataException>(() => transaction.View.Write(NativeBattleEffectArena.Length, [1]));
        Assert.Throws<InvalidDataException>(() => transaction.View.Read(NativeBattleEffectArena.Length, 1));
    }

    [Fact]
    public void Growth_after_checkpoint_restores_bytes_unknown_spans_and_zero_holes_on_rollback()
    {
        int high = NativeBattleEffectArena.Length - 16;
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(4, [1, 2, 3, 4]).MarkUnknown(high + 4, 4);
        using NativeBattleEffectArenaTransaction transaction = new(source);
        transaction.BeginCheckpoint();
        transaction.View.Write(5, [8, 9]);
        transaction.View.Write(high, [10, 11, 12, 13, 14, 15, 16, 17]);
        transaction.View.MarkUnknown(high + 8, 2);
        var changed = transaction.View.ReadSnapshot(high, 12);
        Assert.Equal(new byte[] { 10, 11, 12, 13, 14, 15, 16, 17, 0, 0, 0, 0 }, changed.Data);
        Assert.Equal(new bool[] { true, true, true, true, true, true, true, true, false, false, true, true }, changed.Known);
        transaction.RollbackCheckpoint();
        Assert.Equal(SnapshotJson(source), SnapshotJson(transaction.Freeze()));
        Assert.Equal(new byte[12], transaction.View.ReadSnapshot(high, 12).Data);
        Assert.Equal(source.ReadSnapshot(high, 12).Known, transaction.View.ReadSnapshot(high, 12).Known);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, transaction.View.Read(4, 4));
        Assert.Equal(new byte[32], transaction.View.Read(0x80000, 32));
    }

    [Fact]
    public void Independent_owners_and_reused_overlay_growth_never_expose_disposed_transaction_bytes()
    {
        int high = NativeBattleEffectArena.Length - 64;
        using (NativeBattleEffectArenaTransaction previous = new(NativeBattleEffectArena.Clear()))
        {
            previous.View.Write(high, Enumerable.Repeat((byte)0xA5, 64).ToArray());
        }
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().MarkUnknown(high + 8, 8);
        using NativeBattleEffectArenaTransaction current = new(source);
        Assert.Equal(new byte[64], current.View.ReadSnapshot(high, 64).Data);
        Assert.Equal(new byte[8], current.View.Read(high, 8));
        Assert.Throws<NotSupportedException>(() => current.View.Read(high + 8, 1));
        Assert.Equal(SnapshotJson(source), SnapshotJson(current.Freeze()));
    }

    [Fact]
    public void SnapshotPreservesBackingBytesAndLiveKnownnessAcrossOverlappingCopiesAndRollback()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(40, new byte[] { 1, 2, 3, 4, 5, 6 }).MarkUnknown(42, 2);
        var immutable = source.ReadSnapshot(39, 8);
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 0 }, immutable.Data);
        Assert.Equal(new bool[] { true, true, true, false, false, true, true, true }, immutable.Known);
        using NativeBattleEffectArenaTransaction transaction = new(source);
        transaction.BeginCheckpoint();
        transaction.View.Write(41, new byte[] { 9, 8 });
        transaction.View.MarkUnknown(44, 1);
        var live = transaction.View.ReadSnapshot(40, 6);
        Assert.Equal(new byte[] { 1, 9, 8, 4, 5, 6 }, live.Data);
        Assert.Equal(new bool[] { true, true, true, false, false, true }, live.Known);
        transaction.View.Write(42, live.Data);
        Assert.Equal(live.Data, transaction.View.Read(42, 6));
        Assert.Throws<NotSupportedException>(() => source.Read(40, 6));
        transaction.RollbackCheckpoint();
        Assert.Equal(immutable.Data, transaction.View.ReadSnapshot(39, 8).Data);
        Assert.Equal(immutable.Known, transaction.View.ReadSnapshot(39, 8).Known);
        Assert.Equal(SnapshotJson(source), SnapshotJson(transaction.Freeze()));
    }

    [Fact]
    public void Known_and_unknown_stores_keep_working_identity_and_merge_adjacent_data_receipts()
    {
        NativeBattleEffectArena source = new([new(0, [1, 2]), new(2, [3, 4])]);
        Assert.Same(source, source.Write(0, [1]));
        NativeBattleEffectArena merged = source.Write(1, [2]);
        Assert.Single(merged.Capture().Regions);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, merged.Read(0, 4));
        NativeBattleEffectArena unknown = NativeBattleStorageSnapshots.Restore<NativeBattleEffectArena>(source.Capture()).MarkUnknown(0, 1);
        NativeBattleEffectArena known = unknown.Write(0, [1]);
        Assert.Same(unknown, known);
        Assert.Empty(known.Capture().UnknownRegions);
        Assert.Equal(new byte[] { 1 }, known.Read(0, 1));
        Assert.Same(unknown, unknown.MarkUnknown(0, 1));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, source.Read(0, 4));
    }

    [Fact]
    public void Ordered_read_through_and_borrowed_writers_match_independent_scalar_stores_and_preserve_detached_history()
    {
        NativeBattleEffectArena source = new([new(0, [1, 2]), new(2, [3, 4]), new(100, [5])]) { UnknownRegions = [new(1, 3), new(128, 16)] };
        NativeBattleEffectArena history = source.Capture();
        string original = SnapshotJson(history);
        NativeBattleEffectArena scalar = NativeBattleStorageSnapshots.Restore(history);
        using NativeBattleEffectArenaTransaction transaction = new(source);
        Random random = new(7821);
        for (int index = 0; index < 300; index++)
        {
            transaction.BeginCheckpoint();
            int offset = random.Next(2048);
            int count = random.Next(33);
            if (index % 3 == 0)
            {
                scalar = scalar.MarkUnknown(offset, count);
                transaction.View.MarkUnknown(offset, count);
            }
            else
            {
                byte[] bytes = new byte[count];
                random.NextBytes(bytes);
                scalar = scalar.Write(offset, bytes);
                if (index % 2 == 0)
                {
                    using NativeBattleEffectArenaWriter nested = new(transaction.View);
                    nested.Write(offset, bytes);
                    Assert.Same(transaction.View, nested.Freeze());
                }
                else
                {
                    transaction.View.Write(offset, bytes);
                }
            }
            transaction.CommitCheckpoint();
            Exception? expected = Record.Exception(() => scalar.Read(offset, count));
            if (expected is null)
            {
                Assert.Equal(scalar.Read(offset, count), transaction.View.Read(offset, count));
            }
            else
            {
                Assert.IsType<NotSupportedException>(expected);
                Assert.Throws<NotSupportedException>(() => transaction.View.Read(offset, count));
                var scalarSnapshot = scalar.ReadSnapshot(offset, count);
                var stagedSnapshot = transaction.View.ReadSnapshot(offset, count);
                Assert.Equal(scalarSnapshot.Data, stagedSnapshot.Data);
                Assert.Equal(scalarSnapshot.Known, stagedSnapshot.Known);
            }
            if (index % 23 == 0)
            {
                Assert.Equal(SnapshotJson(scalar), SnapshotJson(transaction.Freeze()));
            }
        }
        Assert.Equal(SnapshotJson(scalar), SnapshotJson(transaction.Freeze()));
        Assert.Equal(original, SnapshotJson(history));
        Assert.Equal(SnapshotJson(scalar), SnapshotJson(source));
        Assert.Same(source, transaction.Freeze());
    }

    [Fact]
    public void Failed_callback_rolls_back_overlapping_bytes_region_merges_and_unknown_ranges_only_to_its_checkpoint()
    {
        NativeBattleEffectArena source = new([new(0, [1, 2]), new(4, [5, 6])]) { UnknownRegions = [new(10, 4)] };
        using NativeBattleEffectArenaTransaction transaction = new(source);
        transaction.BeginCheckpoint();
        transaction.View.Write(8, [9, 10]);
        transaction.CommitCheckpoint();
        NativeBattleEffectArena first = transaction.Freeze();
        NativeBattleEffectArena history = first.Capture();
        string before = SnapshotJson(history);
        transaction.BeginCheckpoint();
        transaction.View.Write(2, [3, 4, 7, 8]);
        transaction.View.Write(4, [9, 10, 11, 12, 13, 14, 15]);
        transaction.View.MarkUnknown(0, 20);
        transaction.RollbackCheckpoint();
        Assert.Equal(before, SnapshotJson(transaction.Freeze()));
        Assert.Equal(new byte[] { 1, 2, 0, 0, 5, 6, 0, 0, 9, 10 }, transaction.View.Read(0, 10));
        transaction.BeginCheckpoint();
        transaction.View.Write(0, [99]);
        transaction.CommitCheckpoint();
        Assert.Equal(before, SnapshotJson(history));
        Assert.Same(source, first);
        Assert.Equal(new byte[] { 1, 2 }, source.Read(0, 2));
    }

    [Fact]
    public void Publication_updates_the_same_physical_owner_keeps_detached_history_and_rejects_disposed_views()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, [1, 2, 3]);
        NativeBattleEffectArena history = source.Capture();
        NativeBattleEffectArenaTransaction transaction = new(source);
        byte[] bytes = [9, 8];
        transaction.View.Write(1, bytes);
        bytes[0] = 0;
        Assert.Equal(new byte[] { 1, 2, 3 }, source.Read(0, 3));
        NativeBattleEffectArena published = transaction.Freeze();
        transaction.Dispose();
        Assert.Equal(new byte[] { 1, 9, 8 }, published.Read(0, 3));
        Assert.Equal(new byte[] { 1, 2, 3 }, history.Read(0, 3));
        Assert.Same(source, published);
        Assert.Equal(new byte[] { 1, 9, 8 }, source.Read(0, 3));
        Assert.Throws<ObjectDisposedException>(() => transaction.View.Read(0, 1));
        Assert.DoesNotContain("Transaction", SnapshotJson(published));
        Assert.Throws<InvalidOperationException>(() => new NativeBattleEffectArenaTransaction(transaction.View));
    }

    private static string SnapshotJson(NativeBattleEffectArena arena) => JsonSerializer.Serialize(arena.Capture());
}
