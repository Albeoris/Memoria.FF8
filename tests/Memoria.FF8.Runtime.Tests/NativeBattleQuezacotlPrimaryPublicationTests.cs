using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlPrimaryPublicationTests
{
    [Fact]
    public void Completed_local_packets_can_handoff_without_publishing_them_to_the_previous_graphics_owner()
    {
        NativeBattleQuezacotlPacketWrite published = Initial();
        NativeBattleGfGeometryBatch batch = Batch(21);
        NativeBattleQuezacotlPacketWrite expected = NativeBattleQuezacotlPacketArena.Append(published.Memory, published.Effect, published.Ordering, batch);
        using NativeBattleLogicalOrderingTransaction ordering = new(published.Ordering);
        ordering.BeginCheckpoint();
        NativeBattleQuezacotlPacketWrite local = NativeBattleQuezacotlPacketArena.Append(published.Memory, published.Effect, ordering.View, batch);
        NativeBattleLogicalOrdering handoff = ordering.Freeze();
        ordering.RollbackCheckpoint();
        NativeBattleLogicalOrdering previousOwner = ordering.Freeze();
        Assert.Equal(JsonSerializer.Serialize(expected.Ordering), JsonSerializer.Serialize(handoff));
        Assert.Equal(JsonSerializer.Serialize(published.Ordering), JsonSerializer.Serialize(previousOwner));
        Assert.Equal(JsonSerializer.Serialize(expected.Memory), JsonSerializer.Serialize(local.Memory));
        Assert.DoesNotContain("Transaction", JsonSerializer.Serialize(handoff));
        Assert.DoesNotContain("Transaction", JsonSerializer.Serialize(previousOwner));
    }

    [Fact]
    public void Failed_node_discards_successful_unpublished_links_and_freezes_the_actual_published_memory_record()
    {
        NativeBattleQuezacotlPacketWrite published = Initial();
        string expectedMemory = JsonSerializer.Serialize(published.Memory);
        string expectedOrdering = JsonSerializer.Serialize(published.Ordering);
        NativeBattleGfGeometryBatch invalid = new([Known(0, 22), Known(20, -1)], 40);
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlPacketArena.Append(published.Memory, published.Effect, published.Ordering, invalid));
        Assert.Equal(expectedMemory, JsonSerializer.Serialize(published.Memory));
        Assert.Equal(expectedOrdering, JsonSerializer.Serialize(published.Ordering));

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
        Assert.Equal(expectedMemory, JsonSerializer.Serialize(frozenPublishedOwner));
        Assert.Equal(expectedOrdering, JsonSerializer.Serialize(ordering.Freeze()));
        Assert.Equal(1, frozenPublishedOwner.QuezacotlTargetY1774);
        Assert.Equal(777, unpublishedLocal.QuezacotlTargetY1774);
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
}
