namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleResourcePointerEncodingTests
{
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
