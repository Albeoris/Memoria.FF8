using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLogicalOrderingWorkingStorageTests
{
    [Fact]
    public void Retained_proxy_metadata_reads_do_not_open_a_producer_scope_or_advance_the_allocation_cursor()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        ordering.Prepend(5, Reference(0), Words, 0, [11, 22, 33, 44]);
        ordering.ResetAfterDraw45D080();
        string original = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering));
        using (NativeBattleTaskOperation operation = new([]))
        {
            operation.AdmitOrdering(ordering);
            Assert.Equal(new[] { 11, 22, 33, 44 }, ordering.ReadRetainedProxyDepth(0));
            Assert.Empty(ordering.ReadRetainedProxyDepth(1));
            Assert.Empty(ordering.ReadRetainedProxyDepth(int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => ordering.ReadRetainedProxyDepth(-1));
            Assert.Equal(0, ordering.ProxyCursor);
            operation.Commit();
        }
        Assert.Equal(original, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering)));
    }

    [Fact]
    public void Suspending_latest_primary_callback_keeps_published_prefix_when_secondary_fails_before_publication()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        ordering.Prepend(5, Reference(0), Words, 0, []);
        NativeBattleLogicalOrdering.Delta suffix;
        using (NativeBattleLogicalOrderingTransaction primary = new(ordering))
        {
            primary.BeginCheckpoint();
            Append(primary.Writer, 1);
            primary.CommitCheckpoint();
            primary.BeginCheckpoint();
            Append(primary.Writer, 2);
            suffix = primary.SuspendCheckpointDelta();
            Assert.Equal(2, ordering.Packets.Length);
            Assert.Same(ordering, primary.Freeze());
        }
        string accepted = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering));
        Assert.Throws<InvalidDataException>(() => Secondary());
        Assert.Equal(accepted, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering)));
        Assert.Equal(new[] { Reference(0), Reference(1) }, ordering.Packets.Select(packet => packet.Reference));
        Assert.Equal(1, ordering.Table[5].Index);

        void Secondary()
        {
            using NativeBattleLogicalOrderingTransaction continuation = new(ordering);
            continuation.ResumeDelta(suffix);
            Assert.Equal(3, ordering.Packets.Length);
            throw new InvalidDataException("Secondary validation failed before publishing its input graphics.");
        }
    }

    [Fact]
    public void Secondary_accepted_prefix_keeps_resumed_primary_suffix_and_discards_later_failed_callback()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleLogicalOrdering.Delta suffix;
        using (NativeBattleLogicalOrderingTransaction primary = new(ordering))
        {
            primary.BeginCheckpoint();
            Append(primary.Writer, 0);
            primary.CommitCheckpoint();
            primary.BeginCheckpoint();
            Append(primary.Writer, 1);
            suffix = primary.SuspendCheckpointDelta();
            primary.Freeze();
        }
        using (NativeBattleLogicalOrderingTransaction continuation = new(ordering))
        {
            continuation.ResumeDelta(suffix);
            using (NativeBattleLogicalOrderingTransaction secondary = new(ordering))
            {
                secondary.BeginCheckpoint();
                Append(secondary.Writer, 2);
                secondary.CommitCheckpoint();
                secondary.Freeze();
                secondary.BeginCheckpoint();
                Append(secondary.Writer, 3);
                secondary.RollbackCheckpoint();
                secondary.Freeze();
            }
            continuation.Freeze();
        }
        Assert.Equal(new[] { Reference(0), Reference(1), Reference(2) }, ordering.Packets.Select(packet => packet.Reference));
        Assert.Equal(3, ordering.ProxyCursor);
        Assert.Equal(2, ordering.Table[5].Index);
        Assert.Equal(3, ordering.Traverse(Lengths).Length);
    }

    [Fact]
    public void Successive_producers_keep_one_owner_and_current_read_only_collection_views()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        object identity = ordering.StorageIdentity;
        NativeBattleOrderingList<NativeBattlePrimitiveLink> roots = ordering.Table;
        NativeBattleOrderingList<NativeBattleLogicalPacket> packets = ordering.Packets;
        for (int index = 0; index < 100; index++)
        {
            using NativeBattleLogicalOrdering.Builder producer = new(ordering);
            Append(producer, index);
            Assert.Same(ordering, producer.ToImmutable());
        }
        Assert.Same(identity, ordering.StorageIdentity);
        Assert.Equal(100, ordering.ProxyCursor);
        Assert.Equal(100, packets.Length);
        Assert.True(roots[5].Proxy);
        Assert.Equal(99, roots[5].Index);
    }

    [Fact]
    public void Disposing_a_later_failed_suffix_preserves_the_last_accepted_prefix()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        using (NativeBattleLogicalOrderingTransaction transaction = new(ordering))
        {
            transaction.BeginCheckpoint();
            Append(transaction.Writer, 0);
            transaction.CommitCheckpoint();
            Assert.Same(ordering, transaction.Freeze());
            using (NativeBattleLogicalOrdering.Builder producer = new(transaction.View))
            {
                Append(producer, 1);
                Assert.Throws<InvalidDataException>(() => producer.Prepend(-1, Reference(2), Words, 0, []));
            }
            Append(transaction.Writer, 3);
        }
        Assert.Single(ordering.Packets);
        Assert.Equal(1, ordering.ProxyCursor);
        Assert.Equal(Reference(0), ordering.Proxies[0].Packet);
        Assert.Equal(0, ordering.Table[5].Index);
    }

    [Fact]
    public void Callback_rollback_reverts_nested_published_producers_and_persistent_packet_indexes()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        using (NativeBattleLogicalOrderingTransaction transaction = new(ordering))
        {
            transaction.BeginCheckpoint();
            using (NativeBattleLogicalOrdering.Builder producer = new(transaction.View))
            {
                Append(producer, 0);
                producer.ToImmutable();
            }
            transaction.RollbackCheckpoint();
            Assert.Empty(ordering.Packets);
            Assert.False(ordering.Table[5].Proxy);
            Append(transaction.Writer, 0);
            transaction.Freeze();
        }
        Assert.Single(ordering.Packets);
        Assert.Single(ordering.Traverse(Lengths));
    }

    [Fact]
    public void Snapshots_json_and_two_restores_are_detached_and_active_capture_is_rejected()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        ordering.Prepend(5, Reference(0), Words, 0, []);
        NativeBattleLogicalOrdering snapshot = NativeBattleStorageSnapshots.Capture(ordering);
        string json = JsonSerializer.Serialize(snapshot);
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(ordering));
        Assert.Throws<InvalidOperationException>(() => snapshot.ClearRoots45D550());
        NativeBattleLogicalOrdering left = NativeBattleStorageSnapshots.Restore(snapshot);
        NativeBattleLogicalOrdering right = NativeBattleStorageSnapshots.Restore(snapshot);
        left.SetTableLink(5, new(false, -1));
        Assert.True(right.Table[5].Proxy);
        Assert.NotSame(left.StorageIdentity, right.StorageIdentity);
        Assert.Equal(json, JsonSerializer.Serialize(snapshot));
        NativeBattleLogicalOrdering loaded = JsonSerializer.Deserialize<NativeBattleLogicalOrdering>(json)!;
        Assert.True(loaded.IsSnapshot);
        Assert.Equal(json, JsonSerializer.Serialize(loaded));
        using NativeBattleLogicalOrdering.Builder pending = new(right);
        Assert.Throws<InvalidOperationException>(() => NativeBattleStorageSnapshots.Capture(right));
    }

    [Fact]
    public void A_bounded_suspended_suffix_can_resume_after_scope_disposal_without_an_ordering_snapshot()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleLogicalOrdering.Delta delta;
        using (NativeBattleLogicalOrderingTransaction first = new(ordering))
        {
            Append(first.Writer, 0);
            first.Freeze();
            first.BeginCheckpoint();
            Append(first.Writer, 1);
            first.CommitCheckpoint();
            delta = first.SuspendDelta();
            Assert.Single(ordering.Packets);
            first.Freeze();
        }
        NativeBattleLogicalOrdering accepted = NativeBattleStorageSnapshots.Capture(ordering);
        using (NativeBattleLogicalOrderingTransaction next = new(ordering))
        {
            next.ResumeDelta(delta);
            Assert.Equal(2, ordering.Packets.Length);
            next.Freeze();
            Assert.Throws<InvalidOperationException>(() => next.ResumeDelta(delta));
        }
        Assert.Single(accepted.Packets);
        Assert.Equal(2, ordering.Packets.Length);
        Assert.Equal(2, ordering.Traverse(Lengths).Length);
    }

    [Fact]
    public void Table_swap_and_native_clear_metadata_roll_back_with_their_actual_packet_containers()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        ordering.Prepend(5, Reference(0), Words, 0, []);
        NativeBattleOrderingList<NativeBattleLogicalPacket> beforeReset = ordering.Packets;
        using (NativeBattleLogicalOrderingTransaction transaction = new(ordering))
        {
            transaction.BeginCheckpoint();
            ordering.ResetAfterDraw45D080().ClearRoots45D550().SelectTable(1).SetTableLink(8, new(true, 0));
            Assert.Empty(ordering.Packets);
            Assert.Throws<InvalidOperationException>(() => _ = beforeReset.Length);
            transaction.RollbackCheckpoint();
            transaction.Freeze();
        }
        Assert.Equal((byte)0, ordering.TableBuffer);
        Assert.True(ordering.Table[5].Proxy);
        Assert.Single(ordering.Packets);
        Assert.False(ordering.Packets[0].RequiresPhysicalRead);
        Assert.Single(ordering.Traverse(Lengths));
    }

    [Fact]
    public void Disposed_transaction_collection_borrows_fail_and_physical_decode_keeps_its_original_boundary()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleLogicalOrderingTransaction transaction = new(ordering);
        NativeBattleOrderingList<NativeBattlePrimitiveLink> borrowed = transaction.View.Table;
        transaction.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = borrowed.Length);
        ordering.Prepend(5, Reference(0), Words, 0, []);
        using (NativeBattleLogicalOrdering.Builder producer = new(ordering))
        {
            producer.InvalidatePacketRange(Reference(0), 4);
            producer.ToImmutable();
        }
        Assert.Throws<NotSupportedException>(() => ordering.Traverse(Lengths));
        int reads = 0;
        var commands = ordering.Traverse(Lengths, (_, length) =>
        {
            reads++;
            byte[] bytes = new byte[length];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x01FFFFFF);
            if (length > 4)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x02000000);
            }
            return bytes;
        });
        Assert.Equal(2, reads);
        Assert.Equal((byte)2, Assert.Single(commands).Opcode);
        Assert.Equal(0x01000000u, ordering.Packets[0].Words[1]);
    }

    [Fact]
    public void Model_producer_scope_ends_before_render_tail_resets_and_next_frame_reuses_the_same_packet_bank()
    {
        NativeBattlePrimitiveReference packet = new("off_B6D070", 0, 0);
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        ProduceAndReset(ordering, packet, closeBeforeTail: true);
        Assert.Equal(0, ordering.ProxyCursor);
        Assert.Empty(ordering.Packets);
        Assert.DoesNotContain(ordering.Table, link => link.Proxy);
        Assert.DoesNotContain(ordering.OtherTable!.Value, link => link.Proxy);
        using (NativeBattleLogicalOrderingTransaction next = new(ordering))
        {
            next.Writer.Prepend(5, packet, Words, 0, []);
            next.Freeze();
        }
        Assert.Equal(1, ordering.ProxyCursor);
        Assert.Single(ordering.Packets);
        Assert.Single(ordering.Traverse(Lengths));

        NativeBattleLogicalOrdering incorrectLifetime = NativeBattleLogicalOrdering.Clear();
        ProduceAndReset(incorrectLifetime, packet, closeBeforeTail: false);
        Assert.Equal(1, incorrectLifetime.ProxyCursor);
        incorrectLifetime.Prepend(5, packet, Words, 0, []);
        Assert.Throws<InvalidDataException>(() => incorrectLifetime.Traverse(Lengths));
    }

    [Fact]
    public void Ending_the_owned_model_producer_does_not_accept_an_enclosing_automatic_ordering_scope()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        string before = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering));
        using (NativeBattleTaskOperation operation = new([]))
        {
            operation.AdmitOrdering(ordering);
            ProduceAndReset(ordering, new("off_B6D070", 0, 0), closeBeforeTail: true);
            ordering.Prepend(7, new("off_B6D070", 0, 0), Words, 0, []);
            Assert.Single(ordering.Traverse(Lengths));
        }
        Assert.Equal(before, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering)));
    }

    private static void ProduceAndReset(NativeBattleLogicalOrdering ordering, NativeBattlePrimitiveReference packet, bool closeBeforeTail)
    {
        using NativeBattleLogicalOrderingTransaction producer = new(ordering);
        producer.Writer.Prepend(5, packet, Words, 0, []);
        Assert.Same(ordering, producer.Freeze());
        Assert.Single(ordering.Traverse(Lengths));
        if (closeBeforeTail)
        {
            producer.Dispose();
        }
        ordering.ResetAfterDraw45D080().ClearRoots45D550();
    }

    private static NativeBattlePrimitiveReference Reference(int index) => new("logical-test", 0, index * 8);
    private static readonly ImmutableArray<uint> Words = [0x01000000, 0x01000000];
    private static readonly ImmutableArray<byte> Lengths = Enumerable.Repeat((byte)1, 256).ToImmutableArray();
    private static void Append(NativeBattleLogicalOrdering.Builder producer, int index) => producer.Prepend(5, Reference(index), Words, 0, []);
}
