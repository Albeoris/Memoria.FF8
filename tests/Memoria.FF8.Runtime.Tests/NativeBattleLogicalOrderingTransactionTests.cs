using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLogicalOrderingTransactionTests
{
    [Fact]
    public void ModelProducerBatchesShareOneIndexAndFreezeIndependentSerializablePackets()
    {
        NativeBattleLogicalOrdering.Builder seed = new(NativeBattleLogicalOrdering.Clear());
        for (int index = 0; index < 512; index++)
        {
            seed.Prepend(20, new("20DFAB8", 0, index * 20), [0x04000000, 0x20010203, 1, 2, 3], 0, []);
        }
        NativeBattleLogicalOrdering source = seed.ToImmutable();
        string original = JsonSerializer.Serialize(source);
        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattleLogicalOrdering expected = AppendProducers(source);
        long scalarBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        using NativeBattleLogicalOrderingTransaction transaction = new(source);
        _ = AppendProducers(transaction.View);
        NativeBattleLogicalOrdering frozen = transaction.Freeze();
        long sharedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(sharedBytes < scalarBytes / 3, $"Shared producers allocated {sharedBytes} bytes versus {scalarBytes} bytes rebuilding indexes.");
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(transaction.View));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(frozen));
        Assert.Equal(original, JsonSerializer.Serialize(source));
        string completed = JsonSerializer.Serialize(frozen);
        transaction.View.Prepend(21, new("20DFAB8", 0, 0), [0x04000000, 0x20030201, 4, 5, 6], 0, []);
        Assert.Equal(completed, JsonSerializer.Serialize(frozen));

        static NativeBattleLogicalOrdering AppendProducers(NativeBattleLogicalOrdering ordering)
        {
            for (int producer = 0; producer < 16; producer++)
            {
                NativeBattleLogicalOrdering.Builder builder = new(ordering);
                for (int packet = 0; packet < 4; packet++)
                {
                    builder.Prepend(20, new("20DFAB8", 0, (512 + producer * 4 + packet) * 20), [0x04000000, 0x20010203, 1, 2, 3], 0, []);
                }
                ordering = builder.ToImmutable();
            }
            return ordering;
        }
    }

    [Fact]
    public void Ordered_alias_invalidations_and_replacements_match_immutable_producers_across_callback_commit_and_rollback()
    {
        NativeBattleLogicalOrdering source = NativeBattleLogicalOrdering.Clear();
        for (int index = 0; index < 8; index++)
        {
            source = source.Prepend(20 + index, new("20DFAB8", 0, index * 20), [0x04000000, 0x20010203, 1, 2, 3], 0, []);
        }
        source = source with { ProxyCursor = 2 };
        string original = JsonSerializer.Serialize(source);
        NativeBattleLogicalOrdering expected = source;
        using NativeBattleLogicalOrderingTransaction transaction = new(source);
        Random random = new(1947);
        for (int callback = 0; callback < 100; callback++)
        {
            NativeBattleLogicalOrdering before = expected;
            transaction.BeginCheckpoint();
            for (int store = 0; store < 4; store++)
            {
                NativeBattlePrimitiveReference reference = new(store % 2 == 0 ? "20DFAB8" : "off_B6D070", (byte)(store % 2), random.Next(32) * 4);
                int bucket = random.Next(16, 40);
                NativeBattleLogicalOrdering.Builder scalar = new(expected);
                NativeBattleLogicalOrdering.Builder borrowed = new(transaction.View);
                scalar.InvalidatePacketRange(reference, 16);
                borrowed.InvalidatePacketRange(reference, 16);
                scalar.Prepend(bucket, reference, [0x04000000, 0x20010203, (uint)callback, (uint)store, 3], (ushort)store, []);
                borrowed.Prepend(bucket, reference, [0x04000000, 0x20010203, (uint)callback, (uint)store, 3], (ushort)store, []);
                Assert.Equal(scalar.ProxyCount, borrowed.ProxyCount);
                Assert.Equal(scalar.LastHeaderLink, borrowed.LastHeaderLink);
                expected = scalar.ToImmutable();
                Assert.Same(transaction.View, borrowed.ToImmutable());
            }
            if (callback % 3 == 0)
            {
                transaction.RollbackCheckpoint();
                expected = before;
            }
            else
            {
                transaction.CommitCheckpoint();
            }
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(transaction.Freeze()));
        }
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }

    [Fact]
    public void Invalid_callback_rollback_preserves_completed_prefix_and_previously_frozen_snapshots()
    {
        NativeBattleLogicalOrdering source = NativeBattleLogicalOrdering.Clear();
        NativeBattleLogicalOrderingTransaction transaction = new(source);
        transaction.BeginCheckpoint();
        transaction.View.Prepend(20, new("20DFAB8", 0, 100), [0x04000000, 0x20010203, 1, 2, 3], 0, []);
        transaction.CommitCheckpoint();
        NativeBattleLogicalOrdering first = transaction.Freeze();
        string prefix = JsonSerializer.Serialize(first);
        transaction.BeginCheckpoint();
        transaction.View.Prepend(20, new("20DFAB8", 0, 100), [0x04000000, 0x200A0B0C, 4, 5, 6], 1, []);
        transaction.View.Prepend(21, new("20DFAB8", 0, 120), [0x04000000, 0x200D0E0F, 7, 8, 9], 2, []);
        Assert.Throws<InvalidDataException>(() => transaction.View.Prepend(-1, new("20DFAB8", 0, 140), [0x04000000, 0x20010203, 1, 2, 3], 0, []));
        transaction.RollbackCheckpoint();
        Assert.Equal(prefix, JsonSerializer.Serialize(transaction.Freeze()));
        transaction.BeginCheckpoint();
        transaction.View.Prepend(20, new("20DFAB8", 0, 100), [0x04000000, 0x200A0B0C, 4, 5, 6], 1, []);
        transaction.CommitCheckpoint();
        Assert.Equal(prefix, JsonSerializer.Serialize(first));
        NativeBattleLogicalOrdering published = transaction.Freeze();
        transaction.Dispose();
        Assert.DoesNotContain("Transaction", JsonSerializer.Serialize(published));
        Assert.Throws<ObjectDisposedException>(() => new NativeBattleLogicalOrdering.Builder(transaction.View));
        Assert.Empty(source.Proxies);
    }
}
