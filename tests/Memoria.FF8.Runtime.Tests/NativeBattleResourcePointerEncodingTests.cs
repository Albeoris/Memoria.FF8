namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleResourcePointerEncodingTests
{
    [Fact]
    public void Pointer_presence_preserves_explicit_null_collisions_word_boundaries_and_snapshot_undo()
    {
        using NativeBattleWorkingMemory memory = new(16384);
        memory.SetResourcePointer(60, null);
        memory.SetResourcePointer(4093, null);
        memory.BeginCheckpoint();
        memory.Write(32, [1]);
        Assert.True(memory.TryGetResourcePointer(60, out _));
        memory.Write(4096, [2]);
        Assert.False(memory.TryGetResourcePointer(4093, out _));
        memory.RollbackCheckpoint();
        Assert.True(memory.TryGetResourcePointer(4093, out _));
        using NativeBattleWorkingMemory restored = NativeBattleWorkingMemory.Restore(memory.Capture());
        restored.Write(4096, [3]);
        Assert.False(restored.TryGetResourcePointer(4093, out _));
        restored.SetResourcePointer(8192, null);
        restored.Write(8193, [4]);
        restored.Write(512, [5]);
        Assert.False(restored.TryGetResourcePointer(8192, out _));
        Assert.True(restored.TryGetResourcePointer(60, out NativeBattleResourcePointer? explicitNull));
        Assert.Null(explicitNull);
        memory.Write(0, new byte[8193]);
        Assert.False(memory.TryGetResourcePointer(4093, out _));
    }

    [Fact]
    public void Private_pointer_presence_tracks_late_null_ancestors_unaligned_slices_and_overlay_reuse()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        arena.WriteResourcePointer(4093, null);
        using NativeBattleEffectArenaTransaction parent = new(arena);
        NativeBattleEffectArena slice = NativeBattleEffectArena.BorrowWorking(arena.Working, 4061, 128);
        using (NativeBattleEffectArenaTransaction child = NativeBattleEffectArenaTransaction.CreatePrivateChild(slice))
        {
            child.Write(0, [1]);
            parent.View.WriteResourcePointer(4125, null);
            parent.BeginCheckpoint();
            child.Write(65, [2]);
            child.Write(35, [3]);
            child.View.WriteResourcePointer(96, null);
            child.Write(97, [4]);
            Assert.False(child.View.TryGetResourcePointer(64, out _));
            Assert.False(child.View.TryGetResourcePointer(32, out _));
            Assert.False(child.View.TryGetResourcePointer(96, out _));
            child.Freeze();
        }
        Assert.False(parent.View.TryGetResourcePointer(4093, out _));
        parent.RollbackCheckpoint();
        Assert.True(parent.View.TryGetResourcePointer(4093, out _));
        Assert.True(parent.View.TryGetResourcePointer(4125, out NativeBattleResourcePointer? explicitNull));
        Assert.Null(explicitNull);
        parent.Freeze();
        parent.Write(4096, [5]);
        Assert.False(parent.View.TryGetResourcePointer(4093, out _));
        parent.Freeze();
        Assert.False(arena.TryGetResourcePointer(4093, out _));
    }

    [Fact]
    public void Working_pointer_collection_matches_sparse_and_dense_owner_overlap_and_rollback()
    {
        (int Offset, int Length)[] stores = [(0, 1), (4, 1), (7, 1), (7, 2), (11, 1), (12, 1), (1023, 1), (1019, 4), (1017, 7), (0, 32), (0, 1024), (1024, 0), (9, 0)];
        foreach ((int offset, int length) in stores)
        {
            foreach (bool unknown in new[] { false, true })
            {
                foreach (int extraPointers in new[] { 0, 64 })
                {
                    using NativeBattleWorkingMemory memory = new(1024);
                    NativeBattleResourcePointer first = new(null, 0x11223344);
                    NativeBattleResourcePointer last = new(null, 0x55667788);
                    memory.SetResourcePointer(0, first);
                    memory.SetResourcePointer(8, null);
                    memory.SetResourcePointer(1020, last);
                    for (int index = 0; index < extraPointers; index++)
                    {
                        memory.SetResourcePointer(512 + index * 4, null);
                    }
                    int[] starts = [0, 8, 1020];
                    NativeBattleResourcePointer?[] originals = [first, null, last];
                    byte[] bytes = new byte[1024];
                    bool[] known = new bool[1024];
                    memory.ReadWithKnownness(0, bytes, known);
                    memory.BeginCheckpoint();
                    if (unknown)
                    {
                        memory.MarkUnknown(offset, length);
                    }
                    else
                    {
                        memory.Write(offset, new byte[length]);
                    }
                    for (int index = 0; index < starts.Length; index++)
                    {
                        bool overlaps = length != 0 && starts[index] < (long)offset + length && starts[index] + 4L > offset;
                        Assert.Equal(!overlaps, memory.TryGetResourcePointer(starts[index], out NativeBattleResourcePointer? pointer));
                        Assert.Equal(overlaps ? null : originals[index], pointer);
                    }
                    memory.RollbackCheckpoint();
                    for (int index = 0; index < starts.Length; index++)
                    {
                        Assert.True(memory.TryGetResourcePointer(starts[index], out NativeBattleResourcePointer? pointer));
                        Assert.Equal(originals[index], pointer);
                    }
                    byte[] restoredBytes = new byte[1024];
                    bool[] restoredKnown = new bool[1024];
                    memory.ReadWithKnownness(0, restoredBytes, restoredKnown);
                    Assert.Equal(bytes, restoredBytes);
                    Assert.Equal(known, restoredKnown);
                }
            }
        }
    }

    [Fact]
    public void Nonzero_party_bank_encodes_classic_placement_without_changing_resource_ownership()
    {
        NativeBattleResourcePointer pointer = new(new(2, 0x1234) { ResourceOnly = true }, null, 16);
        NativeBattleResourcePointer encoded = pointer.WithNativeAddressEncoding();
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().WriteResourcePointer(12, encoded);

        Assert.Equal(0x0198C24Cu, arena.ReadUInt32(12));
        Assert.Same(pointer.Cpu, encoded.Cpu);
        Assert.Null(encoded.Cpu!.TryPhysicalAddress());
        Assert.Equal(16, encoded.Length);
        Assert.Equal(encoded, arena.ReadResourcePointer(12));
    }

    [Fact]
    public void Owned_enemy_encodes_native_heap_placement_instead_of_resource_storage_offset()
    {
        NativeBattleModelCpuReference reference = new(3, 0x1234) { EnemyResourceId = 4, ResourceOffset = 7 };
        NativeBattleResourcePointer encoded = new NativeBattleResourcePointer(reference, null, 8).WithNativeAddressEncoding();
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().WriteResourcePointer(0, encoded);

        Assert.Equal(0x0193A24Cu, arena.ReadUInt32(0));
        Assert.Null(reference.TryPhysicalAddress());
        Assert.Equal(7, arena.ReadResourcePointer(0)!.Cpu!.ResourceStorageOffset);
        Assert.Equal(4, arena.ReadResourcePointer(0)!.Cpu!.EnemyResourceId);
    }

    [Fact]
    public void Arena_transaction_publication_and_json_keep_encoded_bytes_and_owner()
    {
        NativeBattleResourcePointer pointer = new NativeBattleResourcePointer(new(1, 0x28) { ResourceOnly = true }, null).WithNativeAddressEncoding();
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        using (NativeBattleEffectArenaTransaction transaction = new(arena))
        {
            transaction.View.WriteResourcePointer(8, pointer);
            Assert.Equal(0x0197B040u, transaction.View.ReadUInt32(8));
            transaction.Freeze();
        }
        NativeBattleEffectArena restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(arena));

        Assert.Equal(0x0197B040u, arena.ReadUInt32(8));
        Assert.Equal(arena.ReadUInt32(8), restored.ReadUInt32(8));
        Assert.Equal(pointer, restored.ReadResourcePointer(8));
    }

    [Fact]
    public void Staging_json_keeps_encoded_bytes_and_resource_pointer()
    {
        const uint address = 0x24F9950;
        NativeBattleResourcePointer pointer = new NativeBattleResourcePointer(new(1, 0x28) { ResourceOnly = true }, null).WithNativeAddressEncoding();
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().WriteResourcePointer(address, pointer);
        NativeBattleFileStaging restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(staging));

        Assert.Equal(0x0197B040u, restored.ReadUInt32(address));
        Assert.Equal(pointer, restored.ReadResourcePointer(address));
    }

    [Fact]
    public void Legacy_staging_without_opt_in_restores_symbolic_owner_and_unknown_bytes()
    {
        const uint address = 0x24F9950;
        NativeBattleResourcePointer pointer = new(new(1, 0x28) { ResourceOnly = true }, null);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().WriteResourcePointer(address, pointer);
        string json = NativeBattleTestSnapshots.Json(staging);
        Assert.DoesNotContain("NativeAddressEncoding", json);
        NativeBattleFileStaging restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(json);

        Assert.Equal(pointer, restored.ReadResourcePointer(address));
        Assert.All(restored.ReadSnapshot(address, 4).Known, known => Assert.False(known));
        Assert.Throws<NotSupportedException>(() => restored.ReadUInt32(address));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Partial_dword_overwrite_invalidates_owner_without_losing_other_encoded_bytes(bool stagingStorage)
    {
        NativeBattleResourcePointer pointer = new NativeBattleResourcePointer(new(1, 0x28) { ResourceOnly = true }, null).WithNativeAddressEncoding();
        if (stagingStorage)
        {
            const uint address = 0x24F9950;
            NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().WriteResourcePointer(address, pointer);
            staging.WriteByte(address + 1, 0xAB);

            Assert.False(staging.TryGetResourcePointerMetadata(address, out _));
            Assert.Equal(new byte[] { 0x40, 0xAB, 0x97, 0x01 }, staging.Read(address, 4));
            Assert.Null(staging.ReadResourcePointer(address)!.Cpu);
        }
        else
        {
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().WriteResourcePointer(8, pointer);
            arena.Write(9, [0xAB]);

            Assert.False(arena.TryGetResourcePointer(8, out _));
            Assert.Equal(new byte[] { 0x40, 0xAB, 0x97, 0x01 }, arena.Read(8, 4));
            Assert.Null(arena.ReadResourcePointer(8)!.Cpu);
        }
    }
}
