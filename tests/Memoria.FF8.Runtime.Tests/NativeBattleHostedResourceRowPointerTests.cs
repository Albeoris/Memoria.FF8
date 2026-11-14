namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedResourceRowPointerTests
{
    private const uint StagingField = NativeBattleFileStaging.Address + 0x100;
    private const int TaskField = 36;

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(36)]
    [InlineData(50)]
    public void Typed_alias_survives_staging_and_task_round_trips_without_numeric_bytes(int row)
    {
        NativeBattleResourcePointer pointer = new(null, null) { CpuRow = row };
        NativeBattleResourceRowReference reference = new(row, null);
        Assert.Null(pointer.NativeByteValue);
        Assert.Same(pointer, pointer.WithNativeAddressEncoding());

        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(StagingField, [1, 2, 3, 4]).WriteResourcePointer(StagingField, pointer);
        staging = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(staging));
        staging.Validate();
        Assert.Equal(pointer, staging.ReadResourcePointer(StagingField));
        Assert.All(staging.ReadSnapshot(StagingField, 4).Known, known => Assert.False(known));
        Assert.Throws<NotSupportedException>(() => staging.ReadUInt32(StagingField));

        NativeBattleModulePoolTask task = CreateTask().WriteDword(TaskField, 0x12345678).WriteResourceRowReference(TaskField, reference);
        NativeBattleModulePoolTask captured = NativeBattleStorageSnapshots.Capture(task);
        Assert.Equal(reference, captured.ReadResourceRowReference(TaskField));
        task = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(reference, task.ReadResourceRowReference(TaskField));
        Assert.Throws<NotSupportedException>(() => task.ReadDword(TaskField));

        staging.WriteByte(StagingField + 4, 0xA5);
        task.WriteByte(TaskField + 4, 0xA5);
        Assert.Equal(pointer, staging.ReadResourcePointer(StagingField));
        Assert.Equal(reference, task.ReadResourceRowReference(TaskField));

        staging.WriteByte(StagingField + 1, 0xA5);
        task.WriteByte(TaskField + 1, 0xA5);
        Assert.False(staging.TryGetResourcePointerMetadata(StagingField, out _));
        Assert.Null(NativeBattleStorageSnapshots.Capture(task).ResourcePointers);
        Assert.Throws<NotSupportedException>(() => staging.ReadUInt32(StagingField));
        Assert.Throws<NotSupportedException>(() => task.ReadDword(TaskField));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(51)]
    public void Invalid_alias_is_rejected_by_both_metadata_writers(int row)
    {
        NativeBattleResourcePointer pointer = new(null, null) { CpuRow = row };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleModulePoolTask task = CreateTask();
        Assert.False(pointer.HasValidOwner());
        Assert.Throws<InvalidDataException>(() => staging.WriteResourcePointer(StagingField, pointer));
        Assert.Throws<NotSupportedException>(() => task.WriteResourceRowReference(TaskField, new(row, null)));
    }

    private static NativeBattleModulePoolTask CreateTask()
    {
        return NativeBattleEffectTaskPool.Initialize(0x2521320, NativeBattleEffectNodeMemory.ArenaAddress + 0x4000, 64, 1).Slots[0];
    }
}
