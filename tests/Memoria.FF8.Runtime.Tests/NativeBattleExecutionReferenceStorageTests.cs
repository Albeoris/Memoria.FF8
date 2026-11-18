using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleExecutionReferenceStorageTests
{
    private static readonly BattleExecutionReference.Model Model = new(new BattlePartyActorModel(new("live-owner")));
    private static readonly BattleExecutionReference.Targets Targets = new(BattleModelTargets.From([Model.Owner]));
    private static readonly BattleExecutionReference.Header Header = new(new(4));

    [Fact]
    public void Exact_variant_and_width_are_required_and_raw_stores_invalidate_spans()
    {
        using NativeBattleWorkingMemory memory = new(96);
        memory.WriteExecutionReference(4, 1, Model, [8]);
        memory.WriteExecutionReference(6, 2, Targets);
        memory.WriteExecutionReference(12, 4, Header, [9, 8, 7, 6]);
        Assert.Equal(Model, memory.ReadExecutionReference<BattleExecutionReference.Model>(4, 1));
        Assert.Equal(Targets, memory.ReadExecutionReference<BattleExecutionReference.Targets>(6, 2));
        Assert.Equal(Header, memory.ReadExecutionReference<BattleExecutionReference.Header>(12, 4));
        Assert.False(memory.TryReadExecutionReference<BattleExecutionReference.Model>(4, 2, out _));
        Assert.False(memory.TryReadExecutionReference<BattleExecutionReference.Model>(6, 2, out _));
        Assert.Throws<InvalidDataException>(() => memory.ReadExecutionReference<BattleExecutionReference.Model>(5, 1));
        Assert.Equal(8, memory.ReadByte(4));
        Assert.Throws<NotSupportedException>(() => memory.ReadUInt16(6));

        memory.WriteRetainedBytes(7, [0xF0]);
        Assert.False(memory.TryReadExecutionReference<BattleExecutionReference.Targets>(6, 2, out _));
        memory.SetResourcePointer(15, new(null, 0x1000));
        Assert.False(memory.TryReadExecutionReference<BattleExecutionReference.Header>(12, 4, out _));
        Assert.True(memory.TryGetResourcePointer(15, out _));
        memory.WriteExecutionReference(18, 2, new BattleExecutionReference.Cursor(new(8)));
        Assert.False(memory.TryGetResourcePointer(15, out _));
        memory.MarkUnknown(18, 1);
        Assert.False(memory.TryReadExecutionReference<BattleExecutionReference.Cursor>(18, 2, out _));
        Assert.Equal(Model, memory.ReadExecutionReference<BattleExecutionReference.Model>(4, 1));
    }

    [Fact]
    public void Nested_and_outer_checkpoint_restore_reference_kind_bytes_and_knownness()
    {
        using NativeBattleWorkingMemory memory = new(96);
        memory.WriteExecutionReference(12, 4, Header, [9, 8, 7, 6]);
        memory.BeginCheckpoint();
        memory.WriteExecutionReference(13, 2, Targets);
        memory.BeginNestedCheckpoint();
        memory.SetResourcePointer(12, new(null, 0x1000));
        Assert.False(memory.TryReadExecutionReference<BattleExecutionReference.Targets>(13, 2, out _));
        memory.RollbackNestedCheckpoint();
        Assert.False(memory.TryGetResourcePointer(12, out _));
        Assert.Equal(Targets, memory.ReadExecutionReference<BattleExecutionReference.Targets>(13, 2));
        Assert.False(memory.IsKnown(13, 2));
        memory.RollbackCheckpoint();
        Assert.Equal(Header, memory.ReadExecutionReference<BattleExecutionReference.Header>(12, 4));
        Assert.Equal(0x06070809u, memory.ReadUInt32(12));
    }

    [Fact]
    public void Rejected_width_encoding_and_journal_reservation_leave_owner_unchanged()
    {
        using NativeBattleWorkingMemory memory = new(96, maxJournalEntries: 1);
        memory.WriteExecutionReference(10, 2, Targets, [4, 0]);
        long version = memory.Version;
        Assert.Throws<ArgumentOutOfRangeException>(() => memory.WriteExecutionReference(10, 3, Model));
        Assert.Throws<ArgumentException>(() => memory.WriteExecutionReference(10, 2, Model, [4]));
        memory.BeginCheckpoint();
        Assert.Throws<InvalidOperationException>(() => memory.WriteExecutionReference(10, 2, Model));
        Assert.Throws<InvalidOperationException>(() => memory.Write(10, [1, 2]));
        Assert.Equal(version, memory.Version);
        Assert.Equal(0, memory.JournalEntryCount);
        Assert.Equal(4, memory.ReadUInt16(10));
        Assert.Equal(Targets, memory.ReadExecutionReference<BattleExecutionReference.Targets>(10, 2));
        memory.RollbackCheckpoint();
    }

    [Fact]
    public void Explicit_arena_copy_transfers_only_full_references_even_when_overlapping()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        arena.WriteExecutionReference(10, 2, Targets, [1, 2]);
        arena.WriteExecutionReference(12, 4, Header);
        arena.CopyResourceSnapshot(arena, 10, 11, 5);
        Assert.Equal(Targets, arena.ReadExecutionReference<BattleExecutionReference.Targets>(11, 2));
        Assert.False(arena.TryReadExecutionReference<BattleExecutionReference.Targets>(10, 2, out _));
        Assert.False(arena.TryReadExecutionReference<BattleExecutionReference.Header>(13, 4, out _));
        Assert.Equal(new byte[] { 1, 2 }, arena.Read(11, 2));
        Assert.False(arena.ReadSnapshot(13, 3).Known[0]);

        arena.Working.Copy(11, 20, 2);
        Assert.Equal(new byte[] { 1, 2 }, arena.Read(20, 2));
        Assert.False(arena.TryReadExecutionReference<BattleExecutionReference.Targets>(20, 2, out _));
    }

    [Fact]
    public void Task_block_copy_does_not_transfer_a_reference_crossing_the_destination_header()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        source.WriteExecutionReference(0, 2, Targets, [2, 3]);
        source.WriteExecutionReference(32, 4, Header, [4, 0, 0, 0]);
        NativeBattleEffectTaskPool pool = Pool(0x200);
        NativeBattleEffectTaskMemory.WriteBankSpan(pool, 12, source, 36);

        Assert.Equal(Targets, pool.Slots[0].ReadExecutionReference<BattleExecutionReference.Targets>(12, 2));
        Assert.Equal(Header, pool.Slots[0].ReadExecutionReference<BattleExecutionReference.Header>(44, 4));
        Assert.Equal(4u, pool.Slots[0].ReadDword(44));

        NativeBattleEffectTaskMemory.WriteBankSpan(pool, 13, source, 36);
        Assert.Equal(Targets, pool.Slots[0].ReadExecutionReference<BattleExecutionReference.Targets>(13, 2));
        Assert.False(pool.Slots[0].TryReadExecutionReference<BattleExecutionReference.Targets>(12, 2, out _));
        Assert.False(pool.Slots[0].TryReadExecutionReference<BattleExecutionReference.Header>(44, 4, out _));
        Assert.Empty(pool.Slots[1].EnumerateExecutionReferences());
    }

    [Fact]
    public void Bound_task_and_overlay_share_live_references_and_roll_back_local_edits()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool pool = Pool(0x100);
        pool.Slots[0].WriteExecutionReference(12, 1, Model, [7]);
        pool.BindArena(arena);
        Assert.Equal(Model, arena.ReadExecutionReference<BattleExecutionReference.Model>(0x10C, 1));
        using NativeBattleEffectArenaTransaction transaction = new(arena);
        transaction.BeginCheckpoint();
        pool.Slots[0].WriteExecutionReference(12, 2, Targets);
        Assert.Equal(Targets, transaction.View.ReadExecutionReference<BattleExecutionReference.Targets>(0x10C, 2));
        transaction.RollbackCheckpoint();
        Assert.Equal(Model, pool.Slots[0].ReadExecutionReference<BattleExecutionReference.Model>(12, 1));
        transaction.View.WriteExecutionReference(16 + 0x100, 4, Header);
        transaction.Freeze();
        Assert.Equal(Header, pool.Slots[0].ReadExecutionReference<BattleExecutionReference.Header>(16, 4));
    }

    [Fact]
    public void Private_overlay_translates_references_and_restores_parent_changes_on_rollback()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        arena.WriteExecutionReference(20, 2, Targets);
        using NativeBattleEffectArenaTransaction parent = new(arena);
        NativeBattleEffectArena view = NativeBattleEffectArena.BorrowWorking(arena.Working, 16, 16);
        parent.BeginCheckpoint();
        using (NativeBattleEffectArenaTransaction child = NativeBattleEffectArenaTransaction.CreatePrivateChild(view))
        {
            Assert.Equal(Targets, child.View.ReadExecutionReference<BattleExecutionReference.Targets>(4, 2));
            child.View.WriteExecutionReference(5, 1, Model, [6]);
            child.Freeze();
        }
        Assert.False(parent.View.TryReadExecutionReference<BattleExecutionReference.Targets>(20, 2, out _));
        Assert.Equal(Model, parent.View.ReadExecutionReference<BattleExecutionReference.Model>(21, 1));
        parent.RollbackCheckpoint();
        Assert.Equal(Targets, parent.View.ReadExecutionReference<BattleExecutionReference.Targets>(20, 2));
        Assert.False(parent.View.TryReadExecutionReference<BattleExecutionReference.Model>(21, 1, out _));
    }

    [Fact]
    public void Scalar_overlay_store_removes_each_overlapping_reference_and_rollback_restores_ownership()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        arena.WriteExecutionReference(12, 4, Header, [9, 8, 7, 6]);
        arena.WriteExecutionReference(19, 1, Model, [7]);
        using NativeBattleEffectArenaTransaction parent = new(arena);
        parent.WriteExecutionReference(16, 2, Targets, [4, 0]);
        parent.WriteExecutionReference(18, 1, Model, [3]);
        NativeBattleEffectArena view = NativeBattleEffectArena.BorrowWorking(arena.Working, 12, 8);
        parent.BeginCheckpoint();
        using (NativeBattleEffectArenaTransaction child = NativeBattleEffectArenaTransaction.CreatePrivateChild(view))
        {
            child.Write(3, [1, 2, 3, 4]);
            Assert.False(child.View.TryReadExecutionReference<BattleExecutionReference.Header>(0, 4, out _));
            Assert.False(child.View.TryReadExecutionReference<BattleExecutionReference.Targets>(4, 2, out _));
            Assert.False(child.View.TryReadExecutionReference<BattleExecutionReference.Model>(6, 1, out _));
            Assert.Equal(Model, child.View.ReadExecutionReference<BattleExecutionReference.Model>(7, 1));
            child.Freeze();
        }
        Assert.False(parent.View.TryReadExecutionReference<BattleExecutionReference.Header>(12, 4, out _));
        Assert.False(parent.View.TryReadExecutionReference<BattleExecutionReference.Targets>(16, 2, out _));
        Assert.False(parent.View.TryReadExecutionReference<BattleExecutionReference.Model>(18, 1, out _));
        parent.RollbackCheckpoint();
        Assert.Equal(Header, parent.View.ReadExecutionReference<BattleExecutionReference.Header>(12, 4));
        Assert.Equal(Targets, parent.View.ReadExecutionReference<BattleExecutionReference.Targets>(16, 2));
        Assert.Equal(Model, parent.View.ReadExecutionReference<BattleExecutionReference.Model>(18, 1));
        Assert.Equal(new byte[] { 9, 8, 7, 6, 4, 0, 3, 7 }, parent.View.Read(12, 8));
    }

    [Theory]
    [InlineData(8, 0)]
    [InlineData(8, 8)]
    [InlineData(32, 0)]
    [InlineData(32, 8)]
    public void Range_overlay_stores_invalidate_crossing_references_and_preserve_checkpoint_ownership(int length, int offset)
    {
        const int origin = 64;
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 48; index++)
        {
            arena.WriteExecutionReference(512 + index * 4, 4, Header, [1, 2, 3, 4]);
        }
        if (offset != 0)
        {
            arena.WriteExecutionReference(origin, 2, Targets, [5, 6]);
            arena.WriteExecutionReference(origin + offset - 3, 4, Header, [9, 8, 7, 6]);
        }
        arena.WriteExecutionReference(origin + offset + 2, 2, Targets);
        arena.WriteExecutionReference(origin + offset + length - 1, 4, Header, [4, 3, 2, 1]);
        arena.WriteExecutionReference(origin + offset + length + 3, 1, Model, [7]);
        using NativeBattleEffectArenaTransaction parent = new(arena);
        parent.WriteExecutionReference(origin + offset + 2, 2, Model, null);
        parent.BeginCheckpoint();
        NativeBattleEffectArena view = NativeBattleEffectArena.BorrowWorking(arena.Working, origin, offset + length + 4);
        using (NativeBattleEffectArenaTransaction child = NativeBattleEffectArenaTransaction.CreatePrivateChild(view))
        {
            child.BeginCheckpoint();
            child.Write(offset, new byte[length]);
            AssertInvalidated(child.View);
            Assert.All(child.View.ReadSnapshot(offset, length).Known, value => Assert.True(value));
            child.RollbackCheckpoint();
            Assert.Equal(Model, child.View.ReadExecutionReference<BattleExecutionReference.Model>(offset + 2, 2));
            Assert.False(child.View.ReadSnapshot(offset + 2, 2).Known[0]);
            Assert.Equal(Header, child.View.ReadExecutionReference<BattleExecutionReference.Header>(offset + length - 1, 4));
            if (offset != 0)
            {
                Assert.Equal(Header, child.View.ReadExecutionReference<BattleExecutionReference.Header>(offset - 3, 4));
            }
            child.BeginCheckpoint();
            child.MarkUnknown(offset, length);
            child.MarkUnknown(offset, length);
            AssertInvalidated(child.View);
            Assert.All(child.View.ReadSnapshot(offset, length).Known, value => Assert.False(value));
            child.CommitCheckpoint();
            child.Freeze();
        }
        Assert.False(parent.View.TryReadExecutionReference<BattleExecutionReference.Model>(origin + offset + 2, 2, out _));
        Assert.False(parent.View.TryReadExecutionReference<BattleExecutionReference.Header>(origin + offset + length - 1, 4, out _));
        parent.RollbackCheckpoint();
        Assert.Equal(Model, parent.View.ReadExecutionReference<BattleExecutionReference.Model>(origin + offset + 2, 2));
        Assert.False(parent.View.ReadSnapshot(origin + offset + 2, 2).Known[0]);
        Assert.Equal(Header, parent.View.ReadExecutionReference<BattleExecutionReference.Header>(origin + offset + length - 1, 4));
        Assert.Equal(new byte[] { 4, 3, 2, 1 }, parent.View.Read(origin + offset + length - 1, 4));
        Assert.Equal(Header, parent.View.ReadExecutionReference<BattleExecutionReference.Header>(512, 4));

        void AssertInvalidated(NativeBattleEffectArena target)
        {
            Assert.False(target.TryReadExecutionReference<BattleExecutionReference.Model>(offset + 2, 2, out _));
            Assert.False(target.TryReadExecutionReference<BattleExecutionReference.Header>(offset + length - 1, 4, out _));
            Assert.Equal(Model, target.ReadExecutionReference<BattleExecutionReference.Model>(offset + length + 3, 1));
            if (offset != 0)
            {
                Assert.False(target.TryReadExecutionReference<BattleExecutionReference.Header>(offset - 3, 4, out _));
                Assert.Equal(Targets, target.ReadExecutionReference<BattleExecutionReference.Targets>(0, 2));
            }
        }
    }

    [Fact]
    public void In_memory_restore_keeps_aliases_and_JSON_retains_only_actual_bytes()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool pool = Pool(0x100).BindArena(arena);
        NativeBattleModulePoolTask task = pool.Slots[0];
        task.WriteExecutionReference(12, 1, Model, [7]);
        task.WriteExecutionReference(16, 4, Header);
        var frozen = NativeBattleStorageSnapshots.Capture(new Storage(arena, pool));
        var restored = NativeBattleStorageSnapshots.Restore(frozen);

        Assert.Same(restored.Arena.Working, restored.Pool.RequireOwner().Storage);
        Assert.Equal(Model, frozen.Pool.Slots[0].ReadExecutionReference<BattleExecutionReference.Model>(12, 1));
        Assert.Equal(Header, restored.Arena.ReadExecutionReference<BattleExecutionReference.Header>(0x110, 4));
        restored.Pool.Slots[0].WriteByte(12, 9);
        Assert.False(restored.Arena.TryReadExecutionReference<BattleExecutionReference.Model>(0x10C, 1, out _));
        Assert.Equal(Model, task.ReadExecutionReference<BattleExecutionReference.Model>(12, 1));

        string arenaJson = JsonSerializer.Serialize(frozen.Arena);
        string taskJson = JsonSerializer.Serialize(frozen.Pool.Slots[0]);
        Assert.DoesNotContain("ExecutionReferences", arenaJson);
        Assert.DoesNotContain("live-owner", arenaJson);
        Assert.DoesNotContain("ExecutionReferences", taskJson);
        Assert.DoesNotContain("live-owner", taskJson);
        NativeBattleEffectArena retainedArena = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectArena>(arenaJson)!);
        NativeBattleModulePoolTask retainedTask = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(taskJson)!;
        Assert.Equal(7, retainedArena.Read(0x10C, 1)[0]);
        Assert.False(retainedArena.ReadSnapshot(0x110, 4).Known[0]);
        Assert.Equal(7, retainedTask.ReadByte(12));
        Assert.False(retainedTask.ReadSnapshot(16, 4).Known[0]);
        Assert.False(retainedTask.TryReadExecutionReference<BattleExecutionReference.Model>(12, 1, out _));
    }

    [Fact]
    public void Shared_retained_graph_keeps_execution_references_only_in_process()
    {
        NativeBattleRetainedMemory cold = NativeBattleStorageSnapshots.Restore(NativeBattleRetainedMemory.FromColdProcess());
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        const int sourceOffset = 0x100;
        NativeBattleNodeSource source = NativeBattleNodeSource.Borrow(arena, sourceOffset, NativeBattleRecover93Nodes.SourceLength);
        arena.WriteExecutionReference(sourceOffset + 20, 2, Targets, [7, 8]);
        NativeBattleRecover93NodeState nodes = NativeBattleRecover93NodeState.FromColdProcess() with { Data = source };
        NativeBattleEffectMemory memory = cold.EffectMemory! with { Arena = arena, Recover93 = new(null, [], null, null, null, 0, 0, nodes) };
        NativeBattleRetainedMemory frozen = NativeBattleStorageSnapshots.Capture(cold with { EffectMemory = memory });
        Assert.Single(frozen.StorageGraph!.Authorities);

        NativeBattleRetainedMemory restored = NativeBattleStorageSnapshots.Restore(frozen);
        NativeBattleEffectArena main = restored.EffectMemory!.Arena!;
        NativeBattleEffectArena borrow = restored.EffectMemory.Recover93!.Nodes.Data!.Arena;
        Assert.Same(main.Working, borrow.Working);
        Assert.Equal(Targets, borrow.ReadExecutionReference<BattleExecutionReference.Targets>(20, 2));
        main.Write(sourceOffset + 21, [9]);
        Assert.False(borrow.TryReadExecutionReference<BattleExecutionReference.Targets>(20, 2, out _));
        Assert.Equal(Targets, arena.ReadExecutionReference<BattleExecutionReference.Targets>(sourceOffset + 20, 2));

        string json = JsonSerializer.Serialize(frozen);
        Assert.DoesNotContain("live-owner", json);
        NativeBattleRetainedMemory persisted = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleRetainedMemory>(json)!);
        Assert.False(persisted.EffectMemory!.Arena!.TryReadExecutionReference<BattleExecutionReference.Targets>(sourceOffset + 20, 2, out _));
        Assert.Equal(new byte[] { 7, 8 }, persisted.EffectMemory.Arena.Read(sourceOffset + 20, 2));
    }

    private static NativeBattleEffectTaskPool Pool(int offset) => NativeBattleEffectTaskPool.Initialize(0x2521950, 0x20DFAB8u + (uint)offset, 48, 2);

    private sealed record Storage(NativeBattleEffectArena Arena, NativeBattleEffectTaskPool Pool);
}
