namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleNodeStagingSourceTests
{
    private const uint Address = NativeBattleFileStaging.Address;

    [Fact]
    public void Whole_case_private_scope_keeps_node_publication_private_until_successful_staging_publication()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess().WriteUInt32(Address, 1);
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using NativeBattleFileStagingTransaction scope = NativeBattleFileStagingTransaction.CreatePrivateChild(staging);
            NativeBattleNodeSource source = NativeBattleNodeSource.BorrowPrivateView(scope.View, Address, 16);
            using NativeBattleNodeBankOperation operation = new(source);
            operation.WriteUInt32(0, 2);
            operation.Publish();
            Assert.Equal(2u, scope.View.ReadUInt32(Address));
            Assert.Equal(1u, staging.ReadUInt32(Address));
            throw new InvalidOperationException("Packet delivery failed after node bank publication.");
        }));
        Assert.Equal(1u, staging.ReadUInt32(Address));
        NativeBattleNodeSource retained;
        using (NativeBattleFileStagingTransaction scope = NativeBattleFileStagingTransaction.CreatePrivateChild(staging))
        {
            NativeBattleNodeSource source = NativeBattleNodeSource.BorrowPrivateView(scope.View, Address, 16);
            using (NativeBattleNodeBankOperation operation = new(source))
            {
                operation.WriteUInt32(0, 3);
                operation.Publish();
            }
            scope.Freeze(scope.View);
            retained = source.RebindStaging(staging, Address, 16);
        }
        Assert.Equal(3u, retained.ReadUInt32(0));
        staging.WriteUInt32(Address, 4);
        Assert.Equal(4u, retained.ReadUInt32(0));
    }

    [Fact]
    public void Staging_node_resource_reader_keeps_original_ctor_observation_while_local_stores_and_parent_change()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess().WriteUInt32(Address, 1);
        using NativeBattleFileStagingTransaction scope = NativeBattleFileStagingTransaction.CreatePrivateChild(staging);
        NativeBattleNodeSource source = NativeBattleNodeSource.BorrowPrivateView(scope.View, Address, 16);
        using NativeBattleNodeBankOperation operation = new(source);
        using NativeBattleGfSourceReader reader = operation.OpenReader(0, 16);
        operation.WriteUInt32(0, 2);
        scope.View.WriteUInt32(Address + 4, 3);
        Assert.Equal(2u, operation.ReadUInt32(0));
        Assert.Equal(1u, reader.ReadUInt32(0));
        Assert.Equal(0u, operation.ReadUInt32(4));
        operation.Publish();
        Assert.Equal(2u, source.ReadUInt32(0));
        Assert.Equal(3u, source.ReadUInt32(4));
        Assert.Equal(1u, staging.ReadUInt32(Address));
        Assert.Equal(1u, reader.ReadUInt32(0));
    }

    [Fact]
    public void Explicit_capture_restores_staging_alias_and_hidden_unknown_bytes_without_a_page_arena()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess().Write(Address, [1, 2, 3, 4]);
        staging.MarkUnknown(Address + 1, 1);
        NativeBattleNodeSource source = NativeBattleNodeSource.Borrow(staging, Address, 4);
        History frozen = NativeBattleStorageSnapshots.Capture(new History(staging, source));
        Assert.Null(((INativeBattleArenaStorageNode)frozen.Source).SnapshotArena);
        History restored = NativeBattleStorageSnapshots.Restore(frozen);
        Assert.Same(restored.Staging.OwnerIdentity, restored.Source.StagingAuthority.OwnerIdentity);
        byte[] data = new byte[4];
        bool[] known = new bool[4];
        restored.Source.ReadWithKnownness(0, data, known);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, data);
        Assert.Equal(new[] { true, false, true, true }, known);
        restored.Staging.WriteByte(Address, 9);
        Assert.Equal((byte)9, restored.Source.ReadByte(0));
        Assert.Equal((byte)1, frozen.Source.Data[0]);
    }

    private sealed record History(NativeBattleFileStaging Staging, NativeBattleNodeSource Source);
}
