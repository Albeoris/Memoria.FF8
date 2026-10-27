using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectTaskWorkingStorageTests
{
    [Fact]
    public void Typed_stores_keep_the_same_slot_and_bank_with_traffic_proportional_to_changed_bytes()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 1024, 4);
        NativeBattleModulePoolTask slot = pool.Slots[0];
        object identity = pool.StorageIdentity;
        long before = pool.RequireOwner().Storage.BytesWritten;
        for (uint value = 0; value < 1000; value++)
        {
            Assert.Same(slot, slot.WriteDword(12, value));
            Assert.Same(pool, pool.WriteSlot(0, slot));
        }
        Assert.Same(identity, pool.StorageIdentity);
        Assert.Same(slot, pool.Slots[0]);
        Assert.Equal(4000L, pool.RequireOwner().Storage.BytesWritten - before);
        Assert.Equal(999u, slot.ReadDword(12));
        Assert.Throws<InvalidOperationException>(() => _ = slot.Payload);
        Assert.Throws<InvalidOperationException>(() => _ = slot with { Flags = 1 });
    }

    [Fact]
    public void Snapshot_and_two_restores_detach_bytes_headers_knownness_and_pointer_metadata()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 32, 2);
        pool.Append(0x1234);
        pool.Slots[0].WriteResourcePointer(12, new(null, 0x12345678));
        pool.Slots[0].WriteByte(20, 0xAB).MarkPayloadUnknown(20, 1);
        NativeBattleEffectTaskPool snapshot = NativeBattleStorageSnapshots.Capture(pool);
        string json = JsonSerializer.Serialize(snapshot);
        Assert.Throws<InvalidOperationException>(() => snapshot.Slots[0].WriteByte(12, 1));
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(pool));
        NativeBattleEffectTaskPool left = NativeBattleStorageSnapshots.Restore(snapshot);
        NativeBattleEffectTaskPool right = NativeBattleStorageSnapshots.Restore(snapshot);
        left.Slots[0].WriteByte(13, 0xFF);
        left.UpdateLinks(null, null);
        Assert.NotSame(left.StorageIdentity, right.StorageIdentity);
        Assert.Equal(0x12345678u, right.Slots[0].ReadResourcePointer(12).StagingAddress);
        Assert.Empty(left.Slots[0].EnumerateResourcePointers());
        Assert.Equal(0, right.Head);
        Assert.Equal(json, JsonSerializer.Serialize(snapshot));
        var hidden = right.Slots[0].ReadSnapshot(20, 1);
        Assert.Equal((byte)0xAB, hidden.Data[0]);
        Assert.False(hidden.Known[0]);
        NativeBattleEffectTaskPool loaded = JsonSerializer.Deserialize<NativeBattleEffectTaskPool>(json)!;
        Assert.True(loaded.IsSnapshot);
        Assert.Equal(json, JsonSerializer.Serialize(loaded));
    }

    [Fact]
    public void Borrowed_bank_and_slot_restore_one_shared_authority()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 32, 2);
        NativeBattleEffectArena view = NativeBattleEffectTaskMemory.CaptureBank(pool);
        var frozen = NativeBattleStorageSnapshots.Capture(new BankGraph(pool, pool.Slots[1], view));
        var restored = NativeBattleStorageSnapshots.Restore(frozen);
        Assert.Same(restored.Pool.Slots[1], restored.Task);
        restored.Task.WriteDword(12, 0xAABBCCDD);
        Assert.Equal(0xAABBCCDDu, restored.View.ReadUInt32(32 + 12));
        restored.View.WriteUInt32(32 + 16, 0x11223344);
        Assert.Equal(0x11223344u, restored.Task.ReadDword(16));
        Assert.Equal(0u, frozen.Task.ReadDword(12));
    }

    [Fact]
    public void Pool_checkpoint_rolls_back_payload_headers_descriptor_and_same_pass_children()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 32, 3);
        pool.Append(0x1234);
        pool.Slots[0].WriteDword(12, 7);
        pool.BeginCheckpoint();
        List<int> visited = [];
        pool.Advance((current, slot) =>
        {
            visited.Add(slot);
            current.Slots[slot].WriteDword(12, 99);
            if (slot == 0)
            {
                current.Append(0x5678);
            }
            return new(current, 0);
        });
        Assert.Equal(new[] { 0, 1 }, visited);
        Assert.Throws<InvalidOperationException>(() => NativeBattleStorageSnapshots.Capture(pool));
        pool.RollbackCheckpoint();
        Assert.Equal(0, pool.Head);
        Assert.Equal(0, pool.Tail);
        Assert.Null(pool.Slots[0].Next);
        Assert.Equal(7u, pool.Slots[0].ReadDword(12));
        Assert.Equal((ushort)0, pool.Slots[1].Flags);
        Assert.Equal(0u, pool.Slots[1].ReadDword(12));
    }

    [Fact]
    public void Late_invalid_bank_header_leaves_earlier_payload_unpublished_even_inside_a_checkpoint()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 32, 2);
        pool.Slots[0].WriteDword(28, 0x12345678);
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        source.WriteUInt32(0, 0xFFFFFFFF);
        source.WriteUInt32(8, 0x2001);
        pool.BeginCheckpoint();
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTaskMemory.WriteBankSpan(pool, 28, source, 12));
        Assert.Equal(0x12345678u, pool.Slots[0].ReadDword(28));
        pool.RollbackCheckpoint();
    }

    [Fact]
    public void Retained_stores_preserve_hidden_knownness_while_native_clear_makes_actual_bytes_known()
    {
        NativeBattleModulePoolTask task = new(0, 0, null, 0, new byte[32].ToImmutableArray());
        task.WriteDword(12, 0x12345678).MarkPayloadUnknown(13, 2);
        task.WriteRetainedBytes(12, [1, 2, 3, 4]);
        var retained = task.ReadSnapshot(12, 4);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, retained.Data);
        Assert.Equal(new[] { true, false, false, true }, retained.Known);
        Assert.Throws<InvalidDataException>(() => task.ClearPayload(task.PayloadLength - 1));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, task.ReadSnapshot(12, 4).Data);
        task.ClearPayload(task.PayloadLength);
        Assert.Equal(0u, task.ReadDword(12));
        Assert.All(task.ReadSnapshot(12, task.PayloadLength).Known, known => Assert.True(known));
    }

    private sealed record BankGraph(NativeBattleEffectTaskPool Pool, NativeBattleModulePoolTask Task, NativeBattleEffectArena View);
}
