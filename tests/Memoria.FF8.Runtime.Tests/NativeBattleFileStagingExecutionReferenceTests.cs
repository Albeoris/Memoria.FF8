using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFileStagingExecutionReferenceTests
{
    private const uint Start = NativeBattleFileStaging.Address + 4095;
    private static readonly BattleExecutionReference.Model Model = new(new BattleEnemyActorModel(new("staging-enemy")));
    private static readonly BattleExecutionReference.Header Header = new(new(12));

    [Fact]
    public void Exact_kind_width_and_unknown_bytes_hold_across_page_boundaries()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        staging.WriteExecutionReference(Start, 4, Header);
        staging.WriteExecutionReference(Start + 5, 1, Model, [5]);

        Assert.Equal(Header, staging.ReadExecutionReference<BattleExecutionReference.Header>(Start, 4));
        Assert.Equal(Model, staging.ReadExecutionReference<BattleExecutionReference.Model>(Start + 5, 1));
        Assert.Equal((byte)5, staging.ReadByte(Start + 5));
        Assert.All(staging.ReadSnapshot(Start, 4).Known, known => Assert.False(known));
        Assert.False(staging.TryReadExecutionReference<BattleExecutionReference.Header>(Start, 2, out _));
        Assert.False(staging.TryReadExecutionReference<BattleExecutionReference.Model>(Start, 4, out _));
        staging.WriteUInt16(Start + 8, 12);
        Assert.Throws<InvalidDataException>(() => staging.ReadExecutionReference<BattleExecutionReference.Header>(Start + 8, 2));
        long version = staging.StorageVersion;
        Assert.Throws<ArgumentOutOfRangeException>(() => staging.WriteExecutionReference(Start, 3, Model));
        Assert.Throws<ArgumentException>(() => staging.WriteExecutionReference(Start, 2, Model, [5]));
        Assert.Equal(version, staging.StorageVersion);
        Assert.Equal(Header, staging.ReadExecutionReference<BattleExecutionReference.Header>(Start, 4));
    }

    [Fact]
    public void Overlapping_raw_unknown_and_pointer_writes_invalidate_the_entire_reference()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        staging.WriteExecutionReference(Start, 4, Header, [1, 2, 3, 4]);
        staging.WriteByte(Start + 3, 4);
        Assert.False(staging.TryReadExecutionReference<BattleExecutionReference.Header>(Start, 4, out _));

        staging.WriteExecutionReference(Start, 2, Model);
        staging.WriteResourcePointer(Start + 1, new(null, 0x1234));
        Assert.False(staging.TryReadExecutionReference<BattleExecutionReference.Model>(Start, 2, out _));
        Assert.True(staging.TryGetResourcePointerMetadata(Start + 1, out _));
        staging.WriteExecutionReference(Start + 4, 1, Model, [6]);
        Assert.False(staging.TryGetResourcePointerMetadata(Start + 1, out _));
        staging.MarkUnknown(Start + 4, 1);
        Assert.False(staging.TryReadExecutionReference<BattleExecutionReference.Model>(Start + 4, 1, out _));
    }

    [Fact]
    public void Raw_copy_moves_bytes_and_knownness_without_relocating_references()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        staging.WriteExecutionReference(Start, 2, Header, [12, 0]);
        staging.WriteExecutionReference(Start + 2, 2, Model);
        staging.CopyMemory(Start, Start + 1, 4);
        Assert.Equal(new byte[] { 12, 0 }, staging.Read(Start + 1, 2));
        Assert.All(staging.ReadSnapshot(Start + 3, 2).Known, known => Assert.False(known));
        Assert.False(staging.TryReadExecutionReference<BattleExecutionReference.Header>(Start, 2, out _));
        Assert.False(staging.TryReadExecutionReference<BattleExecutionReference.Header>(Start + 1, 2, out _));
        Assert.False(staging.TryReadExecutionReference<BattleExecutionReference.Model>(Start + 3, 2, out _));

        staging.WriteExecutionReference(Start, 2, Header, [12, 0]);
        staging.CopyResourceMemory(Start, Start + 8, 2);
        Assert.Equal(Header, staging.ReadExecutionReference<BattleExecutionReference.Header>(Start, 2));
        Assert.False(staging.TryReadExecutionReference<BattleExecutionReference.Header>(Start + 8, 2, out _));
        Assert.Equal(new byte[] { 12, 0 }, staging.Read(Start + 8, 2));
    }

    [Fact]
    public void Owner_checkpoint_restores_metadata_and_physical_values()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        staging.WriteExecutionReference(Start, 4, Header, [1, 2, 3, 4]);
        NativeBattleFileStagingWorkingOwner owner = staging.WorkingOwner;
        owner.BeginCheckpoint();
        staging.WriteExecutionReference(Start + 1, 2, Model);
        Assert.Equal(Model, staging.ReadExecutionReference<BattleExecutionReference.Model>(Start + 1, 2));
        Assert.False(staging.TryReadExecutionReference<BattleExecutionReference.Header>(Start, 4, out _));
        owner.RollbackCheckpoint();
        Assert.Equal(Header, staging.ReadExecutionReference<BattleExecutionReference.Header>(Start, 4));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, staging.Read(Start, 4));
    }

    [Fact]
    public void Child_overlay_merge_and_nested_rollback_preserve_the_actual_parent_view()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        staging.WriteExecutionReference(Start, 2, Header, [12, 0]);
        using NativeBattleFileStagingTransaction parent = new(staging);
        parent.BeginCheckpoint();
        using (NativeBattleFileStagingTransaction child = NativeBattleFileStagingTransaction.CreatePrivateChild(parent.View))
        {
            child.View.WriteExecutionReference(Start + 1, 1, Model);
            Assert.Equal(Header, parent.View.ReadExecutionReference<BattleExecutionReference.Header>(Start, 2));
            Assert.Equal(Model, child.View.ReadExecutionReference<BattleExecutionReference.Model>(Start + 1, 1));
            child.Freeze(child.View);
        }
        Assert.Equal(Model, parent.View.ReadExecutionReference<BattleExecutionReference.Model>(Start + 1, 1));
        Assert.False(parent.View.TryReadExecutionReference<BattleExecutionReference.Header>(Start, 2, out _));
        parent.RollbackCheckpoint();
        Assert.Equal(Header, parent.View.ReadExecutionReference<BattleExecutionReference.Header>(Start, 2));

        parent.View.WriteExecutionReference(Start + 1, 1, Model, [9]);
        parent.Freeze(parent.View);
        Assert.Equal(Model, staging.ReadExecutionReference<BattleExecutionReference.Model>(Start + 1, 1));
        Assert.Equal((byte)9, staging.ReadByte(Start + 1));
    }

    [Fact]
    public void Detached_in_process_storage_preserves_references_and_public_json_keeps_only_native_data()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        staging.WriteExecutionReference(Start, 2, Header, [12, 0]);
        staging.WriteExecutionReference(Start + 4, 1, Model);
        staging.WriteResourcePointer(Start + 8, new(null, 0x12345678));
        NativeBattleFileStaging detached = NativeBattleStorageSnapshots.Capture(staging);
        NativeBattleFileStaging restored = NativeBattleStorageSnapshots.Restore(detached);
        Assert.Equal(Header, detached.ReadExecutionReference<BattleExecutionReference.Header>(Start, 2));
        Assert.Equal(Model, restored.ReadExecutionReference<BattleExecutionReference.Model>(Start + 4, 1));
        restored.WriteByte(Start + 4, 7);
        Assert.False(restored.TryReadExecutionReference<BattleExecutionReference.Model>(Start + 4, 1, out _));
        Assert.Equal(Model, staging.ReadExecutionReference<BattleExecutionReference.Model>(Start + 4, 1));

        string json = JsonSerializer.Serialize(detached);
        Assert.DoesNotContain("ExecutionReferences", json);
        Assert.DoesNotContain("staging-enemy", json);
        NativeBattleFileStaging retained = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(json)!);
        Assert.Equal(new byte[] { 12, 0 }, retained.Read(Start, 2));
        Assert.False(retained.ReadSnapshot(Start + 4, 1).Known[0]);
        Assert.Equal(0x12345678u, retained.ReadResourcePointer(Start + 8)!.StagingAddress);
        Assert.False(retained.TryReadExecutionReference<BattleExecutionReference.Header>(Start, 2, out _));
    }
}
