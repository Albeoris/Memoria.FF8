using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTemporaryMemoryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(512)]
    [InlineData(513)]
    public void Fused_views_preserve_unknown_backing_bytes_exact_bounds_and_detachment(int length)
    {
        uint address = NativeBattleTemporaryMemory.Address + 64;
        byte[] bytes = Enumerable.Range(0, length).Select(index => unchecked((byte)(index + 1))).ToArray();
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, bytes);
        if (length > 1)
        {
            staging.MarkUnknown(address + 1, 1);
        }
        var expected = staging.ReadSnapshot(address, length);

        NativeBattleEffectArena view = NativeBattleTemporaryMemory.ReadView(staging, address, length);
        staging.Write(address, new byte[length]);

        Assert.Equal(length, view.LogicalLength);
        Assert.Equal(expected.Data, view.ReadSnapshot(0, length).Data);
        Assert.Equal(expected.Known, view.ReadSnapshot(0, length).Known);
        Assert.Throws<InvalidDataException>(() => view.Read(length, 1));
        if (length > 1)
        {
            Assert.Throws<NotSupportedException>(() => view.Read(1, 1));
        }
    }

    [Fact]
    public void Staging_copy_reads_live_transaction_bytes_and_routes_borrowed_destination_with_rollback()
    {
        uint address = NativeBattleTemporaryMemory.Address + 64;
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().Write(address, [1, 2, 3, 4]);
        NativeBattleEffectArena owner = NativeBattleEffectArena.Clear();
        NativeBattleEffectArena window = NativeBattleEffectArena.BorrowWorking(owner.Working, 32, 16);
        using NativeBattleFileStagingTransaction stagingTransaction = new(source);
        stagingTransaction.View.Write(address, [9, 8, 7, 6]).MarkUnknown(address + 1, 1);
        using NativeBattleEffectArenaTransaction arenaTransaction = new(window);
        arenaTransaction.BeginCheckpoint();

        Assert.Same(window, NativeBattleTemporaryMemory.CopyToArena(stagingTransaction.View, address, window, 4, 4));

        Assert.Equal(new byte[] { 9, 8, 7, 6 }, window.ReadSnapshot(4, 4).Data);
        Assert.Equal(new[] { true, false, true, true }, window.ReadSnapshot(4, 4).Known);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, source.Read(address, 4));
        Assert.Equal(new byte[4], owner.Read(36, 4));
        Assert.Throws<InvalidOperationException>(() => owner.Capture());
        arenaTransaction.RollbackCheckpoint();
        Assert.Equal(new byte[4], window.Read(4, 4));
        arenaTransaction.Freeze();
        Assert.Equal(new byte[4], owner.Read(36, 4));
    }

    [Fact]
    public void Staging_copy_keeps_numeric_unknownness_without_importing_pointer_metadata()
    {
        uint address = NativeBattleTemporaryMemory.Address + 64;
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().WriteResourcePointer(address, null);
        NativeBattleEffectArena destination = NativeBattleEffectArena.Clear().WriteResourcePointer(4, new(null, 0x12345678));

        NativeBattleTemporaryMemory.CopyToArena(source, address, destination, 4, 4);

        Assert.False(destination.TryGetResourcePointer(4, out _));
        Assert.Equal(source.ReadSnapshot(address, 4).Data, destination.ReadSnapshot(4, 4).Data);
        Assert.Equal(source.ReadSnapshot(address, 4).Known, destination.ReadSnapshot(4, 4).Known);
    }

    [Fact]
    public void Staging_copy_preserves_range_then_source_validation_order_and_never_mutates_on_failure()
    {
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown();
        NativeBattleFileStagingTransaction transaction = new(source);
        NativeBattleFileStaging disposed = transaction.View;
        transaction.Dispose();
        NativeBattleEffectArena destination = NativeBattleEffectArena.Clear().Write(0, [3, 4]);

        Assert.Equal("length", Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleTemporaryMemory.CopyToArena(disposed, uint.MaxValue, destination, -1, 2)).ParamName);
        Assert.Throws<ObjectDisposedException>(() => NativeBattleTemporaryMemory.CopyToArena(disposed, NativeBattleTemporaryMemory.Address, destination, -1, 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleTemporaryMemory.CopyToArena(source, NativeBattleTemporaryMemory.Address, destination, -1, 2));
        Assert.Equal(new byte[] { 3, 4 }, destination.Read(0, 2));
        Assert.Equal(0, NativeBattleTemporaryMemory.ReadView(source, uint.MaxValue, 0).LogicalLength);
        Assert.Equal(1, NativeBattleTemporaryMemory.ReadView(source, uint.MaxValue, 1).LogicalLength);
    }

    [Fact]
    public void RoundedAllocationsReleaseTheirCursorAndRetainActualBytes()
    {
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
        NativeBattleTemporaryAllocation first = NativeBattleTemporaryMemory.Allocate5082B0(staging, 3);
        Assert.Equal(NativeBattleTemporaryMemory.Address, first.Address);
        staging = first.Staging.Write(first.Address, new byte[] { 1, 2, 3 });
        NativeBattleTemporaryAllocation second = NativeBattleTemporaryMemory.Allocate5082B0(staging, 400);
        Assert.Equal(first.Address + 4, second.Address);
        staging = NativeBattleTemporaryMemory.Release5082D0(second.Staging, 400);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 3);
        Assert.Equal(first.Address, BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(new byte[] { 1, 2, 3 }, staging.Read(first.Address, 3));
        Assert.Throws<NotSupportedException>(() => staging.Read(first.Address + 3, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleTemporaryMemory.Release5082D0(staging, 4));
    }

    [Fact]
    public void SlicesPreserveUnknownPaddingAndIndependentRestore()
    {
        uint address = NativeBattleTemporaryMemory.Address + 64;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, new byte[] { 1, 2, 3, 4, 5, 6 }).Write(address + 8, new byte[] { 9, 10 });
        NativeBattleEffectArena view = NativeBattleTemporaryMemory.ReadView(staging, address, 10);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, view.Read(0, 6));
        Assert.Throws<NotSupportedException>(() => view.Read(6, 2));
        view = view.Write(6, new byte[] { 7, 8 }).MarkUnknown(1, 2);
        NativeBattleFileStaging restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(staging))!;
        NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(restored, address, view, 10);
        Assert.Equal(new byte[] { 4, 5, 6, 7, 8, 9, 10 }, published.Read(address + 3, 7));
        Assert.Throws<NotSupportedException>(() => published.Read(address + 1, 2));
        Assert.Equal(NativeBattleTestSnapshots.Json(published), NativeBattleTestSnapshots.Json(NativeBattleTemporaryMemory.PublishView(staging, address, view, 10)));
    }
}
