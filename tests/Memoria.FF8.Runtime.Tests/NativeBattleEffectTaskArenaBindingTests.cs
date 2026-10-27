using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectTaskArenaBindingTests
{
    [Fact]
    public void Binding_uses_one_body_authority_revokes_private_handles_and_keeps_descriptor_outside_native_bytes()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        const int offset = 0x100;
        NativeBattleEffectTaskPool pool = Pool(offset);
        pool.Append(0x1234);
        NativeBattleModulePoolTask task = pool.Slots[0];
        task.WriteDword(12, 0x11223344).WriteByte(20, 0xAB).MarkPayloadUnknown(20, 1);
        NativeBattleEffectArena oldPrivate = NativeBattleEffectTaskMemory.CaptureBank(pool);
        arena.WriteUInt32(offset + 64, 0xDEADBEEF);

        Assert.Same(pool, pool.BindArena(arena));
        Assert.Same(task, pool.Slots[0]);
        Assert.Same(arena.Working.BackingIdentity, pool.StorageIdentity);
        Assert.Throws<ObjectDisposedException>(() => oldPrivate.ReadUInt32(12));
        task.WriteDword(12, 0x55667788);
        Assert.Equal(0x55667788u, arena.ReadUInt32(offset + 12));
        arena.WriteUInt32(offset + 16, 0xAABBCCDD);
        Assert.Equal(0xAABBCCDDu, task.ReadDword(16));
        pool.Append(0x5678);
        Assert.Equal(0xDEADBEEFu, arena.ReadUInt32(offset + 64));
        Assert.Equal(1, pool.Tail);
        Assert.Same(pool, pool.BindArena(arena));
        pool.Revoke();
        Assert.Throws<ObjectDisposedException>(() => task.ReadDword(12));
        NativeBattleEffectArena snapshot = NativeBattleStorageSnapshots.Capture(arena);
        string json = JsonSerializer.Serialize(snapshot);
        NativeBattleEffectArena loaded = JsonSerializer.Deserialize<NativeBattleEffectArena>(json)!;
        NativeBattleEffectArena restored = NativeBattleStorageSnapshots.Restore(loaded);
        Assert.Equal(0x55667788u, restored.ReadUInt32(offset + 12));
        var hidden = restored.ReadSnapshot(offset + 20, 1);
        Assert.Equal((byte)0xAB, hidden.Data[0]);
        Assert.False(hidden.Known[0]);
    }

    [Fact]
    public void Nested_shared_body_checkpoints_restore_both_pools_descriptors_and_parent_changes()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool parent = Pool(0x100).BindArena(arena);
        NativeBattleEffectTaskPool child = Pool(0x200).BindArena(arena);
        parent.BeginCheckpoint();
        parent.Append(0x1234);
        parent.Slots[0].WriteDword(12, 10);
        child.BeginCheckpoint();
        child.Append(0x5678);
        child.Slots[0].WriteDword(12, 20);
        parent.Append(0x9ABC);
        Assert.Throws<InvalidOperationException>(() => parent.CommitCheckpoint());

        child.RollbackCheckpoint();

        Assert.Null(child.Head);
        Assert.Null(child.Tail);
        Assert.Equal((ushort)0, child.Slots[0].Flags);
        Assert.Equal(0u, child.Slots[0].ReadDword(12));
        Assert.Equal(0, parent.Tail);
        Assert.Null(parent.Slots[0].Next);
        Assert.Equal(10u, parent.Slots[0].ReadDword(12));
        Assert.True(parent.HasCheckpoint);
        Assert.False(child.HasCheckpoint);
        parent.CommitCheckpoint();
        Assert.False(parent.HasCheckpoint);
        Assert.Equal(10u, arena.ReadUInt32(0x100 + 12));
    }

    [Fact]
    public void External_kernel_rollback_restores_descriptors_and_invalidates_nested_local_scope_markers()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool pool = Pool(0x100).BindArena(arena);
        arena.Working.BeginCheckpoint();
        pool.BeginCheckpoint();
        pool.Append(0x1234);
        pool.Slots[0].WriteDword(12, 77);

        arena.Working.RollbackCheckpoint();

        Assert.False(pool.HasCheckpoint);
        Assert.Null(pool.Head);
        Assert.Null(pool.Tail);
        Assert.Equal(0u, pool.Slots[0].ReadDword(12));
        pool.BeginCheckpoint();
        pool.Append(0x1234);
        pool.CommitCheckpoint();
        Assert.Equal(0, pool.Head);
    }

    [Fact]
    public void Public_arena_transaction_is_the_effective_authority_for_task_and_borrowed_bank_reads_and_writes()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool pool = Pool(0x100).BindArena(arena);
        NativeBattleModulePoolTask task = pool.Slots[0];
        using NativeBattleEffectArenaTransaction transaction = new(arena);
        transaction.BeginCheckpoint();
        pool.BeginCheckpoint();
        pool.Append(0x1234);
        task.WriteDword(12, 0x11223344);
        transaction.View.WriteUInt32(0x100 + 16, 0xAABBCCDD);
        NativeBattleEffectArena borrowed = NativeBattleEffectTaskMemory.CaptureBank(pool);
        borrowed.WriteUInt32(24, 0x12345678);
        Assert.Equal(0x11223344u, transaction.View.ReadUInt32(0x100 + 12));
        Assert.Equal(0xAABBCCDDu, task.ReadDword(16));
        Assert.Equal(0x12345678u, task.ReadDword(24));
        Assert.Equal(0x11223344u, BinaryPrimitives.ReadUInt32LittleEndian(task.ReadRawPayloadSpan()));
        Assert.Equal(0u, arena.ReadUInt32(0x100 + 12));
        Assert.Same(pool, pool.BindArena(transaction.View));
        Assert.Throws<InvalidOperationException>(() => NativeBattleStorageSnapshots.Capture(pool));

        transaction.RollbackCheckpoint();
        pool.RollbackCheckpoint();
        Assert.Null(pool.Head);
        Assert.Equal(0u, task.ReadDword(12));
        transaction.Freeze();
        Assert.True(NativeBattleStorageSnapshots.Capture(pool).IsSnapshot);

        transaction.BeginCheckpoint();
        pool.BeginCheckpoint();
        pool.Append(0x1234);
        task.WriteDword(12, 0x55667788);
        pool.CommitCheckpoint();
        transaction.CommitCheckpoint();
        transaction.Freeze();
        Assert.Equal(0x55667788u, arena.ReadUInt32(0x100 + 12));
        Assert.Equal(0, pool.Head);
    }

    [Fact]
    public void Capture_restore_shares_main_body_but_detaches_descriptors_and_json_rebinds_explicitly()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool pool = Pool(0x100).BindArena(arena);
        pool.Append(0x1234);
        pool.Slots[0].WriteDword(12, 17);
        BindingGraph frozen = NativeBattleStorageSnapshots.Capture(new BindingGraph(arena, pool));
        BindingGraph left = NativeBattleStorageSnapshots.Restore(frozen);
        BindingGraph right = NativeBattleStorageSnapshots.Restore(frozen);
        left.Pool.Slots[0].WriteDword(12, 99);
        left.Pool.Append(0x5678);
        Assert.Equal(99u, left.Arena.ReadUInt32(0x100 + 12));
        Assert.Equal(17u, right.Arena.ReadUInt32(0x100 + 12));
        Assert.Equal(0, right.Pool.Tail);
        Assert.NotSame(left.Pool.StorageIdentity, right.Pool.StorageIdentity);
        NativeBattleEffectArena jsonArena = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(frozen.Arena))!);
        NativeBattleEffectTaskPool jsonPool = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTaskPool>(JsonSerializer.Serialize(frozen.Pool))!);
        jsonPool.BindArena(jsonArena);
        jsonPool.Slots[0].WriteDword(12, 123);
        Assert.Equal(123u, jsonArena.ReadUInt32(0x100 + 12));
    }

    [Fact]
    public void Explicit_generic_executable_binding_and_partial_transaction_failures_preserve_physical_boundaries()
    {
        NativeBattleEffectArena executable = NativeBattleEffectArena.CreateUnknown(NativeBattleQuezacotlExecutableMemory.Length);
        NativeBattleEffectTaskPool fragment = NativeBattleEffectTaskPool.Initialize(NativeBattleQuezacotlFragmentPool.Descriptor, NativeBattleQuezacotlFragmentPool.Bank, NativeBattleQuezacotlFragmentPool.Stride, NativeBattleQuezacotlFragmentPool.Capacity);
        fragment.BindStorage(executable, NativeBattleQuezacotlExecutableMemory.Address);
        fragment.Append(NativeBattleQuezacotlFragmentPool.Callback);
        fragment.Slots[0].WriteDword(12, 0xAABBCCDD);
        int fragmentOffset = checked((int)(NativeBattleQuezacotlFragmentPool.Bank - NativeBattleQuezacotlExecutableMemory.Address));
        Assert.Equal(0xAABBCCDDu, executable.ReadUInt32(fragmentOffset + 12));

        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool pool = Pool(0x100).BindArena(arena);
        NativeBattleEffectArena slice = NativeBattleEffectArena.BorrowWorking(arena.Working, 0x100 + 12, 2);
        using (NativeBattleEffectArenaTransaction transaction = new(slice))
        {
            transaction.View.WriteByte(0, 0xAA);
            Assert.Equal((byte)0xAA, pool.Slots[0].ReadByte(12));
            Assert.Equal((byte)0, pool.Slots[0].ReadByte(16));
            Assert.Throws<NotSupportedException>(() => pool.Slots[0].WriteDword(12, 0xFFFFFFFF));
            Assert.Throws<InvalidOperationException>(() => pool.Append(0x1234));
            Assert.Equal((ushort)0, pool.Slots[0].Flags);
            transaction.Freeze();
        }
        Assert.Equal(0xAAu, pool.Slots[0].ReadDword(12));
    }

    [Fact]
    public void Adoption_is_explicit_validates_before_transfer_and_binding_never_commits_an_active_overlay()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool pool = Pool(0x100);
        NativeBattleEffectArena privateView = NativeBattleEffectTaskMemory.CaptureBank(pool);
        arena.WriteUInt32(0x100 + 4, 0xDEADBEEF);
        Assert.Throws<InvalidDataException>(() => pool.BindArena(arena, NativeBattleTaskStorageBinding.AdoptPhysicalState));
        Assert.False(pool.IsBoundToStorage(arena, NativeBattleEffectNodeMemory.ArenaAddress));
        Assert.Equal(0u, privateView.ReadUInt32(4));
        arena.WriteUInt32(0x100 + 4, 0);
        arena.WriteUInt32(0x100 + 12, 0x11223344);
        using (NativeBattleEffectArenaTransaction transaction = new(arena))
        {
            Assert.Throws<InvalidOperationException>(() => pool.BindArena(arena));
            Assert.Equal(0u, privateView.ReadUInt32(12));
        }
        pool.BindArena(arena, NativeBattleTaskStorageBinding.AdoptPhysicalState);
        Assert.Equal(0x11223344u, pool.Slots[0].ReadDword(12));
        pool.Append(0x1234);
        Assert.Equal(0x11223344u, pool.Slots[0].ReadDword(12));
        Assert.Throws<InvalidOperationException>(() => pool.BindArena(NativeBattleEffectArena.Clear()));
        Assert.Equal(0x11223344u, arena.ReadUInt32(0x100 + 12));
    }

    [Fact]
    public void Bound_retirement_is_a_reversible_logical_handoff_and_never_revokes_shared_arena_bytes()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool pool = Pool(0x100).BindArena(arena);
        NativeBattleModulePoolTask task = pool.Slots[0];
        task.WriteDword(12, 17);
        pool.BeginCheckpoint();
        task.WriteDword(12, 99);
        pool.Revoke();
        Assert.True(pool.HasCheckpoint);
        Assert.Throws<ObjectDisposedException>(() => task.ReadDword(12));

        pool.RollbackCheckpoint();

        Assert.False(pool.HasCheckpoint);
        Assert.Equal(17u, task.ReadDword(12));
        Assert.Equal(17u, arena.ReadUInt32(0x100 + 12));
        pool.BeginCheckpoint();
        pool.Revoke();
        pool.CommitCheckpoint();
        Assert.False(pool.HasCheckpoint);
        Assert.Throws<ObjectDisposedException>(() => task.ReadDword(12));
        Assert.Equal(17u, arena.ReadUInt32(0x100 + 12));
    }

    [Fact]
    public void Raw_payload_spans_from_distinct_slots_survive_other_reads_under_an_active_overlay()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool pool = Pool(0x100).BindArena(arena);
        using NativeBattleEffectArenaTransaction transaction = new(arena);
        transaction.View.WriteUInt32(0x100 + 12, 0x11223344);
        transaction.View.WriteUInt32(0x100 + 32 + 12, 0xAABBCCDD);

        ReadOnlySpan<byte> first = pool.Slots[0].ReadRawPayloadSpan();
        ReadOnlySpan<byte> second = pool.Slots[1].ReadRawPayloadSpan();

        Assert.Equal(0x11223344u, BinaryPrimitives.ReadUInt32LittleEndian(first));
        Assert.Equal(0xAABBCCDDu, BinaryPrimitives.ReadUInt32LittleEndian(second));
        Assert.Equal(0u, arena.ReadUInt32(0x100 + 12));
    }

    private static NativeBattleEffectTaskPool Pool(int offset) => NativeBattleEffectTaskPool.Initialize(0x3000000u + (uint)offset, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset), 32, 2);
    private sealed record BindingGraph(NativeBattleEffectArena Arena, NativeBattleEffectTaskPool Pool);
}
