using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlPrimaryPublicationTests
{
    [Fact]
    public void Completed_local_ordering_suffix_can_be_suspended_while_its_previous_owner_is_published()
    {
        NativeBattleQuezacotlPacketWrite published = Initial();
        string previousOrdering = OrderingReceipt(published.Ordering);
        NativeBattleGfGeometryBatch batch = Batch(21);
        NativeBattleQuezacotlPacketWrite input = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(published));
        NativeBattleQuezacotlPacketWrite expected = NativeBattleQuezacotlPacketArena.Append(input.Memory, input.Effect, input.Ordering, batch);
        using NativeBattleLogicalOrderingTransaction ordering = new(published.Ordering);
        ordering.BeginCheckpoint();
        NativeBattleQuezacotlPacketWrite local = NativeBattleQuezacotlPacketArena.Append(published.Memory, published.Effect, ordering.View, batch);
        string completedHandoff = OrderingReceipt(ordering.View);
        NativeBattleLogicalOrdering.Delta pending = ordering.SuspendCheckpointDelta();
        NativeBattleLogicalOrdering previousOwner = ordering.Freeze();
        Assert.Equal(OrderingReceipt(expected.Ordering), completedHandoff);
        Assert.Equal(previousOrdering, OrderingReceipt(previousOwner));
        Assert.Equal(SnapshotJson(expected.Memory), SnapshotJson(local.Memory));
        Assert.DoesNotContain("Transaction", completedHandoff);
        Assert.DoesNotContain("Transaction", OrderingReceipt(previousOwner));
        ordering.ResumeDelta(pending);
        Assert.Equal(completedHandoff, OrderingReceipt(ordering.Freeze()));
    }

    [Fact]
    public void Failed_node_discards_successful_unpublished_links_and_freezes_the_actual_published_memory_record()
    {
        NativeBattleQuezacotlPacketWrite published = Initial();
        string expectedMemory = SnapshotJson(published.Memory);
        string expectedOrdering = OrderingReceipt(published.Ordering);
        NativeBattleGfGeometryBatch invalid = new([Known(0, 22), Known(20, -1)], 40);
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlPacketArena.Append(published.Memory, published.Effect, published.Ordering, invalid));
        Assert.Equal(expectedMemory, SnapshotJson(published.Memory));
        Assert.Equal(expectedOrdering, OrderingReceipt(published.Ordering));

        using NativeBattleEffectArenaTransaction arena = new(published.Memory.Arena!);
        using NativeBattleLogicalOrderingTransaction ordering = new(published.Ordering);
        arena.BeginCheckpoint();
        ordering.BeginCheckpoint();
        NativeBattleEffectMemory actualPublishedOwner = published.Memory with { Arena = arena.View };
        NativeBattleQuezacotlPacketWrite local = NativeBattleQuezacotlPacketArena.Append(actualPublishedOwner, published.Effect, ordering.View, Batch(21));
        NativeBattleEffectMemory unpublishedLocal = local.Memory with { QuezacotlTargetY1774 = 777 };
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlPacketArena.Append(unpublishedLocal, local.Effect, ordering.View, invalid));
        arena.RollbackCheckpoint();
        ordering.RollbackCheckpoint();
        NativeBattleEffectMemory frozenPublishedOwner = actualPublishedOwner with { Arena = arena.Freeze() };
        Assert.Equal(expectedMemory, SnapshotJson(frozenPublishedOwner));
        Assert.Equal(expectedOrdering, OrderingReceipt(ordering.Freeze()));
        Assert.Equal(1, frozenPublishedOwner.QuezacotlTargetY1774);
        Assert.Equal(777, unpublishedLocal.QuezacotlTargetY1774);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Actual_graphics_publication_accepts_primary_or_provisional_prefix_and_rolls_back_only_later_suffix(bool provisional, bool callback)
    {
        NativeBattleQuezacotlPacketWrite input = Initial();
        string accepted;
        using (NativeBattleTaskOperation operation = new([]))
        {
            operation.AdmitOrdering(input.Ordering);
            using NativeBattleLogicalOrderingTransaction? continuation = provisional ? new(input.Ordering) : null;
            if (continuation is not null)
            {
                NativeBattleQuezacotlPacketArena.Append(input.Memory, input.Effect, continuation.View, Batch(21));
            }
            using (NativeBattleLogicalOrderingTransaction producer = new(input.Ordering))
            {
                if (callback)
                {
                    producer.BeginCheckpoint();
                }
                NativeBattleQuezacotlPacketArena.Append(input.Memory, input.Effect, producer.View, Batch(22));
                Assert.Throws<InvalidOperationException>(() => NativeBattleTaskOperation.AcceptPublishedOrdering(producer.View));
                NativeBattleLogicalOrdering published = BattleSession.FreezeNativeQuezacotlOrderingPublication(producer.View, provisional ? input.Ordering : null, continuation);
                NativeBattleTaskOperation.AcceptPublishedOrdering(published);
                accepted = OrderingReceipt(published);
                Assert.Null(published.Transaction);
                if (callback)
                {
                    producer.CommitCheckpoint();
                    producer.BeginCheckpoint();
                }
                NativeBattleQuezacotlPacketArena.Append(input.Memory, input.Effect, producer.View, Batch(23));
            }
        }
        Assert.Equal(accepted, OrderingReceipt(input.Ordering));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ancestor_publication_rejects_an_unfinished_inner_native_producer(bool callback)
    {
        NativeBattleQuezacotlPacketWrite input = Initial();
        string original = OrderingReceipt(input.Ordering);
        using (NativeBattleLogicalOrderingTransaction ancestor = new(input.Ordering))
        {
            if (callback)
            {
                ancestor.BeginCheckpoint();
            }
            using NativeBattleLogicalOrderingTransaction inner = new(input.Ordering);
            NativeBattleQuezacotlPacketArena.Append(input.Memory, input.Effect, inner.View, Batch(21));
            Assert.Throws<InvalidOperationException>(() => ancestor.FreezeKnownAncestorPrefix());
        }
        Assert.Equal(original, OrderingReceipt(input.Ordering));
    }
    private static NativeBattleQuezacotlPacketWrite Initial()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116) with { QuezacotlTargetY1774 = 1 };
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { DrawArenaOffset = 128 };
        return NativeBattleQuezacotlPacketArena.Append(memory, effect, NativeBattleLogicalOrdering.Clear(), Batch(20));
    }

    private static NativeBattleGfGeometryBatch Batch(int bucket) => new([Known(0, bucket)], 20);

    private static NativeBattleGfGeometryAttempt Known(int offset, int bucket)
    {
        NativeBattleGeometryPrimitive primitive = new(bucket, [0x04000000, 0x20010203, 1, 2, 3], [100, 110, 120, 0]);
        byte[] bytes = new byte[20];
        for (int index = 0; index < primitive.Words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), primitive.Words[index]);
        }
        return new(offset, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray(), primitive);
    }
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));

    private static string OrderingReceipt(NativeBattleLogicalOrdering value) => JsonSerializer.Serialize(new { Table = value.Table.ToArray(), Proxies = value.Proxies.ToArray(), Packets = value.Packets.ToArray(), value.ProxyCursor, value.TableBuffer, OtherTable = value.OtherTable?.ToArray() });

}
