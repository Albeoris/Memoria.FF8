using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectNodeContextMemoryTests
{
    [Theory]
    [InlineData(0, 2, 8)]
    [InlineData(2, 0, 8)]
    [InlineData(0, 2, 512)]
    [InlineData(2, 0, 512)]
    [InlineData(0, 2, 513)]
    [InlineData(2, 0, 513)]
    public void Copy_snapshots_all_source_lanes_before_overlapping_stores(int sourceOffset, int destinationOffset, int count)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0, Enumerable.Range(0, count + 4).Select(index => unchecked((byte)(index + 1))).ToArray()).MarkUnknown(sourceOffset + 1, count - 2);
        (byte[] expectedData, bool[] expectedKnown) = arena.ReadSnapshot(0, count + 4);
        expectedData.AsSpan(sourceOffset, count).CopyTo(expectedData.AsSpan(destinationOffset));
        expectedKnown.AsSpan(sourceOffset, count).CopyTo(expectedKnown.AsSpan(destinationOffset));

        Assert.Same(arena, NativeBattleEffectNodeContextMemory.Copy(arena, sourceOffset, arena, destinationOffset, count));

        var actual = arena.ReadSnapshot(0, count + 4);
        Assert.Equal(expectedData, actual.Data);
        Assert.Equal(expectedKnown, actual.Known);
        NativeBattleEffectArena restored = NativeBattleStorageSnapshots.Restore<NativeBattleEffectArena>(arena.Capture());
        Assert.Equal(expectedData, restored.ReadSnapshot(0, count + 4).Data);
        Assert.Equal(expectedKnown, restored.ReadSnapshot(0, count + 4).Known);
    }

    [Fact]
    public void Copy_keeps_unknown_backing_bytes_and_invalidates_intersecting_destination_pointers()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, [17, 18]).MarkUnknown(1, 1);
        NativeBattleEffectArena destination = NativeBattleEffectArena.Clear().WriteResourcePointer(8, null);

        NativeBattleEffectNodeContextMemory.Copy(source, 0, destination, 10, 2);

        Assert.False(destination.TryGetResourcePointer(8, out _));
        Assert.Equal(new byte[] { 17, 18 }, destination.ReadSnapshot(10, 2).Data);
        Assert.Equal(new[] { true, false }, destination.ReadSnapshot(10, 2).Known);
        Assert.Throws<NotSupportedException>(() => destination.Read(11, 1));
    }

    [Fact]
    public void Copy_routes_borrowed_window_and_rolls_back_suffix_without_losing_accepted_prefix()
    {
        NativeBattleEffectArena owner = NativeBattleEffectArena.Clear();
        owner.AdmitRetained(new());
        NativeBattleEffectArena prefix = NativeBattleEffectArena.Clear().Write(0, [1, 2, 3, 4]).MarkUnknown(1, 1);
        NativeBattleEffectArena suffix = NativeBattleEffectArena.Clear().Write(0, [9, 8, 7, 6]).MarkUnknown(2, 1);
        byte[] physicalBytes = new byte[4];
        bool[] physicalKnown = new bool[4];
        using (NativeBattleTaskOperation operation = new([]))
        {
            using (NativeBattleEffectArenaTransaction transaction = new(owner))
            {
                NativeBattleEffectNodeContextMemory.Copy(prefix, 0, transaction.View, 36, 4);
                owner.Working.ReadWithKnownness(36, physicalBytes, physicalKnown);
                Assert.Equal(new byte[4], physicalBytes);
                Assert.Throws<InvalidOperationException>(() => owner.Capture());
                transaction.Freeze();
            }
            NativeBattleTaskOperation.AcceptPublishedArena(owner.Working);
            NativeBattleEffectArena window = NativeBattleEffectArena.BorrowWorking(owner.Working, 32, 16);
            using (NativeBattleEffectArenaTransaction transaction = new(window))
            {
                NativeBattleEffectNodeContextMemory.Copy(suffix, 0, window, 4, 4);
                owner.Working.ReadWithKnownness(36, physicalBytes, physicalKnown);
                Assert.Equal(prefix.ReadSnapshot(0, 4).Data, physicalBytes);
                Assert.Equal(prefix.ReadSnapshot(0, 4).Known, physicalKnown);
                Assert.Equal(suffix.ReadSnapshot(0, 4).Data, window.ReadSnapshot(4, 4).Data);
                Assert.Equal(suffix.ReadSnapshot(0, 4).Known, window.ReadSnapshot(4, 4).Known);
                transaction.Freeze();
            }
            Assert.Throws<InvalidDataException>(() => NativeBattleEffectNodeContextMemory.Copy(suffix, 0, owner, NativeBattleEffectArena.Length - 1, 4));
        }

        Assert.Equal(prefix.ReadSnapshot(0, 4).Data, owner.ReadSnapshot(36, 4).Data);
        Assert.Equal(prefix.ReadSnapshot(0, 4).Known, owner.ReadSnapshot(36, 4).Known);
        Assert.False(owner.Working.HasCheckpoint);
    }

    [Fact]
    public void Copy_validates_source_and_destination_bounds_before_mutation_and_allows_empty_boundary()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, [1, 2]);
        NativeBattleEffectArena destination = NativeBattleEffectArena.Clear().Write(0, [3, 4]);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectNodeContextMemory.Copy(source, 0, destination, 0, -1));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectNodeContextMemory.Copy(source, NativeBattleEffectArena.Length - 1, destination, 0, 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectNodeContextMemory.Copy(source, 0, destination, NativeBattleEffectArena.Length - 1, 2));
        Assert.Same(destination, NativeBattleEffectNodeContextMemory.Copy(source, NativeBattleEffectArena.Length, destination, NativeBattleEffectArena.Length, 0));
        Assert.Equal(new byte[] { 3, 4 }, destination.Read(0, 2));
    }

    [Fact]
    public void Fused_arena_store_keeps_length_and_frozen_snapshot_guards()
    {
        NativeBattleEffectArena destination = NativeBattleEffectArena.Clear().Write(0, [3, 4]);
        Assert.Throws<ArgumentException>(() => destination.WriteWithKnownness(0, new byte[] { 1, 2 }, new[] { true }));
        NativeBattleEffectArena snapshot = destination.Capture();
        Assert.Throws<InvalidOperationException>(() => snapshot.WriteWithKnownness(0, new byte[] { 1, 2 }, new[] { true, false }));
        Assert.Equal(new byte[] { 3, 4 }, destination.Read(0, 2));
        Assert.Equal(new byte[] { 3, 4 }, snapshot.Read(0, 2));
    }

    [Fact]
    public void Physical_view_shares_the_existing_prefix_and_Euler_owner_without_copying_away_unknown_values()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleEffectNodeScratch scratch = memory.NodeScratch2793E30! with { Bytes = new byte[] { 1, 2, 3, 4, 5, 6, 0xAB, 0xCD }.ToImmutableArray(), KnownMask = 0x3F, Prefix2793E08 = memory.NodeScratch2793E30!.Prefix2793E08!.Write(0, [17, 18]) };
        memory = memory with { NodeScratch2793E30 = scratch };
        NativeBattleEffectArena view = NativeBattleEffectNodeContextMemory.Capture(memory);
        Assert.Equal(new byte[] { 17, 18 }, view.Read(100, 2));
        (byte[] data, bool[] known) = view.ReadSnapshot(140, 8);
        Assert.Equal(scratch.Bytes.ToArray(), data);
        Assert.Equal(new[] { true, true, true, true, true, true, false, false }, known);
        Assert.Throws<NotSupportedException>(() => view.Read(146, 2));
        view = NativeBattleEffectNodeContextMemory.Copy(view, 140, view, 148, 8);
        NativeBattleEffectMemory published = NativeBattleEffectNodeContextMemory.Publish(memory, view);
        NativeBattleEffectArena next = NativeBattleEffectNodeContextMemory.Capture(published);
        Assert.Equal(data, next.ReadSnapshot(148, 8).Data);
        Assert.Equal(known, next.ReadSnapshot(148, 8).Known);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(published)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffectNodeContextMemory.Publish(NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(memory)))!), view))));
    }

    [Fact]
    public void Missing_extended_owner_stays_unknown_while_real_stores_publish_only_their_covered_bytes()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { NodeContextExtension = null };
        NativeBattleEffectArena view = NativeBattleEffectNodeContextMemory.Capture(memory);
        Assert.Throws<NotSupportedException>(() => view.Read(0, 1));
        Assert.Throws<NotSupportedException>(() => view.Read(148, 1));
        Assert.Equal(new byte[40], view.Read(100, 40));
        NativeBattleEffectMemory published = NativeBattleEffectNodeContextMemory.Publish(memory, view.Write(76, [1, 2, 3, 4]));
        NativeBattleEffectArena next = NativeBattleEffectNodeContextMemory.Capture(published);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, next.Read(76, 4));
        Assert.Throws<NotSupportedException>(() => next.Read(75, 1));
        Assert.Throws<NotSupportedException>(() => next.Read(80, 1));
    }
}
