using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectArenaTransactionTests
{
    [Fact]
    public void Equal_known_writes_skip_copies_only_when_the_original_store_would_not_merge_adjacent_regions()
    {
        NativeBattleEffectArena source = new([new(0, [1, 2]), new(2, [3, 4])]);
        Assert.Same(source, source.Write(0, [1]));
        NativeBattleEffectArena merged = source.Write(1, [2]);
        Assert.Single(merged.Regions);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, merged.Read(0, 4));
        NativeBattleEffectArena unknown = source.MarkUnknown(0, 1);
        NativeBattleEffectArena known = unknown.Write(0, [1]);
        Assert.Empty(known.UnknownRegions);
        Assert.Equal(new byte[] { 1 }, known.Read(0, 1));
        Assert.Same(unknown, unknown.MarkUnknown(0, 1));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, source.Read(0, 4));
    }

    [Fact]
    public void Ordered_read_through_and_nested_packet_writers_match_immutable_stores_and_unknown_bytes()
    {
        NativeBattleEffectArena source = new([new(0, [1, 2]), new(2, [3, 4]), new(100, [5])]) { UnknownRegions = [new(1, 3), new(128, 16)] };
        string original = JsonSerializer.Serialize(source);
        NativeBattleEffectArena scalar = source;
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
                    NativeBattleEffectArenaWriter nested = new(transaction.View);
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
                Exception actual = Assert.Throws<NotSupportedException>(() => transaction.View.Read(offset, count));
                Assert.Equal(expected.Message, actual.Message);
            }
            if (index % 23 == 0)
            {
                Assert.Equal(JsonSerializer.Serialize(scalar), JsonSerializer.Serialize(transaction.Freeze()));
            }
        }
        Assert.Equal(JsonSerializer.Serialize(scalar), JsonSerializer.Serialize(transaction.Freeze()));
        Assert.Equal(original, JsonSerializer.Serialize(source));
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
        string before = JsonSerializer.Serialize(first);
        transaction.BeginCheckpoint();
        transaction.View.Write(2, [3, 4, 7, 8]);
        transaction.View.Write(4, [9, 10, 11, 12, 13, 14, 15]);
        transaction.View.MarkUnknown(0, 20);
        transaction.RollbackCheckpoint();
        Assert.Equal(before, JsonSerializer.Serialize(transaction.Freeze()));
        Assert.Equal(new byte[] { 1, 2, 0, 0, 5, 6, 0, 0, 9, 10 }, transaction.View.Read(0, 10));
        transaction.BeginCheckpoint();
        transaction.View.Write(0, [99]);
        transaction.CommitCheckpoint();
        Assert.Equal(before, JsonSerializer.Serialize(first));
        Assert.Equal(new byte[] { 1, 2 }, source.Read(0, 2));
    }

    [Fact]
    public void Transaction_storage_never_backs_a_published_immutable_arena_and_cannot_be_read_after_disposal()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, [1, 2, 3]);
        NativeBattleEffectArenaTransaction transaction = new(source);
        byte[] bytes = [9, 8];
        transaction.View.Write(1, bytes);
        bytes[0] = 0;
        NativeBattleEffectArena published = transaction.Freeze();
        transaction.Dispose();
        Assert.Equal(new byte[] { 1, 9, 8 }, published.Read(0, 3));
        Assert.Equal(new byte[] { 1, 2, 3 }, source.Read(0, 3));
        Assert.Throws<ObjectDisposedException>(() => transaction.View.Read(0, 1));
        Assert.DoesNotContain("Transaction", JsonSerializer.Serialize(published));
        Assert.Throws<InvalidOperationException>(() => new NativeBattleEffectArenaTransaction(transaction.View));
    }
}
