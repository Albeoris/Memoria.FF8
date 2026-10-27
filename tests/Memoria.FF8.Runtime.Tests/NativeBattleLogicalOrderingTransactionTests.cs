using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLogicalOrderingTransactionTests
{
    [Fact]
    public void ModelProducerBatchesShareOneIndexAndCaptureIndependentSerializablePackets()
    {
        NativeBattleLogicalOrdering source = NativeBattleLogicalOrdering.Clear();
        using (NativeBattleLogicalOrdering.Builder seed = new(source))
        {
            for (int index = 0; index < 512; index++)
            {
                seed.Prepend(20, new("20DFAB8", 0, index * 20), [0x04000000, 0x20010203, 1, 2, 3], 0, []);
            }
            seed.ToImmutable();
        }
        NativeBattleLogicalOrdering original = NativeBattleStorageSnapshots.Capture(source);
        string originalJson = JsonSerializer.Serialize(original);
        NativeBattleLogicalOrdering expected = AppendProducers(NativeBattleStorageSnapshots.Restore(original));
        NativeBattleLogicalOrdering expectedSnapshot = NativeBattleStorageSnapshots.Capture(expected);
        using (NativeBattleLogicalOrderingTransaction transaction = new(source))
        {
            _ = AppendProducers(transaction.View);
            Assert.Same(source, transaction.Freeze());
            Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(transaction.View));
            Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(source));
        }
        NativeBattleLogicalOrdering frozen = NativeBattleStorageSnapshots.Capture(source);
        Assert.Equal(JsonSerializer.Serialize(expectedSnapshot), JsonSerializer.Serialize(frozen));
        Assert.Equal(originalJson, JsonSerializer.Serialize(original));
        string completed = JsonSerializer.Serialize(frozen);
        using (NativeBattleLogicalOrderingTransaction transaction = new(source))
        {
            transaction.View.Prepend(21, new("20DFAB8", 0, 0), [0x04000000, 0x20030201, 4, 5, 6], 0, []);
            transaction.Freeze();
        }
        Assert.Equal(completed, JsonSerializer.Serialize(frozen));
        Assert.NotEqual(completed, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(source)));

        static NativeBattleLogicalOrdering AppendProducers(NativeBattleLogicalOrdering ordering)
        {
            for (int producer = 0; producer < 16; producer++)
            {
                using NativeBattleLogicalOrdering.Builder builder = new(ordering);
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
    public void Ordered_alias_invalidations_and_replacements_match_independent_producers_across_callback_commit_and_rollback()
    {
        NativeBattleLogicalOrdering source = NativeBattleLogicalOrdering.Clear();
        for (int index = 0; index < 8; index++)
        {
            source.Prepend(20 + index, new("20DFAB8", 0, index * 20), [0x04000000, 0x20010203, 1, 2, 3], 0, []);
        }
        source.UpdateMetadata(2, source.TableBuffer);
        NativeBattleLogicalOrdering original = NativeBattleStorageSnapshots.Capture(source);
        string originalJson = JsonSerializer.Serialize(original);
        NativeBattleLogicalOrdering expected = NativeBattleStorageSnapshots.Restore(original);
        using (NativeBattleLogicalOrderingTransaction transaction = new(source))
        {
            Random random = new(1947);
            for (int callback = 0; callback < 100; callback++)
            {
                NativeBattleLogicalOrdering before = NativeBattleStorageSnapshots.Capture(expected);
                transaction.BeginCheckpoint();
                for (int store = 0; store < 4; store++)
                {
                    NativeBattlePrimitiveReference reference = new(store % 2 == 0 ? "20DFAB8" : "off_B6D070", (byte)(store % 2), random.Next(32) * 4);
                    int bucket = random.Next(16, 40);
                    using NativeBattleLogicalOrdering.Builder scalar = new(expected);
                    using NativeBattleLogicalOrdering.Builder borrowed = new(transaction.View);
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
                    expected = NativeBattleStorageSnapshots.Restore(before);
                }
                else
                {
                    transaction.CommitCheckpoint();
                }
                Assert.Equal(Receipt(expected), Receipt(transaction.Freeze()));
            }
        }
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(expected)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(source)));
        Assert.Equal(originalJson, JsonSerializer.Serialize(original));
    }

    [Fact]
    public void Invalid_callback_rollback_preserves_completed_prefix_and_previously_captured_snapshots()
    {
        NativeBattleLogicalOrdering source = NativeBattleLogicalOrdering.Clear();
        NativeBattleLogicalOrdering original = NativeBattleStorageSnapshots.Capture(source);
        using (NativeBattleLogicalOrderingTransaction transaction = new(source))
        {
            transaction.BeginCheckpoint();
            transaction.View.Prepend(20, new("20DFAB8", 0, 100), [0x04000000, 0x20010203, 1, 2, 3], 0, []);
            transaction.CommitCheckpoint();
            transaction.Freeze();
        }
        NativeBattleLogicalOrdering first = NativeBattleStorageSnapshots.Capture(source);
        string prefix = JsonSerializer.Serialize(first);
        NativeBattleLogicalOrderingTransaction next = new(source);
        using (next)
        {
            next.BeginCheckpoint();
            next.View.Prepend(20, new("20DFAB8", 0, 100), [0x04000000, 0x200A0B0C, 4, 5, 6], 1, []);
            next.View.Prepend(21, new("20DFAB8", 0, 120), [0x04000000, 0x200D0E0F, 7, 8, 9], 2, []);
            Assert.Throws<InvalidDataException>(() => next.View.Prepend(-1, new("20DFAB8", 0, 140), [0x04000000, 0x20010203, 1, 2, 3], 0, []));
            next.RollbackCheckpoint();
            Assert.Equal(Receipt(first), Receipt(next.Freeze()));
            next.BeginCheckpoint();
            next.View.Prepend(20, new("20DFAB8", 0, 100), [0x04000000, 0x200A0B0C, 4, 5, 6], 1, []);
            next.CommitCheckpoint();
            Assert.Equal(prefix, JsonSerializer.Serialize(first));
            next.Freeze();
        }
        NativeBattleLogicalOrdering published = NativeBattleStorageSnapshots.Capture(source);
        Assert.DoesNotContain("Transaction", JsonSerializer.Serialize(published));
        Assert.Throws<ObjectDisposedException>(() => new NativeBattleLogicalOrdering.Builder(next.View));
        Assert.Empty(original.Proxies);
        Assert.NotEmpty(source.Proxies);
        Assert.Equal(prefix, JsonSerializer.Serialize(first));
    }

    private static string Receipt(NativeBattleLogicalOrdering value) => JsonSerializer.Serialize(new { Table = value.Table.ToArray(), Proxies = value.Proxies.ToArray(), Packets = value.Packets.ToArray(), value.ProxyCursor, value.TableBuffer, OtherTable = value.OtherTable?.ToArray() });
}
