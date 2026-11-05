using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStagingResourceMoveTests
{
    private const uint Address = NativeBattleFileStaging.Address;

    [Fact]
    public void Slice_binding_and_unaligned_move_retain_the_original_source_with_independent_COW()
    {
        ImmutableArray<byte> source = Enumerable.Range(0, 18000).Select(value => (byte)(value % 251)).ToImmutableArray();
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().BindResource(Address, source, 17, 12000);
        Assert.True(staging.TryBorrowResource(Address, 12000, out ImmutableArray<byte> bound, out int offset));
        Assert.Equal(source, bound);
        Assert.Equal(17, offset);
        uint destination = Address + 0x10000;
        staging.CopyResourceMemory(Address + 100, destination, 8192);
        Assert.True(staging.TryBorrowResource(destination, 8192, out bound, out offset));
        Assert.Equal(source, bound);
        Assert.Equal(117, offset);
        Assert.Equal(0, staging.MaterializedPageCount);
        using NativeBattleStagingReadWindow history = staging.OpenReadWindow(destination, 8192, maximumRetainedBytes: 0);
        staging.WriteByte(destination + 5000, 0xFE);
        Assert.Equal(1, staging.MaterializedPageCount);
        Assert.Equal(source[5117], history.ReadByte(5000));
        Assert.Equal(source[5117], staging.ReadByte(Address + 5100));
        Assert.Equal(0xFE, staging.ReadByte(destination + 5000));
        Assert.Equal(0, history.RetainedByteCount);
        Assert.Throws<InvalidDataException>(() => staging.BindResource(Address, source, source.Length - 1, 2));
        Assert.Throws<InvalidDataException>(() => staging.BindResource(Address, default));
    }

    [Theory]
    [InlineData(-33, false)]
    [InlineData(257, false)]
    [InlineData(-33, true)]
    [InlineData(257, true)]
    public void Resource_move_matches_raw_memmove_for_overlap_opaque_unknowns_pointer_invalidation_and_overlay(int displacement, bool overlay)
    {
        ImmutableArray<byte> source = Enumerable.Range(0, 14000).Select(value => (byte)(value % 251)).ToImmutableArray();
        NativeBattleFileStaging actual = NativeBattleFileStaging.FromColdProcess().BindResource(Address, source, 17, 12000);
        actual.MarkUnknown(Address + 200, 1);
        actual.WorkingOwner.WriteWithKnownness(Address + 200, [0xA5, 0xB6], [false, true]);
        actual.MarkUnknown(Address + 6000, 3).WriteResourcePointer(Address + 8000, new(null, null) { CpuRow = 2 });
        NativeBattleFileStaging expected = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(actual));
        uint first = Address + 100;
        uint destination = checked((uint)((long)first + displacement));
        if (overlay)
        {
            using NativeBattleFileStagingTransaction a = new(actual);
            using NativeBattleFileStagingTransaction b = new(expected);
            a.View.WriteByte(Address + 4000, 0xC7);
            b.View.WriteByte(Address + 4000, 0xC7);
            a.View.CopyResourceMemory(first, destination, 9000);
            b.View.CopyMemory(first, destination, 9000);
            Assert.Equal(b.View.ReadSnapshot(destination, 9000).Data, a.View.ReadSnapshot(destination, 9000).Data);
            Assert.Equal(b.View.ReadSnapshot(destination, 9000).Known, a.View.ReadSnapshot(destination, 9000).Known);
            a.Freeze(a.View);
            b.Freeze(b.View);
        }
        else
        {
            actual.CopyResourceMemory(first, destination, 9000);
            expected.CopyMemory(first, destination, 9000);
        }
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
    }

    [Fact]
    public void Sliced_overlay_prefix_child_publication_and_rollback_keep_exact_historical_source_offsets()
    {
        ImmutableArray<byte> source = Enumerable.Range(0, 18000).Select(value => (byte)(value % 251)).ToImmutableArray();
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        using NativeBattleFileStagingTransaction parent = new(staging);
        parent.View.BindResource(Address, source, 37, 12000);
        using NativeBattleStagingReadWindow first = parent.View.OpenReadWindow(Address, 12000, maximumRetainedBytes: 0);
        parent.BeginCheckpoint();
        using NativeBattleFileStagingTransaction child = NativeBattleFileStagingTransaction.CreatePrivateChild(parent.View);
        child.View.BindResource(Address + 100, source, 2037, 5000);
        using NativeBattleStagingReadWindow second = child.View.OpenReadWindow(Address + 100, 5000, maximumRetainedBytes: 0);
        child.View.CopyResourceMemory(Address + 100, Address + 7000, 5000);
        Assert.True(child.View.TryBorrowResource(Address + 7000, 5000, out ImmutableArray<byte> retained, out int offset));
        Assert.Equal(source, retained);
        Assert.Equal(2037, offset);
        child.Freeze(child.View);
        Assert.Equal(source[2037], parent.View.ReadByte(Address + 7000));
        parent.RollbackCheckpoint();
        Assert.Equal(source[7037], parent.View.ReadByte(Address + 7000));
        Assert.Equal(source[137], first.ReadByte(100));
        Assert.Equal(source[2037], second.ReadByte(0));
        Assert.Equal(0, first.RetainedByteCount);
        Assert.Equal(0, second.RetainedByteCount);
    }
}
