using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleElectricDischarge126ResourceRowTests
{
    private const uint Bank = NativeBattleEffectNodeMemory.ArenaAddress + 0x4000;

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(10)]
    public void CPU_row_retains_typed_identity_and_unresolved_numeric_DWORD_through_JSON(int row)
    {
        NativeBattleModulePoolTask task = Pool().Slots[0].WriteDword(36, 0xDEADBEEF).WriteResourceRowReference(36, new(row, null));
        Assert.Equal(new NativeBattleResourceRowReference(row, null), task.ReadResourceRowReference(36));
        Assert.Throws<NotSupportedException>(() => task.ReadDword(36));
        NativeBattleModulePoolTask restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(task)))!);
        Assert.Equal(task.ReadResourceRowReference(36), restored.ReadResourceRowReference(36));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(36));
        Assert.NotNull(NativeBattleStorageSnapshots.Capture(restored.WriteByte(40, 1)).ResourcePointers);
        Assert.Null(NativeBattleStorageSnapshots.Capture(restored.WriteByte(39, 1)).ResourcePointers);
        Assert.Null(NativeBattleStorageSnapshots.Capture(restored.MarkPayloadUnknown(36, 4)).ResourcePointers);
        Assert.Equal(new NativeBattleResourceRowReference(null, 0x12340000u), restored.WriteDword(36, 0x12340000).ReadResourceRowReference(36));
    }

    [Fact]
    public void Staging_row_uses_its_actual_numeric_DWORD_and_CPU_geometry_is_not_a_row()
    {
        NativeBattleModulePoolTask task = Pool().Slots[0].WriteResourceRowReference(36, new(null, 0x2000010));
        Assert.Equal(0x2000010u, task.ReadDword(36));
        Assert.Equal(new NativeBattleResourceRowReference(null, 0x2000010u), task.ReadResourceRowReference(36));
        task = task.WriteResourcePointer(36, new(new(0, 100), null));
        Assert.Equal(0x196B07Cu, task.ReadDword(36));
        Assert.Throws<NotSupportedException>(() => task.ReadResourceRowReference(36));
        Assert.DoesNotContain("CpuRow", JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(task)));
    }

    [Fact]
    public void Mixed_owners_invalid_row_indices_and_fabricated_numeric_row_bytes_fail()
    {
        NativeBattleModulePoolTask task = Pool().Slots[0];
        Assert.Throws<NotSupportedException>(() => task.WriteResourceRowReference(36, new(0, 0x2000000)));
        Assert.Throws<NotSupportedException>(() => task.WriteResourceRowReference(36, new(-1, null)));
        Assert.Throws<NotSupportedException>(() => task.WriteResourceRowReference(36, new(11, null)));
        Assert.Throws<NotSupportedException>(() => task.WriteResourceRowReference(36, new(null, null)));
        task = task.WriteResourceRowReference(36, new(3, null));
        Assert.Throws<InvalidDataException>(() => NativeBattleTaskFixture.EditTask(task, data => data with { Unknown = null }).ReadResourceRowReference(36));
        Assert.Throws<InvalidDataException>(() => task.WriteResourceRowReference(62, new(0, null)));
    }

    [Fact]
    public void Closed_task_bank_transfers_symbolic_row_to_the_single_arena_owner()
    {
        NativeBattleEffectTaskPool pool = Pool();
        pool = pool.WriteSlot(0, pool.Slots[0].WriteResourceRowReference(36, new(3, null)));
        NativeBattleEffectMemory memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, [pool]);
        NativeBattleOriginalEffectState effect = new(126, 125, new(126, NativeBattleElectricDischarge126Profile.Entry, NativeBattleElectricDischarge126Profile.Initializer), new(0, 0, 8, 0, 0, 0), 0) { Pools = [pool] };
        NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(memory, effect);
        Assert.Empty(transfer.Effect.Pools);
        Assert.Equal(3, transfer.Memory.Arena!.ReadResourcePointer(0x4024)!.CpuRow);
        Assert.Throws<NotSupportedException>(() => transfer.Memory.Arena.Read(0x4024, 4));
        NativeBattleEffectArena restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(transfer.Memory.Arena)))!);
        restored.Validate();
        Assert.Equal(3, restored.ReadResourcePointer(0x4024)!.CpuRow);
        Assert.Throws<NotSupportedException>(() => restored.Read(0x4024, 4));
        Assert.Equal(0u, restored.Write(0x4024, new byte[4]).ReadResourcePointer(0x4024)!.StagingAddress);
    }

    [Fact]
    public void Arena_transaction_checkpoints_preserve_row_identity_and_authoritative_overwrites()
    {
        NativeBattleResourcePointer row = new(null, null) { CpuRow = 3 };
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().WriteResourcePointer(16, row);
        using NativeBattleEffectArenaTransaction transaction = new(source);
        Assert.Equal(row, transaction.View.ReadResourcePointer(16));
        transaction.BeginCheckpoint();
        transaction.View.Write(16, BitConverter.GetBytes(0x2000000));
        Assert.Equal(0x2000000u, transaction.View.ReadResourcePointer(16)!.StagingAddress);
        transaction.RollbackCheckpoint();
        Assert.Equal(row, transaction.View.ReadResourcePointer(16));
        NativeBattleEffectArena frozen = transaction.Freeze();
        frozen.Validate();
        Assert.Equal(row, frozen.ReadResourcePointer(16));
        Assert.Throws<NotSupportedException>(() => frozen.Read(16, 4));
    }

    [Fact]
    public void Staging_retains_symbolic_row_without_aliasing_a_CPU_geometry_pointer()
    {
        uint address = NativeBattleFileStaging.Address + 0x100;
        NativeBattleResourcePointer row = new(null, null) { CpuRow = 3 };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().WriteResourcePointer(address, row);
        staging.Validate();
        Assert.Equal(row, staging.ReadResourcePointer(address));
        Assert.Throws<NotSupportedException>(() => staging.Read(address, 4));
        NativeBattleFileStaging restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(staging)))!);
        restored.Validate();
        Assert.Equal(row, restored.ReadResourcePointer(address));
        Assert.Equal(0u, restored.Write(address, new byte[4]).ReadResourcePointer(address)!.StagingAddress);
    }

    private static NativeBattleEffectTaskPool Pool() => NativeBattleEffectTaskPool.Initialize(0x2521320, Bank, 64, 1);
}
