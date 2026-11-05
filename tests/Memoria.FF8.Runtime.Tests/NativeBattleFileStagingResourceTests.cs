using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFileStagingResourceTests
{
    private const uint Address = NativeBattleFileStaging.Address;

    [Fact]
    public void Dat_binding_retains_source_and_materializes_only_the_written_page()
    {
        ImmutableArray<byte> source = Dat(16384);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().BindResource(Address, source);
        Assert.Equal(source, staging.ReadDat(Address));
        Assert.Equal(0, staging.MaterializedPageCount);
        using NativeBattleStagingReadWindow window = staging.OpenReadWindow(Address, source.Length, maximumRetainedBytes: 0);
        staging.WriteByte(Address + 5000, 0xA5);
        Assert.Equal(1, staging.MaterializedPageCount);
        Assert.Equal(0, window.RetainedByteCount);
        Assert.Equal(source[5000], window.ReadByte(5000));
        Assert.Equal(0xA5, staging.ReadByte(Address + 5000));
        Assert.NotEqual(source, staging.ReadDat(Address));
        Assert.Equal(0xA5, staging.ReadDat(Address)[5000]);
        Assert.Equal(0, source[5000]);
    }

    [Fact]
    public void Partial_page_binding_preserves_neighbor_knownness_raw_unknown_bytes_and_zero_proofs()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        staging.MarkUnknown(Address - 2, 1).MarkUnknown(Address + 5001, 1);
        staging.WorkingOwner.WriteWithKnownness(Address - 3, [0xA5, 0xB6, 0xC7], [true, false, true]);
        staging.WorkingOwner.WriteWithKnownness(Address + 5001, [0xD8, 0xE9], [false, true]);
        NativeBattleFileStaging expected = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(staging))!;
        ImmutableArray<byte> resource = new byte[5001].ToImmutableArray();
        staging.BindResource(Address, resource);
        expected.Write(Address, resource.AsSpan());
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(staging));
        staging.MarkUnknown(Address - 2, 1);
        expected.MarkUnknown(Address - 2, 1);
        Assert.Equal(0xB6, staging.ReadSnapshot(Address - 2, 1).Data[0]);
        Assert.False(staging.ReadSnapshot(Address - 2, 1).Known[0]);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(staging));
        Assert.Equal(0, staging.ReadByte(Address + 5003));
    }

    [Fact]
    public void Owner_nested_rollback_restores_resource_identity_and_page_map()
    {
        ImmutableArray<byte> original = Dat(12288);
        ImmutableArray<byte> replacement = Dat(16384);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().BindResource(Address, original);
        string before = NativeBattleTestSnapshots.Json(staging);
        staging.WorkingOwner.BeginCheckpoint();
        staging.WriteByte(Address + 5000, 1);
        staging.WorkingOwner.BeginNestedCheckpoint();
        staging.BindResource(Address, replacement).WriteByte(Address + 10000, 2);
        staging.WorkingOwner.CommitNestedCheckpoint();
        staging.WorkingOwner.RollbackCheckpoint();
        Assert.Equal(original, staging.ReadDat(Address));
        Assert.Equal(0, staging.MaterializedPageCount);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(staging));
    }

    [Fact]
    public void Ordered_overlay_binding_write_and_unknown_publication_matches_direct_memory()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        NativeBattleFileStaging expected = NativeBattleFileStaging.FromColdProcess();
        using NativeBattleFileStagingTransaction transaction = new(staging);
        Apply(expected);
        Apply(transaction.View);
        Assert.Equal(expected.ReadSnapshot(Address, 12300).Data, transaction.View.ReadSnapshot(Address, 12300).Data);
        Assert.Equal(expected.ReadSnapshot(Address, 12300).Known, transaction.View.ReadSnapshot(Address, 12300).Known);
        transaction.Freeze(transaction.View);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(staging));

        static void Apply(NativeBattleFileStaging target)
        {
            target.Write(Address + 16, [7, 8]);
            target.MarkUnknown(Address + 20, 4);
            target.BindResource(Address, Dat(12288));
            target.WriteByte(Address + 16, 9);
            target.MarkUnknown(Address + 17, 3);
            target.WriteByte(Address + 18, 10);
            target.BindResource(Address + 4093, ImmutableArray.Create<byte>(1, 2, 3, 4, 5));
            target.MarkUnknown(Address + 4094, 1);
        }
    }

    [Fact]
    public void Child_bindings_publish_to_parent_and_rollback_with_parent_checkpoint()
    {
        ImmutableArray<byte> original = Dat(12288);
        ImmutableArray<byte> replacement = Dat(16384);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().BindResource(Address, original);
        using NativeBattleFileStagingTransaction parent = new(staging);
        parent.BeginCheckpoint();
        using (NativeBattleFileStagingTransaction child = NativeBattleFileStagingTransaction.CreatePrivateChild(parent.View))
        {
            child.View.BindResource(Address, replacement);
            Assert.Equal(replacement, child.View.ReadDat(Address));
            child.Freeze(child.View);
        }
        Assert.Equal(replacement, parent.View.ReadDat(Address));
        Assert.Equal(original, staging.ReadDat(Address));
        parent.RollbackCheckpoint();
        Assert.Equal(original, parent.View.ReadDat(Address));
        parent.View.BindResource(Address, replacement);
        parent.Freeze(parent.View);
        Assert.Equal(replacement, staging.ReadDat(Address));
        Assert.Equal(0, staging.MaterializedPageCount);
    }

    [Fact]
    public void Read_windows_keep_source_prefixes_and_unknown_order_across_rebind_and_rollback()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        using NativeBattleFileStagingTransaction parent = new(staging);
        parent.View.WriteByte(Address + 16, 0xA5);
        parent.View.MarkUnknown(Address, 32);
        parent.View.BindResource(Address, Dat(12288));
        parent.View.MarkUnknown(Address + 17, 1);
        using NativeBattleStagingReadWindow first = parent.View.OpenReadWindow(Address, 12288, maximumRetainedBytes: 0);
        Assert.Equal(0, first.RetainedByteCount);
        Assert.Equal(0, first.ReadByte(16));
        Assert.Throws<NotSupportedException>(() => first.ReadByte(17));
        parent.BeginCheckpoint();
        using NativeBattleFileStagingTransaction child = NativeBattleFileStagingTransaction.CreatePrivateChild(parent.View);
        child.View.BindResource(Address, Dat(16384));
        child.View.WriteByte(Address + 17, 0xB6);
        using NativeBattleStagingReadWindow second = child.View.OpenReadWindow(Address, 12288, maximumRetainedBytes: 1);
        Assert.Equal(1, second.RetainedByteCount);
        child.Freeze(child.View);
        parent.RollbackCheckpoint();
        parent.View.WriteByte(Address + 16, 0xC7);
        Assert.Equal(0, first.ReadByte(16));
        Assert.Throws<NotSupportedException>(() => first.ReadByte(17));
        Assert.Equal(0, second.ReadByte(16));
        Assert.Equal(0xB6, second.ReadByte(17));
    }

    [Fact]
    public void Child_source_binding_does_not_retain_shadowed_parent_byte_journal()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        using NativeBattleFileStagingTransaction parent = new(staging);
        parent.View.Write(Address, new byte[12288]);
        using NativeBattleFileStagingTransaction child = NativeBattleFileStagingTransaction.CreatePrivateChild(parent.View);
        child.View.BindResource(Address, Dat(12288));
        using NativeBattleStagingReadWindow window = child.View.OpenReadWindow(Address, 12288, maximumRetainedBytes: 0);
        Assert.Equal(0, window.RetainedByteCount);
        Assert.Equal(1, window.ReadInt32(0));
        parent.View.WriteByte(Address + 32, 99);
        child.View.WriteByte(Address + 32, 88);
        Assert.Equal(0, window.ReadByte(32));
    }

    [Fact]
    public void Legacy_diagnostic_json_materializes_source_bindings_and_continues_independently()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().BindResource(Address, Dat(12288));
        staging.MarkUnknown(Address + 5000, 2).WriteResourcePointer(Address + 6000, new(null, null) { CpuRow = 2 });
        string json = NativeBattleTestSnapshots.Json(staging);
        Assert.DoesNotContain("ResourceBinding", json);
        NativeBattleFileStaging restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(json)!;
        Assert.Equal(json, NativeBattleTestSnapshots.Json(restored));
        restored.WriteByte(Address + 5000, 0xA5);
        Assert.Equal(0xA5, restored.ReadByte(Address + 5000));
        Assert.Throws<NotSupportedException>(() => staging.ReadByte(Address + 5000));
        Assert.Equal(2, restored.ReadResourcePointer(Address + 6000)!.CpuRow);
    }

    [Fact]
    public void Model_loader_binds_raw_and_prepared_dat_through_completion_without_staging_copy()
    {
        int[] offsets = [40, 40, 56, 56, 56, 56, 64, 68];
        byte[] bytes = new byte[72];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, offsets.Length);
        for (int index = 0; index < offsets.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 4), offsets[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(36), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(60), 8);
        NativeBattleModelDatSource source = new(0, false, 2, 310, "model.dat", bytes.ToImmutableArray());
        NativeBattleModelLoadStep request = NativeBattleModelLoader.Queue507080(0, 1, 0, 7, 2).Poll507070(0, [source], NativeBattleFileStaging.Unknown());
        Assert.Equal(source.Bytes, request.Staging.ReadDat(Address));
        Assert.Equal(0, request.Staging.MaterializedPageCount);
        NativeBattleModelLoadStep texture = request.State.Poll507070(source.Bytes.Length, [source], request.Staging);
        ImmutableArray<byte> prepared = texture.Staging.ReadDat(Address);
        Assert.Equal(0, texture.Staging.MaterializedPageCount);
        NativeBattleModelLoadStep complete = texture.State.Poll507070(source.Bytes.Length, [source], texture.Staging);
        Assert.Equal(prepared, complete.Bind!.PreparedDat.Bytes);
        Assert.Equal(prepared, complete.Staging.ReadDat(Address));
    }

    private static ImmutableArray<byte> Dat(int length)
    {
        byte[] bytes = new byte[length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), length);
        return bytes.ToImmutableArray();
    }
}
